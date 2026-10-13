using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public enum WorldTickFailurePolicy { Strict, RefuseSingleAction }

public sealed record WorldActionRefusal(string AgentId, string CandidateId,
    long RefusedTick, long RetryAfterTick, string Check);

public sealed partial class WorldRuntime
{
    private IReadOnlySet<string>? withheldActions;
    private Dictionary<string, string>? attemptedActions;
    private List<WorldActionRefusal> actionRefusals = [];
    private long refusedActionCount;

    private void RecordAttemptedAction(string actor, string candidate)
    {
        if (attemptedActions is not null) attemptedActions[actor] = candidate;
    }

    private bool IsReplayChoice(string actor, string candidate)
    {
        var person = agents[actor];
        return CreateUnfilteredCandidates(actor, person, restrictForOrder: true).Any(choice => choice.Id == candidate) ||
            person.Project?.CandidateId == candidate ||
            FarmWorkFor(actor) is { Work: { } work } field && FarmCandidate(work.Kind, field.Position, work.Crop) == candidate ||
            person.Equipment?.Repair is not null && candidate == "repair_equipment" ||
            person.Lesson is { Stage: "accepted" or "training" } && candidate == "lesson_attend" ||
            ActiveStudent(actor) is { } student && candidate == "lesson_teach:" + student.AgentId;
    }

    private bool IsRefusedAction(string id, string candidate) => candidate != "safe_idle" &&
        actionRefusals.Any(refusal => refusal.AgentId == id && refusal.CandidateId == candidate && WorldTick < refusal.RetryAfterTick);

    private void RecordActionRefusal(WorldActionRefusal refusal)
    {
        if (PendingInstructionFor(refusal.AgentId) is { } order)
            CancelActiveOrder(order, "game_error", "Cancelled because this choice caused a game error.");
        if (FarmWorkFor(refusal.AgentId) is { Work: { } work } field &&
            FarmCandidate(work.Kind, field.Position, work.Crop) == refusal.CandidateId) CancelFarmWork(field);
        if (agents[refusal.AgentId].Equipment?.Repair is not null && refusal.CandidateId == "repair_equipment")
            CancelEquipmentRepair(refusal.AgentId);
        if (agents[refusal.AgentId].Project is { Stage: not ("completed" or "cancelled") } project &&
            project.CandidateId == refusal.CandidateId)
        {
            CancelProductionJobs(worldSimulation.ProductionJobs.Concat(worldSimulation.CropBuilds ?? [])
                .Where(job => job.JobId == project.JobId && job.WorkerId == refusal.AgentId &&
                    job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused)
                .ToArray(), "production_action_refused");
            SetProject(refusal.AgentId, project with
            {
                Stage = "cancelled",
                RequiresFreshChoice = false,
                Blocker = "This choice caused a game error.",
            });
        }
        if (agents[refusal.AgentId].Lesson is { Stage: not ("completed" or "cancelled" or "declined") } lesson &&
            refusal.CandidateId == "lesson_attend")
            SetLesson(refusal.AgentId, lesson with { Stage = "cancelled" });
        if (ActiveStudent(refusal.AgentId) is { } student && refusal.CandidateId == "lesson_teach:" + student.AgentId)
            SetLesson(student.AgentId, student.Lesson! with { Stage = "cancelled" });
        society.ClearCurrentIntention(refusal.AgentId, WorldTick);
        actionRefusals.RemoveAll(item => item.AgentId == refusal.AgentId && item.CandidateId == refusal.CandidateId);
        actionRefusals.Add(refusal);
        actionRefusals = actionRefusals.OrderBy(item => item.AgentId, StringComparer.Ordinal)
            .ThenBy(item => item.CandidateId, StringComparer.Ordinal).ToList();
        refusedActionCount = checked(refusedActionCount + 1);
        AppendEvent("action_refused", JsonSerializer.Serialize(refusal));
    }

