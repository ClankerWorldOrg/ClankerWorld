using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.Playtest;

public enum PrivateWorldTickFailurePolicy { Strict, RefuseSingleAction }

public sealed record PrivateWorldActionRefusal(string InhabitantId, string CandidateId,
    long RefusedTick, long RetryAfterTick, string Check);

public sealed partial class PrivateWorldRuntime
{
    private IReadOnlySet<string>? withheldActions;
    private Dictionary<string, string>? attemptedActions;
    private List<PrivateWorldActionRefusal> actionRefusals = [];
    private long refusedActionCount;

    private bool IsRefusedAction(string id, string candidate) => candidate != "safe_idle" &&
        actionRefusals.Any(refusal => refusal.InhabitantId == id && refusal.CandidateId == candidate && WorldTick < refusal.RetryAfterTick);

    private void RecordActionRefusal(PrivateWorldActionRefusal refusal)
    {
        if (PendingInstructionFor(refusal.InhabitantId) is { } order)
            CancelActiveOrder(order, "game_error", "Cancelled because this choice caused a game error.");
        society.ClearCurrentIntention(refusal.InhabitantId, WorldTick);
        actionRefusals.RemoveAll(item => item.InhabitantId == refusal.InhabitantId && item.CandidateId == refusal.CandidateId);
        actionRefusals.Add(refusal);
        actionRefusals = actionRefusals.OrderBy(item => item.InhabitantId, StringComparer.Ordinal)
            .ThenBy(item => item.CandidateId, StringComparer.Ordinal).ToList();
        refusedActionCount = checked(refusedActionCount + 1);
        AppendEvent("action_refused", JsonSerializer.Serialize(refusal));
    }

    private static void ValidateActionRefusals(PrivateWorldRuntimeState state)
    {
        if (state.ActionRefusals is null || state.RefusedActionCount < state.ActionRefusals.Count)
            throw new InvalidDataException("The saved action-refusal count is invalid.");
        var identities = state.Society.Society.Inhabitants.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<(string, string)>();
        foreach (var refusal in state.ActionRefusals)
            if (refusal is null || !identities.Contains(refusal.InhabitantId) || string.IsNullOrWhiteSpace(refusal.CandidateId) ||
                refusal.CandidateId == "safe_idle" || refusal.CandidateId.Any(char.IsControl) ||
                refusal.RefusedTick < 1 || refusal.RefusedTick > state.Society.Society.WorldTick ||
                refusal.RetryAfterTick - refusal.RefusedTick != state.WorldSystems!.Config.TicksPerDay ||
                refusal.RetryAfterTick <= state.Society.Society.WorldTick ||
                string.IsNullOrEmpty(refusal.Check) || refusal.Check.Length > 96 ||
                refusal.Check.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '.')) ||
                !seen.Add((refusal.InhabitantId, refusal.CandidateId)))
                throw new InvalidDataException("The saved action-refusal boundary is invalid.");
    }

    /// <summary>The host prepares exact checkpoint bytes before publishing a tick; fixtures default to strict failure.</summary>
    public ValueTask<PrivateWorldStepResult> AdvanceOneTickWithCheckpointAsync(
        Func<PrivateWorldRuntime, Action> prepareCheckpoint,
        Func<bool>? commitPermitted = null,
        Func<PrivateWorldRuntime, IReadOnlyList<PlaytestWorldEvent>, IReadOnlyList<PreparedChildModelSelection>>? prepareChildModels = null,
        PrivateWorldTickFailurePolicy failurePolicy = PrivateWorldTickFailurePolicy.Strict,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prepareCheckpoint);
        return AdvanceOneTickCoreAsync(true, commitPermitted, prepareChildModels, cancellationToken,
            prepareCheckpoint, failurePolicy);
    }

    private sealed record PreparedTick(PrivateWorldRuntime Runtime, PrivateWorldStepResult Result, Action? WriteCheckpoint) : IDisposable
    {
        public void Dispose() => Runtime.Dispose();
    }

    // Only recovery-enabled ticks record local replies. Replayed trials use the
    // same request and response, and cannot spend another provider call. Once
    // admitted, these wrappers return to the ordinary provider path.
    private sealed class TickInputRecorder(Func<string, IDecisionProvider>? factory)
    {
        private readonly Dictionary<string, RecordedProvider> providers = new(StringComparer.Ordinal);
        private bool recording = true;
        public IDecisionProvider ProviderFor(string id)
        {
            if (!recording) return factory?.Invoke(id) ?? new DeterministicDecisionProvider();
            if (!providers.TryGetValue(id, out var provider))
            {
                provider = new RecordedProvider(factory?.Invoke(id) ?? new DeterministicDecisionProvider(), () => recording);
                providers.Add(id, provider);
            }
            return provider;
        }
        public bool IsCurrent => providers.Values.All(provider => provider.IsCurrent);
        public void StopRecording()
        {
            recording = false;
            foreach (var provider in providers.Values) provider.Clear();
            providers.Clear();
        }
    }

    private sealed class RecordedProvider(IDecisionProvider inner, Func<bool> recording) : IDecisionProvider
    {
        private readonly long epoch = inner.ProviderEpoch;
        private readonly Dictionary<string, (CognitionDecisionResponse? Response, ExceptionDispatchInfo? Failure)> replies = new(StringComparer.Ordinal);
        public bool IsCurrent => inner.ProviderEpoch == epoch;
        public DecisionProviderKind Kind => inner.Kind;
        public long ProviderEpoch => recording() ? epoch : inner.ProviderEpoch;
        public DecisionProviderKind KindFor(InhabitantObservation observation) => inner.KindFor(observation);
        public void Clear() => replies.Clear();
        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!recording()) return await inner.DecideAsync(request, cancellationToken).ConfigureAwait(false);
            if (replies.TryGetValue(request.RequestId, out var previous))
            {
                previous.Failure?.Throw();
                return previous.Response!;
            }
            try
            {
                var response = await inner.DecideAsync(request, cancellationToken).ConfigureAwait(false);
                replies.Add(request.RequestId, (response, null));
                return response;
            }
            catch (Exception exception) when (exception is not (OutOfMemoryException or OperationCanceledException))
            {
                replies.Add(request.RequestId, (null, ExceptionDispatchInfo.Capture(exception)));
                throw;
            }
        }
    }

    private static string ActionFailureCheck(Exception exception)
    {
        var method = new StackTrace(exception).GetFrames().Select(frame => frame.GetMethod())
            .FirstOrDefault(method => method?.DeclaringType?.Namespace?.StartsWith("ClankerWorld.", StringComparison.Ordinal) == true);
        var name = method is null ? "tick_boundary" : method.DeclaringType!.Name + "." + method.Name;
        return string.Concat(name.Where(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '.').Take(96));
    }
}
