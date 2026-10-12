using System.Net;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Server.Pairing;

namespace ClankerWorld.Server.Providers;

/// <summary>
/// Runs one explicit, metered request through the same personal-model adapter
/// used by agents. It does not change provider configuration or try another
/// request shape when a provider refuses the game's JSON response format.
/// </summary>
public sealed class ProviderSetupCheckService(
    ProviderConfigurationStore configuration,
    ProviderUsageStore usage,
    IHttpClientFactory httpClientFactory)
{
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private const string ModelName = "setup-check";
    private const string CandidateId = "safe_idle";

    public async Task<OwnerProviderSetupCheckResult> CheckAsync(
        OwnerProviderSetupCheckAction action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        _ = OwnerHttpBinding.ProviderSetupCheckPayload(action);

        var providerId = PlayerDecisionProviders.Normalize(action.Provider);
        var model = action.Model.Trim();
        var thinking = ModelThinking.Normalize(action.Thinking);
        var runtime = configuration.CaptureRuntimeConfiguration();
        var apiKey = ResolveApiKey(runtime, action, providerId);
        if (apiKey is null)
            return Result("missing_key", "Choose or add an API key first. Nothing was sent.");
        if (apiKey.Length == 0)
            return Result("missing_key", "Choose or add an API key first. Nothing was sent.");

        string ticket;
        try
        {
            // The reservation is persisted before constructing or sending the
            // one provider request. Every outcome after this point is charged.
            ticket = usage.Begin(providerId, model, "setup");
        }
        catch (ProviderUsageLimitReachedException)
        {
            return Result("usage_limit", "The model-call limit has been reached, so no test was sent.");
        }
        catch (InvalidOperationException) when (usage.Capture().AccountingError is not null)
        {
            return Result("usage_limit", "Model calls are paused because the world server can't read its call count. No test was sent.");
        }

        var request = CreateRequest();
        var provider = providerId == PlayerDecisionProviders.Anthropic
            ? new OpenAiCompatibleDecisionProvider(
                new AnthropicModelClient(httpClientFactory.CreateClient("model"), apiKey),
                model,
                thinking,
                RequestTimeout,
                request.ProviderEpoch)
            : new OpenAiCompatibleDecisionProvider(
                httpClientFactory.CreateClient("model"),
                () => apiKey,
                providerId == PlayerDecisionProviders.OpenAi
                    ? PlayerDecisionProviders.OpenAiEndpoint
                    : PlayerDecisionProviders.OllamaCloudEndpoint,
                model,
                requestTimeout: RequestTimeout,
                providerEpoch: request.ProviderEpoch,
                thinking: thinking);

        CognitionDecisionResponse response;
        try
        {
            response = await provider.DecideAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            usage.Finish(ticket, cancellationToken.IsCancellationRequested ? "abandoned" : "failed");
            if (cancellationToken.IsCancellationRequested)
                throw;
            return Result("timed_out", "The model service didn't answer within 15 seconds. This test still counts as a model call.");
        }
        catch (HttpRequestException exception)
        {
            usage.Finish(ticket, "failed");
            return exception.StatusCode switch
            {
                HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity when thinking is not null =>
                    Result("unsupported_format", "The model service turned down this request. This test still counts as a model call. Choose another model, or set Thinking to Model default."),
                HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity when providerId == PlayerDecisionProviders.Anthropic =>
                    Result("unsupported_format", "The model service turned down this request. This test still counts as a model call. Choose another model."),
                HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity =>
                    Result("unsupported_format", "This model can't reply in the format the game needs. This test still counts as a model call. Choose another model."),
                HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout =>
                    Result("timed_out", "The model service didn't answer in time. This test still counts as a model call."),
                _ => Result("unavailable", "The model service couldn't finish the test. This test still counts as a model call. Check the key, the model and your connection."),
            };
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _ = UnusableModelReply.TryGetTokens(exception, out var inputTokens, out var outputTokens);
            usage.Finish(ticket, "failed", inputTokens, outputTokens);
            return IsUnusableReply(exception)
                ? Result("unusable", "The model answered, but the game couldn't use its reply. This test still counts as a model call.")
                : Result("unavailable", "The model service couldn't finish the test. This test still counts as a model call. Check the key, the model and your connection.");
        }

        try
        {
            response.Validate();
            if (response.SelectedCandidateId != CandidateId || response.Probabilities.Keys.Any(key => key != CandidateId))
                throw new InvalidDataException("The provider selected an unoffered setup-check action.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or InvalidDataException)
        {
            usage.Finish(ticket, "failed", response.Usage?.InputTokens ?? 0, response.Usage?.OutputTokens ?? 0);
            return Result("unusable", "The model answered, but the game couldn't use its reply. This test still counts as a model call.");
        }

        usage.Finish(ticket, "completed", response.Usage?.InputTokens ?? 0, response.Usage?.OutputTokens ?? 0);
        return new OwnerProviderSetupCheckResult(
            "ready",
            "The model sent a reply the game can use. This test used one model call.",
            IsReady: true);
    }

    private static string? ResolveApiKey(
        RuntimeProviderConfiguration runtime,
        OwnerProviderSetupCheckAction action,
        string provider)
    {
        if (!string.IsNullOrWhiteSpace(action.ApiKey))
            return action.ApiKey.Trim();

        if (action.CredentialSlotId is { } slotId)
        {
            var slot = runtime.CredentialSlots?.FirstOrDefault(item => item.Id == slotId && item.Provider == provider);
            return string.IsNullOrWhiteSpace(slot?.ApiKey) ? null : slot.ApiKey.Trim();
        }

        var credential = provider switch
        {
            PlayerDecisionProviders.OpenAi => runtime.OpenAi,
            PlayerDecisionProviders.OllamaCloud => runtime.OllamaCloud,
            _ => ProviderConfigurationStore.AnthropicCredential(runtime.Anthropic),
        };
        return string.IsNullOrWhiteSpace(credential.ApiKey) ? null : credential.ApiKey.Trim();
    }

    private static CognitionDecisionRequest CreateRequest() => new(
        "owner-model-setup-check",
        0,
        new AgentObservation(
            ModelName,
            WorldTick: 0,
            RunEpoch: 0,
            DecisionGeneration: 0,
            ObservationDigest: "sha256:owner-model-setup-check",
            FullnessBasisPoints: 5_000,
            Candidates: [new CognitionCandidate(CandidateId, "Wait safely for a moment.")]));

    private static bool IsUnusableReply(Exception exception) => exception is
        InvalidDataException or JsonException or ArgumentException or InvalidOperationException or
        KeyNotFoundException or IndexOutOfRangeException or FormatException;

    private static OwnerProviderSetupCheckResult Result(string outcome, string message) =>
        new(outcome, message, IsReady: false);
}