    private static void ValidateActionRefusals(WorldRuntimeState state)
    {
        if (state.ActionRefusals is null || state.RefusedActionCount < state.ActionRefusals.Count)
            throw new InvalidDataException("The saved action-refusal count is invalid.");
        var identities = state.Society.Society.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<(string, string)>();
        foreach (var refusal in state.ActionRefusals)
            if (refusal is null || !identities.Contains(refusal.AgentId) || string.IsNullOrWhiteSpace(refusal.CandidateId) ||
                refusal.CandidateId == "safe_idle" || refusal.CandidateId.Any(char.IsControl) ||
                refusal.RefusedTick < 1 || refusal.RefusedTick > state.Society.Society.WorldTick ||
                refusal.RetryAfterTick - refusal.RefusedTick != state.WorldSystems!.Config.TicksPerDay ||
                refusal.RetryAfterTick <= state.Society.Society.WorldTick ||
                string.IsNullOrEmpty(refusal.Check) || refusal.Check.Length > 96 ||
                refusal.Check.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '.')) ||
                !seen.Add((refusal.AgentId, refusal.CandidateId)))
                throw new InvalidDataException("The saved action-refusal boundary is invalid.");
        var recorded = state.Events.Where(item => item.Kind == "action_refused").Select(item =>
        {
            try
            {
                var refusal = JsonSerializer.Deserialize<WorldActionRefusal>(item.Detail);
                if (refusal is null || refusal.RefusedTick != item.WorldTick ||
                    item.Detail != JsonSerializer.Serialize(refusal) || !identities.Contains(refusal.AgentId) ||
                    string.IsNullOrWhiteSpace(refusal.CandidateId) || refusal.CandidateId == "safe_idle" ||
                    refusal.CandidateId.Any(char.IsControl) || refusal.RefusedTick < 1 ||
                    refusal.RetryAfterTick <= refusal.RefusedTick ||
                    refusal.RetryAfterTick - refusal.RefusedTick != state.WorldSystems!.Config.TicksPerDay ||
                    string.IsNullOrEmpty(refusal.Check) || refusal.Check.Length > 96 ||
                    refusal.Check.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '.')))
                    throw new InvalidDataException("The saved action-refusal event is invalid.");
                return refusal;
            }
            catch (JsonException exception) { throw new InvalidDataException("The saved action-refusal event is invalid.", exception); }
        }).ToArray();
        if (state.RefusedActionCount < recorded.Length || state.EventHistoryFloor == 0 && state.RefusedActionCount != recorded.Length ||
            recorded.Select(refusal => refusal.RefusedTick).Distinct().Count() != recorded.Length ||
            recorded.Any(refusal => refusal.RetryAfterTick > state.Society.Society.WorldTick && !state.ActionRefusals.Contains(refusal)) ||
            state.ActionRefusals.Any(refusal => state.Events.Count > 0 && refusal.RefusedTick >= state.Events[0].WorldTick &&
                !recorded.Contains(refusal)))
            throw new InvalidDataException("The saved action refusals do not match their accepted events.");
    }

    /// <summary>The host prepares exact checkpoint bytes before publishing a tick; fixtures default to strict failure.</summary>
    public ValueTask<WorldStepResult> AdvanceOneTickWithCheckpointAsync(
        Func<WorldRuntime, Action> prepareCheckpoint,
        Func<bool>? commitPermitted = null,
        Func<WorldRuntime, IReadOnlyList<WorldEventEntry>, IReadOnlyList<PreparedChildModelSelection>>? prepareChildModels = null,
        WorldTickFailurePolicy failurePolicy = WorldTickFailurePolicy.Strict,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prepareCheckpoint);
        return AdvanceOneTickCoreAsync(true, commitPermitted, prepareChildModels, cancellationToken,
            prepareCheckpoint, failurePolicy);
    }

    internal WorldRuntime CopyForChoiceTrial(Func<string, IDecisionProvider> providers) =>
        RestoreCore(ExportState(), providers, maxCognitionDispatchPerCycle, trustedPreparedState: true,
            applyLoadTransitions: false);

    // Probe the native action phase in the same trusted copy used by tick preparation.
    // Inline fixture replies keep unrelated background scheduling out of the invariant.
    internal async ValueTask<IReadOnlyList<SocietyCognitionDispatchResult>> ApplyChoiceTrialAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            EnqueueDueCognition(new HashSet<string>(StringComparer.Ordinal));
            var decisions = new List<SocietyCognitionDispatchResult>();
            for (var cycle = 0; cycle < agents.Count && society.Capture().Cognition.Queue.Count > 0; cycle++)
            {
                var dispatched = await society.DispatchCognitionAsync().ConfigureAwait(false);
                decisions.AddRange(dispatched.Decisions);
                foreach (var decision in dispatched.Decisions) ApplyDecision(decision);
            }
            CompleteActionPhase(decisions.Select(decision => decision.AgentId));
            return decisions;
        }
        finally { gate.Release(); }
    }

    /// <summary>Replay an accepted refusal alongside the same recorded decision inputs, without reproducing the defect.</summary>
    public ValueTask<WorldStepResult> AdvanceOneTickReplayingActionRefusalAsync(
        WorldEventEntry recordedRefusal, Func<WorldRuntime, Action> prepareCheckpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recordedRefusal);
        ArgumentNullException.ThrowIfNull(prepareCheckpoint);
        WorldActionRefusal refusal;
        try
        {
            refusal = JsonSerializer.Deserialize<WorldActionRefusal>(recordedRefusal.Detail)
                ?? throw new InvalidDataException("The replayed action refusal is missing.");
        }
        catch (JsonException exception) { throw new InvalidDataException("The replayed action refusal is invalid.", exception); }
        if (recordedRefusal.Kind != "action_refused" || recordedRefusal.WorldTick != refusal.RefusedTick ||
            recordedRefusal.Detail != JsonSerializer.Serialize(refusal))
            throw new InvalidDataException("The replayed action refusal is invalid.");
        return AdvanceOneTickCoreAsync(true, null, null, cancellationToken, prepareCheckpoint,
            WorldTickFailurePolicy.Strict, refusal);
    }

    private sealed record PreparedTick(WorldRuntime Runtime, WorldStepResult Result, Action? WriteCheckpoint,
        IReadOnlyList<PreparedIdentityMoment>? IdentityMoments = null) : IDisposable
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
        public bool IsCurrent => providers.All(pair => pair.Value.IsCurrent(factory?.Invoke(pair.Key)));
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
        public bool IsCurrent(IDecisionProvider? current) => inner.ProviderEpoch == epoch &&
            (current is null || current.ProviderEpoch == epoch);
        public DecisionProviderKind Kind => inner.Kind;
        public long ProviderEpoch => recording() ? epoch : inner.ProviderEpoch;
        public DecisionProviderKind KindFor(AgentObservation observation) => inner.KindFor(observation);
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
