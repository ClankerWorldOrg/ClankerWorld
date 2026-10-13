using ClankerWorld.Simulation.Cognition;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClankerWorld.Simulation.Society;

public sealed record SocietyCognitionScheduleEntry(
    string ScheduleId,
    string AgentId,
    int Priority,
    long EnqueuedTick,
    IReadOnlyList<string> TriggerIds,
    AgentObservation Observation);

public sealed record SocietyCognitionSchedulerEvent(
    long EventId,
    long WorldTick,
    string Kind,
    string Detail);

public sealed record SocietyCognitionDispatchResult(
    string AgentId,
    CognitionAdmissionResult Admission)
{
    [JsonIgnore]
    public World.ActionPlan? ActionPlan { get; init; }
    [JsonIgnore]
    public World.ActionRefusal? PlanRefusal { get; init; }
}

public sealed record SocietyDeferredCognitionRequest(
    string AgentId,
    CognitionDecisionRequest Request,
    Func<CancellationToken, ValueTask<CognitionDecisionResponse>> DecideAsync);

public sealed record SocietyCognitionSchedulerState(
    int SchemaVersion,
    int MaxQueueLength,
    int MaxDispatchPerCycle,
    IReadOnlyList<SocietyCognitionScheduleEntry> Queue,
    IReadOnlyList<CognitionRuntimeState> Runtimes,
    IReadOnlyList<SocietyCognitionSchedulerEvent> Events,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] long EventHistoryFloor = 0);

/// <summary>
/// Fair multi-agent cognition admission. Each agent has one
/// CognitionRuntime and therefore one in-flight request; the shared queue is
/// bounded and ordered by priority, age, agent ID, and schedule ID.
/// </summary>
public sealed class SocietyCognitionScheduler
{
    public const int StateSchemaVersion = 1;
    public const string NameRetryTriggerId = "agent_name_retry";

    private readonly Dictionary<string, CognitionRuntime> runtimes;
    private readonly List<SocietyCognitionScheduleEntry> queue = [];
    private readonly List<SocietyCognitionSchedulerEvent> events = [];
    private readonly int maxQueueLength;
    private readonly int maxDispatchPerCycle;
    private readonly Func<string, IDecisionProvider> providerFactory;
    private long nextEventId = 1;
    private long eventHistoryFloor;

