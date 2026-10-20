using System.Net;
using System.Text.Json;

namespace ClankerWorld.Simulation.Cognition;

/// <summary>Retains only known error categories, never remote text or credentials.</summary>
public sealed class ProviderHttpException(HttpStatusCode status, string? failure)
    : HttpRequestException($"The model service returned HTTP {(int)status}.", null, status)
{
    public string? Failure { get; } = failure;

    public static async Task<ProviderHttpException> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? code = null, type = null;
        try
        {
            var body = await ProviderResponseBody.ReadAsync(response.Content, cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                code = Text(error, "code");
                type = Text(error, "type");
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException or HttpRequestException)
        {
            // An unreadable error body must still preserve the HTTP failure.
        }

        var failure = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => code == "invalid_api_key" ? "invalid_key" : "authentication_failed",
            HttpStatusCode.Forbidden => "access_denied",
            HttpStatusCode.NotFound => "model_not_available",
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => "timed_out",
            HttpStatusCode.TooManyRequests => code switch
            {
                "credit_balance_exhausted" => "no_credit",
                "organization_spend_limit_exceeded" => "organization_spend_limit",
                "project_spend_limit_exceeded" => "project_spend_limit",
                "organization_usage_limit_exceeded" => "provider_usage_limit",
                "insufficient_quota" => "quota_exceeded",
                _ when type == "insufficient_quota" => "quota_exceeded",
                _ => "rate_limited",
            },
            >= HttpStatusCode.InternalServerError => "service_unavailable",
            _ => null,
        };
        return new ProviderHttpException(response.StatusCode, failure);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
