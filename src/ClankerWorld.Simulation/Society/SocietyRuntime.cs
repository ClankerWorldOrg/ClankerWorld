using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.Society;

public sealed record SocietyWorldRuntimeState(
    int SchemaVersion,
    SocietyCheckpoint Society,
    SocietyCognitionSchedulerState Cognition);

public sealed record SocietyWorldCapture(
    SocietyCheckpoint Society,
    SocietyCognitionSchedulerState Cognition);

public sealed record SocietyDispatchCycleResult(
    SocietyCheckpoint Society,
    IReadOnlyList<SocietyCognitionDispatchResult> Decisions);

/// <summary>
/// The Phase 4 composition root. Society is the authoritative state boundary;
/// cognition is a derived, bounded service that is reconciled after every
/// lifecycle mutation. Saving this record captures both together without
/// allowing a provider or client to mutate society directly.
/// </summary>
public sealed class SocietyRuntime : IDisposable
{
    public const int StateSchemaVersion = 4;

    private readonly SemaphoreSlim gate = new(1, 1);
    private SocietyCheckpoint society;
    private SocietyCognitionScheduler cognition;

    public SocietyRuntime(
        SocietyCheckpoint checkpoint,
        Func<string, IDecisionProvider>? providerFactory = null,
        int maxCognitionQueueLength = 64,
        int maxCognitionDispatchPerCycle = 8)
    {
        SocietyRules.Validate(checkpoint);
        society = checkpoint;
        cognition = new SocietyCognitionScheduler(
            checkpoint.Agents,
            providerFactory,
            maxCognitionQueueLength,
            maxCognitionDispatchPerCycle);
    }

    public SocietyCheckpoint Checkpoint => society;

    public SocietyWorldCapture Capture()
    {
        gate.Wait();
        try
        {
            return new SocietyWorldCapture(society, cognition.ExportState());
        }
        finally
        {
            gate.Release();
        }
    }

    public SocietyWorldRuntimeState ExportState()
    {
        var capture = Capture();
        return new SocietyWorldRuntimeState(StateSchemaVersion, capture.Society, capture.Cognition);
    }

    public static SocietyRuntime Restore(
        SocietyWorldRuntimeState state,
        Func<string, IDecisionProvider>? providerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SchemaVersion != StateSchemaVersion)
        {
            throw new InvalidDataException("The Phase 4 runtime state schema is unsupported.");
        }