    public SocietyCognitionScheduler(
        IEnumerable<SocietyAgent> agents,
        Func<string, IDecisionProvider>? providerFactory = null,
        int maxQueueLength = 64,
        int maxDispatchPerCycle = 8)
    {
        ArgumentNullException.ThrowIfNull(agents);
        if (maxQueueLength <= 0 || maxDispatchPerCycle <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxQueueLength));
        }

        this.maxQueueLength = maxQueueLength;
        this.maxDispatchPerCycle = maxDispatchPerCycle;
        this.providerFactory = providerFactory ?? (_ => new DeterministicDecisionProvider());
        runtimes = agents
            .Where(item => item.Status == SocietyAgentStatus.Active)
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(
                item => item.Id,
                item => new CognitionRuntime(item.Id, this.providerFactory(item.Id)),
                StringComparer.Ordinal);
    }

    public IReadOnlyList<string> AgentIds => runtimes.Keys.OrderBy(item => item, StringComparer.Ordinal).ToArray();

    public bool Enqueue(SocietyCognitionScheduleEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ValidateEntry(entry);
        if (!runtimes.ContainsKey(entry.AgentId))
        {
            throw new InvalidOperationException($"Unknown agent '{entry.AgentId}'.");
        }

        var existing = queue.SingleOrDefault(item => item.AgentId == entry.AgentId);
        if (existing is not null)
        {
            var merged = existing with
            {
                Priority = Math.Max(existing.Priority, entry.Priority),
                EnqueuedTick = Math.Min(existing.EnqueuedTick, entry.EnqueuedTick),
                TriggerIds = existing.TriggerIds.Concat(entry.TriggerIds)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                // A naming retry can reuse the completed request's older
                // observation while adding its trigger to newer queued work.
                Observation = LatestObservation(existing.Observation, entry.Observation),
            };
            queue[queue.IndexOf(existing)] = merged;
            AppendEvent(entry.EnqueuedTick, "cognition_trigger_coalesced", entry.AgentId);
            return true;
        }

        if (queue.Count >= maxQueueLength)
        {
            AppendEvent(entry.EnqueuedTick, "cognition_backpressure", entry.ScheduleId);
            return false;
        }

        queue.Add(entry);
        AppendEvent(entry.EnqueuedTick, "cognition_queued", entry.ScheduleId);
        return true;
    }

    private static AgentObservation LatestObservation(AgentObservation previous, AgentObservation incoming) =>
        (incoming.RunEpoch, incoming.DecisionGeneration, incoming.WorldTick)
            .CompareTo((previous.RunEpoch, previous.DecisionGeneration, previous.WorldTick)) >= 0
            ? incoming : previous;

    /// <summary>
    /// Reconciles cognition runtimes with the authoritative lifecycle set.
    /// Dead agents cannot receive new work, and newborns get a fresh
    /// provider binding without disturbing surviving runtimes or queued work.
    /// </summary>
    public void SyncAgents(IEnumerable<SocietyAgent> agents, IReadOnlySet<string>? bornChildIds = null)
    {
        ArgumentNullException.ThrowIfNull(agents);
        var current = agents.ToArray();
        var activeIds = current
            .Where(item => item.Status == SocietyAgentStatus.Active)
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var removedId in runtimes.Keys.Where(id => !activeIds.Contains(id)).ToArray())
        {
            runtimes.Remove(removedId);
            queue.RemoveAll(entry => entry.AgentId == removedId);
            AppendEvent(0, "cognition_runtime_removed", removedId);
        }

        foreach (var agent in current
                     .Where(item => item.Status == SocietyAgentStatus.Active)
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (runtimes.ContainsKey(agent.Id))
            {
                continue;
            }

            runtimes.Add(
                agent.Id,
                new CognitionRuntime(agent.Id, providerFactory(agent.Id)));
            AppendEvent(0, "cognition_runtime_added", agent.Id);
        }

        // A save from before child-provider routing can contain an already
        // queued observation. Reconcile it before hosted preview or dispatch.
        var infants = current.Where(item => item.AgeBand == SocietyAgeBand.Infant)
            .Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        queue.RemoveAll(entry => infants.Contains(entry.AgentId));
        var named = current.Where(item => !item.NeedsName).Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        for (var index = queue.Count - 1; index >= 0; index--)
        {
            var entry = queue[index];
            // A completed naming call may leave newer ordinary work queued,
            // including after its consumed retry trigger has been removed.
            if (!named.Contains(entry.AgentId) || !entry.Observation.NeedsName &&
                !entry.TriggerIds.Contains(NameRetryTriggerId, StringComparer.Ordinal))
                continue;
            var remaining = entry.TriggerIds.Where(trigger => trigger != NameRetryTriggerId).ToArray();
            if (remaining.Length == 0) queue.RemoveAt(index);
            else queue[index] = entry with
            {
                TriggerIds = remaining,
                Observation = entry.Observation with { NeedsName = false },
            };
        }
        if (bornChildIds is not null)
        {
            for (var index = 0; index < queue.Count; index++)
                if (bornChildIds.Contains(queue[index].AgentId))
                    queue[index] = queue[index] with
                    {
                        Observation = queue[index].Observation with { RequiresPersonalProvider = true },
                    };
        }
    }

    public async ValueTask<IReadOnlyList<SocietyCognitionDispatchResult>> DispatchAsync(
        CancellationToken cancellationToken = default) =>
        await DispatchEligibleAsync(_ => true, cancellationToken).ConfigureAwait(false);

    public ValueTask<IReadOnlyList<SocietyCognitionDispatchResult>> DispatchDeterministicAsync(
        CancellationToken cancellationToken = default) =>
        DispatchDeterministicAsync(null, null, cancellationToken);

    internal ValueTask<IReadOnlyList<SocietyCognitionDispatchResult>> DispatchDeterministicAsync(
        IReadOnlySet<string>? excludedAgentIds, Func<AgentObservation, bool>? observationIsCurrent,
        CancellationToken cancellationToken) =>
        DispatchEligibleAsync(entry => excludedAgentIds?.Contains(entry.AgentId) != true &&
            (observationIsCurrent?.Invoke(entry.Observation) ?? true) &&
            runtimes[entry.AgentId].ProviderKindFor(entry.Observation) ==
            DecisionProviderKind.Deterministic, cancellationToken);

    public IReadOnlyList<SocietyDeferredCognitionRequest> PreviewModelRequests(
        IReadOnlySet<string> excludedAgentIds) =>
        PreviewModelRequests(excludedAgentIds, null);

    internal IReadOnlyList<SocietyDeferredCognitionRequest> PreviewModelRequests(
        IReadOnlySet<string> excludedAgentIds, Func<AgentObservation, bool>? observationIsCurrent)
    {
        ArgumentNullException.ThrowIfNull(excludedAgentIds);
        var selected = OrderedQueue()
            .Where(entry => !excludedAgentIds.Contains(entry.AgentId) &&
                (observationIsCurrent?.Invoke(entry.Observation) ?? true) &&
                runtimes[entry.AgentId].ProviderKindFor(ObservationFor(entry)) != DecisionProviderKind.Deterministic)
            .Take(maxDispatchPerCycle)
            .ToArray();
        var previews = new List<SocietyDeferredCognitionRequest>(selected.Length);
        foreach (var entry in selected)
        {
            var runtime = runtimes[entry.AgentId];
            try
            {
                var request = runtime.PreviewRequest(ObservationFor(entry));
                previews.Add(new SocietyDeferredCognitionRequest(
                    entry.AgentId,
                    request,
                    token => runtime.DecidePreviewAsync(request, token)));
            }
            catch (InvalidOperationException)
            {
                // The durable queue may outlive a pause/resume boundary. A
                // later observation will replace the stale decision point.
            }
        }
        return previews;
    }

    public IReadOnlySet<string> PendingModelAgentIds() => queue
        .Where(entry => runtimes[entry.AgentId].ProviderKindFor(ObservationFor(entry)) != DecisionProviderKind.Deterministic)
        .Select(entry => entry.AgentId)
        .ToHashSet(StringComparer.Ordinal);

    public long CurrentProviderEpoch(string agentId) => GetRuntime(agentId).ProviderEpoch;

    internal void ClearCurrentIntention(string agentId, long worldTick) =>
        GetRuntime(agentId).ClearCurrentIntention(worldTick);

    internal void CompleteQueuedIdentityChoice(string agentId, string name, string personality, string aspiration)
    {
        var index = queue.FindIndex(entry => entry.AgentId == agentId);
        if (index < 0) return;
        var entry = queue[index];
        var self = entry.Observation.Self is { } previousSelf
            ? previousSelf with { Name = name, Personality = personality, Aspiration = aspiration, FamilyBackground = null }
            : null;
        if (!entry.Observation.NeedsPersonality && !entry.Observation.NeedsAspiration &&
            entry.Observation.Self == self)
            return;
        var identity = JsonSerializer.Serialize(new[]
        {
            "clankerworld.queued-identity/v1", entry.Observation.ObservationDigest, name, personality, aspiration,
        });
        queue[index] = entry with
        {
            Observation = entry.Observation with
            {
                NeedsPersonality = false,
                NeedsAspiration = false,
                Self = self,
                ObservationDigest = $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))}",
            },
        };
    }

    public SocietyCognitionDispatchResult? CompleteDeferred(
        CognitionDecisionRequest originalRequest,
        CognitionDecisionResponse? response,
        string? failure,
        long currentRunEpoch,
        IReadOnlySet<string> legalCandidateIds,
        bool? decisionContextChanged = null)
    {
        ArgumentNullException.ThrowIfNull(originalRequest);
        var agentId = originalRequest.Observation.AgentId;
        var entry = queue.SingleOrDefault(item => item.AgentId == agentId);
        if (entry is null || !runtimes.TryGetValue(agentId, out var runtime) ||
            originalRequest.Observation.RunEpoch != currentRunEpoch ||
            originalRequest.ProviderEpoch != runtime.ProviderEpoch)
            return null;
        var currentRequest = runtime.PreviewRequest(ObservationFor(entry));
        if (currentRequest.RequestId != originalRequest.RequestId ||
            currentRequest.Observation.IsNameRetry != originalRequest.Observation.IsNameRetry ||
            !string.Equals(
                currentRequest.Observation.ConversationChoiceContext,
                originalRequest.Observation.ConversationChoiceContext,
                StringComparison.Ordinal) ||
            currentRequest.Observation.RunEpoch != currentRunEpoch ||
            response is not null &&
                originalRequest.Observation.Candidates.Any(candidate => candidate.Id == response.SelectedCandidateId) &&
                !legalCandidateIds.Contains(response.SelectedCandidateId))
            return null;

        var issued = runtime.IssueRequest(originalRequest.Observation);
        CognitionAdmissionResult admission = response is null
            ? runtime.FailRequest(issued.RequestId, failure ?? "provider_failure")
            : runtime.ApplyResponse(response);
        // Routine refreshes change ticks, hunger and digests while a reply is
        // held. Only a changed decision context or new guidance needs another
        // call. The host can supply its existing context policy; standalone
        // scheduler callers retain newly offered candidate identities, not
        // choices that have only disappeared or changed order.
        var hasNewChoices = decisionContextChanged ??
            !originalRequest.Observation.Candidates.Select(candidate => candidate.Id)
                .ToHashSet(StringComparer.Ordinal)
                .IsSupersetOf(currentRequest.Observation.Candidates.Select(candidate => candidate.Id));
        var hasNewGuidance = (currentRequest.Observation.ObserverGuidance ?? [])
            .Any(message => !(originalRequest.Observation.ObserverGuidance ?? []).Contains(message));
        if (!hasNewChoices && !hasNewGuidance)
            queue.Remove(entry);
        else if (originalRequest.Observation.IsNameRetry)
        {
            var remaining = entry.TriggerIds.Where(trigger => trigger != NameRetryTriggerId).ToArray();
            if (remaining.Length == 0) queue.Remove(entry);
            else queue[queue.IndexOf(entry)] = entry with { TriggerIds = remaining };
        }
        AppendEvent(entry.Observation.WorldTick,
            admission.Accepted ? "cognition_dispatched" : "cognition_dispatch_rejected",
            $"{agentId}:{admission.Outcome}");
        return new SocietyCognitionDispatchResult(agentId, admission)
        {
            ActionPlan = originalRequest.Observation.Candidates.SingleOrDefault(candidate =>
                candidate.Id == admission.Intention?.CandidateId)?.ActionPlan,
        };
    }

    private IOrderedEnumerable<SocietyCognitionScheduleEntry> OrderedQueue() => queue
        .OrderByDescending(item => item.Priority)
        .ThenBy(item => item.EnqueuedTick)
        .ThenBy(item => item.AgentId, StringComparer.Ordinal)
        .ThenBy(item => item.ScheduleId, StringComparer.Ordinal);

    private async ValueTask<IReadOnlyList<SocietyCognitionDispatchResult>> DispatchEligibleAsync(
        Func<SocietyCognitionScheduleEntry, bool> predicate,
        CancellationToken cancellationToken)
    {
        var selected = OrderedQueue()
            .Where(entry => predicate(entry with { Observation = ObservationFor(entry) }))
            .Take(maxDispatchPerCycle)
            .ToArray();

        async Task<SocietyCognitionDispatchResult> DispatchOneAsync(SocietyCognitionScheduleEntry entry)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var runtime = runtimes[entry.AgentId];
            var observation = ObservationFor(entry);
            CognitionAdmissionResult admission;
            try
            {
                admission = await runtime.RequestAndDecideAsync(observation, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException exception)
            {
                admission = new CognitionAdmissionResult(false, false, exception.Message, null);
            }

            if (string.Equals(admission.Outcome, "provider_cancelled", StringComparison.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return new SocietyCognitionDispatchResult(entry.AgentId, admission)
            {
                ActionPlan = observation.Candidates.SingleOrDefault(candidate =>
                    candidate.Id == admission.Intention?.CandidateId)?.ActionPlan,
            };
        }

        // Each agent owns an independent cognition runtime, so the
        // bounded selection may call model providers concurrently. Results
        // are still committed in the deterministic scheduler order below.
        // If cancellation interrupts the batch, every selected queue entry
        // remains durable for a later retry.
        var dispatched = await Task.WhenAll(selected.Select(DispatchOneAsync)).ConfigureAwait(false);
        var results = new List<SocietyCognitionDispatchResult>(selected.Length);
        for (var index = 0; index < selected.Length; index++)
        {
            var entry = selected[index];
            var result = dispatched[index];
            queue.Remove(entry);
            results.Add(result);
            AppendEvent(
                entry.Observation.WorldTick,
                result.Admission.Accepted ? "cognition_dispatched" : "cognition_dispatch_rejected",
                $"{entry.AgentId}:{result.Admission.Outcome}");
        }

        return results;
    }

    private static AgentObservation ObservationFor(SocietyCognitionScheduleEntry entry)
    {
        if (!entry.TriggerIds.Contains(NameRetryTriggerId, StringComparer.Ordinal))
            return entry.Observation with { IsNameRetry = false };

        var retryDigest = $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{entry.Observation.ObservationDigest}\nname_retry=true")))}";
        return entry.Observation with { IsNameRetry = true, ObservationDigest = retryDigest };
    }

    public CognitionRuntimeSnapshot CaptureRuntime(string agentId) =>
        GetRuntime(agentId).Capture();

    public SocietyCognitionSchedulerState ExportState()
    {
        var runtimeStates = runtimes.Values
            .OrderBy(runtime => runtime.AgentId, StringComparer.Ordinal)
            .Select(runtime => runtime.ExportState())
            .ToArray();
        return new SocietyCognitionSchedulerState(
            StateSchemaVersion,
            maxQueueLength,
            maxDispatchPerCycle,
            queue.OrderBy(item => item.ScheduleId, StringComparer.Ordinal).ToArray(),
            runtimeStates,
            events.ToArray(), eventHistoryFloor);
    }

    public static SocietyCognitionScheduler Restore(
        SocietyCognitionSchedulerState state,
        Func<string, IDecisionProvider>? providerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SchemaVersion != StateSchemaVersion ||
            state.MaxQueueLength <= 0 || state.MaxDispatchPerCycle <= 0)
        {
            throw new InvalidDataException("The society cognition scheduler state is invalid.");
        }

        var agents = state.Runtimes
            .Select(item => new SocietyAgent(
                item.AgentId,
                item.AgentId,
                0,
                SocietyAgentStatus.Active,
                SocietyAgeBand.Adult,
                10_000,
                null,
                null,
                SocietyWorkRole.Unassigned,
                0))
            .ToArray();
        var scheduler = new SocietyCognitionScheduler(
            agents,
            providerFactory,
            state.MaxQueueLength,
            state.MaxDispatchPerCycle);
        scheduler.queue.AddRange(state.Queue);
        foreach (var runtimeState in state.Runtimes)
        {
            if (!scheduler.runtimes.ContainsKey(runtimeState.AgentId))
            {
                throw new InvalidDataException("A scheduler runtime has no matching agent.");
            }

            scheduler.runtimes[runtimeState.AgentId] = CognitionRuntime.Restore(
                runtimeState,
                providerFactory?.Invoke(runtimeState.AgentId));
        }

        scheduler.events.AddRange(state.Events);
        scheduler.eventHistoryFloor = state.EventHistoryFloor;
        scheduler.nextEventId = checked(scheduler.eventHistoryFloor + scheduler.events.Count + 1L);
        scheduler.Validate();
        return scheduler;
    }

    public void Validate()
    {
        if (queue.Count > maxQueueLength || eventHistoryFloor < 0)
        {
            throw new InvalidDataException("The society cognition queue exceeds its configured limit.");
        }

        var expected = checked(eventHistoryFloor + 1);
        var previousTick = 0L;
        foreach (var schedulerEvent in events)
        {
            if (schedulerEvent.EventId != expected || schedulerEvent.WorldTick < previousTick)
            {
                throw new InvalidDataException("Society cognition events are not ordered.");
            }

            expected++;
            previousTick = schedulerEvent.WorldTick;
        }

        foreach (var entry in queue)
        {
            ValidateEntry(entry);
            if (!runtimes.ContainsKey(entry.AgentId))
            {
                throw new InvalidDataException("A queued cognition entry references an unknown agent.");
            }
        }
    }

    private CognitionRuntime GetRuntime(string agentId)
    {
        var id = string.IsNullOrWhiteSpace(agentId) ? agentId : agentId.Trim();
        return runtimes.TryGetValue(id, out var runtime)
            ? runtime
            : throw new KeyNotFoundException($"Unknown agent '{id}'.");
    }

    private void AppendEvent(long worldTick, string kind, string detail)
    {
        var committedTick = events.Count == 0
            ? worldTick
            : Math.Max(worldTick, events[^1].WorldTick);
        events.Add(new SocietyCognitionSchedulerEvent(
            checked(nextEventId++),
            committedTick,
            kind,
            detail));
    }

    private static void ValidateEntry(SocietyCognitionScheduleEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.ScheduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.AgentId);
        if (entry.Priority < 0 || entry.EnqueuedTick < 0 || entry.TriggerIds.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entry));
        }

        entry.Observation.Validate();
        if (entry.Observation.AgentId != entry.AgentId ||
            entry.TriggerIds.Any(string.IsNullOrWhiteSpace) ||
            entry.TriggerIds.Distinct(StringComparer.Ordinal).Count() != entry.TriggerIds.Count)
        {
            throw new InvalidDataException("A cognition schedule entry has invalid identity or triggers.");
        }
    }
}