        SocietyRules.Validate(state.Society);
        var scheduler = SocietyCognitionScheduler.Restore(
            state.Cognition,
            providerFactory);
        scheduler.SyncAgents(state.Society.Agents,
            state.Society.Births.Select(birth => birth.ChildId).ToHashSet(StringComparer.Ordinal));
        var runtime = new SocietyRuntime(
            state.Society,
            providerFactory,
            state.Cognition.MaxQueueLength,
            state.Cognition.MaxDispatchPerCycle)
        {
            cognition = scheduler,
        };
        runtime.Validate();
        return runtime;
    }

    public SocietyOperationResult Apply(Func<SocietyCheckpoint, SocietyOperationResult> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        gate.Wait();
        try
        {
            var result = operation(society);
            SocietyRules.Validate(result.Checkpoint);
            society = result.Checkpoint;
            cognition.SyncAgents(society.Agents,
                society.Births.Select(birth => birth.ChildId).ToHashSet(StringComparer.Ordinal));
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public SocietyOperationResult AdvanceTo(long targetTick, IReadOnlyList<SocietyTownStore>? townStores = null,
        IReadOnlyList<SocietyDefaultEstateDivision>? defaultEstates = null) =>
        Apply(checkpoint => SocietyRules.AdvanceTo(checkpoint, targetTick, townStores, defaultEstates));

    public SocietyOperationResult Pause() => Apply(SocietyRules.Pause);

    public SocietyOperationResult Resume() => Apply(SocietyRules.Resume);

    public bool EnqueueCognition(SocietyCognitionScheduleEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        gate.Wait();
        try
        {
            var agent = society.GetAgent(entry.AgentId);
            if (agent.Status != SocietyAgentStatus.Active)
            {
                throw new InvalidOperationException("Dead agents cannot receive cognition work.");
            }

            if (agent.AgeBand == SocietyAgeBand.Infant)
            {
                return false;
            }

            if (entry.Observation.WorldTick > society.WorldTick)
            {
                throw new InvalidOperationException("Cognition cannot observe beyond the authoritative world tick.");
            }

            var child = society.Births.Any(birth => birth.ChildId == agent.Id);
            return cognition.Enqueue(child
                ? entry with { Observation = entry.Observation with { RequiresPersonalProvider = true } }
                : entry);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<SocietyDispatchCycleResult> DispatchCognitionAsync(
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var decisions = await cognition.DispatchAsync(cancellationToken).ConfigureAwait(false);
            return new SocietyDispatchCycleResult(society, decisions);
        }
        finally
        {
            gate.Release();
        }
    }

    public ValueTask<SocietyDispatchCycleResult> DispatchDeterministicCognitionAsync(
        CancellationToken cancellationToken = default) =>
        DispatchDeterministicCognitionAsync(null, null, cancellationToken);

    internal async ValueTask<SocietyDispatchCycleResult> DispatchDeterministicCognitionAsync(
        IReadOnlySet<string>? excludedAgentIds, Func<AgentObservation, bool>? observationIsCurrent,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return new SocietyDispatchCycleResult(society,
                await cognition.DispatchDeterministicAsync(excludedAgentIds, observationIsCurrent,
                    cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            gate.Release();
        }
    }

    public IReadOnlyList<SocietyDeferredCognitionRequest> PreviewModelRequests(IReadOnlySet<string> excludedIds) =>
        PreviewModelRequests(excludedIds, null);

    internal IReadOnlyList<SocietyDeferredCognitionRequest> PreviewModelRequests(
        IReadOnlySet<string> excludedIds, Func<AgentObservation, bool>? observationIsCurrent)
    {
        gate.Wait();
        try { return cognition.PreviewModelRequests(excludedIds, observationIsCurrent); }
        finally { gate.Release(); }
    }

    public IReadOnlySet<string> PendingModelAgentIds()
    {
        gate.Wait();
        try { return cognition.PendingModelAgentIds(); }
        finally { gate.Release(); }
    }

    public long CurrentProviderEpoch(string agentId)
    {
        gate.Wait();
        try { return cognition.CurrentProviderEpoch(agentId); }
        finally { gate.Release(); }
    }

    internal void ClearCurrentIntention(string agentId, long worldTick)
    {
        gate.Wait();
        try { cognition.ClearCurrentIntention(agentId, worldTick); }
        finally { gate.Release(); }
    }

    public SocietyCognitionDispatchResult? CompleteDeferredCognition(
        CognitionDecisionRequest request, CognitionDecisionResponse? response, string? failure,
        IReadOnlySet<string> legalCandidateIds, bool? decisionContextChanged = null)
    {
        gate.Wait();
        try { return cognition.CompleteDeferred(request, response, failure, society.RunEpoch, legalCandidateIds, decisionContextChanged); }
        finally { gate.Release(); }
    }

    internal void CompleteQueuedIdentityChoice(string agentId, string personality, string aspiration)
    {
        gate.Wait();
        try { cognition.CompleteQueuedIdentityChoice(agentId, society.GetAgent(agentId).Name, personality, aspiration); }
        finally { gate.Release(); }
    }

    public void Validate()
    {
        SocietyRules.Validate(society);
        cognition.Validate();
        var activeIds = society.Agents
            .Where(item => item.Status == SocietyAgentStatus.Active)
            .Select(item => item.Id)
            .OrderBy(item => item, StringComparer.Ordinal);
        if (!activeIds.SequenceEqual(cognition.AgentIds.OrderBy(item => item, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("Phase 4 society and cognition runtime populations disagree.");
        }
    }

    public void Dispose() => gate.Dispose();
}
