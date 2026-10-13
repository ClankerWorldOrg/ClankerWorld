using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static readonly TimeSpan IdentityMomentTimeout = TimeSpan.FromSeconds(15);
    private readonly Dictionary<string, PendingIdentityMoment> pendingIdentityMoments = new(StringComparer.Ordinal);
    private sealed record IdentityMomentOutcome(CognitionDecisionResponse? Response);
    private sealed record PendingIdentityMoment(AgentIdentityMomentKind Kind,
        CognitionDecisionRequest Request, Task<IdentityMomentOutcome> Task, CancellationTokenSource Cancellation);

    private void DiscoverIdentityMoments()
    {
        var checkpoint = society.Checkpoint;
        foreach (var person in checkpoint.Agents.Where(item => item.Status == SocietyAgentStatus.Active))
        {
            var physical = agents[person.Id];
            var kinds = new List<AgentIdentityMomentKind>();
            var age = checkpoint.AgeAt(person, WorldTick);
            if (checkpoint.Config.DayLifecycle is { } days && age >= days.MaximumDay / 2)
                kinds.Add(AgentIdentityMomentKind.Midlife);
            if (person.AgeBand == SocietyAgeBand.Elder)
                kinds.Add(AgentIdentityMomentKind.Elder);
            if (checkpoint.Relationships.Any(edge => edge.Type == SocietyRelationshipType.BiologicalParentage &&
                edge.ProposerId == person.Id))
                kinds.Add(AgentIdentityMomentKind.Parenthood);
            if (checkpoint.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Partnership &&
                edge.State == SocietyRelationshipState.EndedByDeath &&
                (edge.ProposerId == person.Id || edge.TargetId == person.Id)))
                kinds.Add(AgentIdentityMomentKind.PartnerLoss);
            if (checkpoint.Relationships.Any(edge => edge.Type == SocietyRelationshipType.BiologicalParentage &&
                edge.TargetId == person.Id && checkpoint.GetAgent(edge.ProposerId).Status == SocietyAgentStatus.Dead))
                kinds.Add(AgentIdentityMomentKind.ParentLoss);
            var existing = physical.IdentityMoments ?? [];
            var added = kinds.Where(kind => !existing.Any(moment => moment.Kind == kind))
                .Select(kind => new AgentIdentityMoment(kind, WorldTick)).ToArray();
            if (added.Length == 0) continue;
            agents[person.Id] = physical with { IdentityMoments = existing.Concat(added).ToArray() };
            checkpointSchemaVersion = StateSchemaVersion;
        }
    }

    private static bool HasWaitingIdentityMoment(AgentState physical) =>
        !physical.IdentityChoicePending && (physical.IdentityMoments ?? [])
            .Any(moment => moment.Outcome is "waiting" or "requested");

    private sealed record PreparedIdentityMoment(AgentIdentityMomentKind Kind,
        CognitionDecisionRequest Request, IDecisionProvider Provider);

    private void StartIdentityMoments() => PrepareIdentityMoments(StartPreparedIdentityMoment);

    private void PrepareIdentityMoments(Action<PreparedIdentityMoment> prepare,
        IReadOnlySet<string>? activeModelCallIds = null, IReadOnlySet<string>? activeIdentityIds = null)
    {
        // Bounded separately from ordinary decisions. The installation usage
        // store still reserves each potentially paid request before HTTP.
        var capacity = Math.Max(0, maxCognitionDispatchPerCycle - (activeIdentityIds?.Count ?? pendingIdentityMoments.Count));
        foreach (var person in society.Checkpoint.Agents
            .Where(item => item.Status == SocietyAgentStatus.Active && item.AgeBand != SocietyAgeBand.Infant)
            .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (capacity == 0) break;
            var physical = agents[person.Id];
            if (physical.IdentityChoicePending || (activeIdentityIds?.Contains(person.Id) ?? pendingIdentityMoments.ContainsKey(person.Id)) ||
                (activeModelCallIds?.Contains(person.Id) ?? pendingModelCalls.ContainsKey(person.Id)) || IsConversationBusy(person.Id) ||
                NeedsUrgentFood(physical) || NeedsUrgentWarmth(physical)) continue;
            var moment = (physical.IdentityMoments ?? []).FirstOrDefault(item => item.Outcome == "waiting");
            if (moment is null) continue;
            try
            {
                var observation = CreateIdentityMomentObservation(person, physical, moment);
                var provider = providerFactory?.Invoke(person.Id) ?? new DeterministicDecisionProvider();
                if (provider.KindFor(observation) != DecisionProviderKind.LargeLanguageModel)
                {
                    FinishIdentityMoment(person.Id, moment.Kind, "kept");
                    continue;
                }
                var request = new CognitionDecisionRequest(
                    $"identity:{Guid.NewGuid():N}", provider.ProviderEpoch, observation);
                request.Validate();
                SetIdentityMoment(person.Id, moment with { Outcome = "requested", RequestedTick = WorldTick });
                AppendEvent(new AgentIdentityMomentStartedEventPayload(person.Id, Convert.ToString(moment.Kind, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));
                prepare(new(moment.Kind, request, provider));
                capacity--;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                FinishIdentityMoment(person.Id, moment.Kind, "kept");
            }
        }
    }

    private void StartPreparedIdentityMoment(PreparedIdentityMoment prepared)
    {
        var cancellation = new CancellationTokenSource(IdentityMomentTimeout);
        var task = Task.Run(async () =>
        {
            try
            {
                cancellation.Token.ThrowIfCancellationRequested();
                return new IdentityMomentOutcome(await prepared.Provider.DecideAsync(prepared.Request, cancellation.Token)
                    .AsTask().WaitAsync(cancellation.Token).ConfigureAwait(false));
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // Neither provider replies nor their exception messages enter events or logs.
                return new IdentityMomentOutcome(null);
            }
        });
        pendingIdentityMoments.Add(prepared.Request.Observation.AgentId,
            new(prepared.Kind, prepared.Request, task, cancellation));
    }

    private AgentObservation CreateIdentityMomentObservation(
        SocietyAgent person, AgentState physical, AgentIdentityMoment moment)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{person.Id}|{moment.Kind}|{moment.TriggeredTick}|{physical.Personality}|{physical.Aspiration}")));
        return new AgentObservation(person.Id, WorldTick, society.Checkpoint.RunEpoch,
            moment.TriggeredTick, digest, physical.FullnessBasisPoints,
            [new("identity_optional", "Keep your current identity or choose an optional change for this life moment.")],
            RequiresPersonalProvider: true,
            Self: new(person.Id, person.Name, person.AgeBand.ToString(), physical.Personality, physical.Aspiration,
                person.HouseholdId, null, null, null),
            NeedsPersonality: true, NeedsAspiration: true)
        { IdentityMoment = moment.Reason };
    }

    private void CompleteIdentityMoments(IReadOnlyList<PendingIdentityMoment> completed,
        Func<PendingIdentityMoment, bool> providerIsCurrent)
    {
        foreach (var pending in completed)
        {
            var id = pending.Request.Observation.AgentId;
            if (!agents.TryGetValue(id, out var physical) ||
                society.Checkpoint.GetAgent(id).Status != SocietyAgentStatus.Active ||
                !(physical.IdentityMoments ?? []).Any(moment => moment.Kind == pending.Kind && moment.Outcome == "requested"))
                continue;
            if (society.Checkpoint.RunEpoch != pending.Request.Observation.RunEpoch ||
                physical.Personality != pending.Request.Observation.Self!.Personality ||
                physical.Aspiration != pending.Request.Observation.Self.Aspiration || !providerIsCurrent(pending))
            {
                FinishIdentityMoment(id, pending.Kind, "interrupted");
                continue;
            }
            var response = pending.Task.GetAwaiter().GetResult().Response;
            var valid = false;
            try
            {
                response?.Validate();
                valid = response is not null && response.RequestId == pending.Request.RequestId &&
                    response.AgentId == id && response.Provider == DecisionProviderKind.LargeLanguageModel &&
                    response.ProviderEpoch == pending.Request.ProviderEpoch &&
                    response.RunEpoch == pending.Request.Observation.RunEpoch &&
                    response.DecisionGeneration == pending.Request.Observation.DecisionGeneration &&
                    response.ObservationDigest == pending.Request.Observation.ObservationDigest &&
                    response.SelectedCandidateId == "identity_optional";
            }
            catch (Exception exception) when (exception is not OutOfMemoryException) { }
            var personality = valid ? response!.ChosenPersonality : null;
            var aspiration = valid ? response!.ChosenAspiration : null;
            if (personality == physical.Personality) personality = null;
            if (aspiration == physical.Aspiration) aspiration = null;
            var accepted = personality is not null || aspiration is not null;
            agents[id] = physical with
            {
                Personality = personality ?? physical.Personality,
                Aspiration = aspiration ?? physical.Aspiration,
                LastDecisionContext = accepted ? null : physical.LastDecisionContext,
            };
            FinishIdentityMoment(id, pending.Kind, accepted ? "accepted" : "kept", personality, aspiration);
        }
    }

    private bool IsIdentityMomentProviderCurrent(PendingIdentityMoment pending)
    {
        try
        {
            var provider = providerFactory?.Invoke(pending.Request.Observation.AgentId);
            return provider is not null && provider.KindFor(pending.Request.Observation) == DecisionProviderKind.LargeLanguageModel &&
                provider.ProviderEpoch == pending.Request.ProviderEpoch;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException) { return false; }
    }

    private void FinishIdentityMoment(string id, AgentIdentityMomentKind kind, string outcome,
        string? personality = null, string? aspiration = null)
    {
        var moment = (agents[id].IdentityMoments ?? []).Single(item => item.Kind == kind);
        SetIdentityMoment(id, moment with
        {
            Outcome = outcome,
            CompletedTick = WorldTick,
            Personality = personality,
            Aspiration = aspiration,
        });
        AppendEvent(outcome == "accepted" ? new AgentIdentityRevisedEventPayload(id, kind.ToString()) : new AgentIdentityMomentKeptEventPayload(id, kind.ToString()));
    }

    private void SetIdentityMoment(string id, AgentIdentityMoment moment)
    {
        var physical = agents[id];
        agents[id] = physical with
        {
            IdentityMoments = (physical.IdentityMoments ?? []).Select(item => item.Kind == moment.Kind ? moment : item).ToArray(),
        };
        checkpointSchemaVersion = StateSchemaVersion;
    }

    private void CancelIdentityMoments(bool underRuntimeGate = true)
    {
        foreach (var id in pendingIdentityMoments.Keys.ToArray()) CancelIdentityMoment(id, underRuntimeGate);
    }

    private void CancelIdentityMoment(string id, bool underRuntimeGate = true)
    {
        if (!pendingIdentityMoments.Remove(id, out var pending)) return;
        if (agents.TryGetValue(id, out var physical) &&
            (physical.IdentityMoments ?? []).Any(moment => moment.Kind == pending.Kind && moment.Outcome == "requested"))
            FinishIdentityMoment(id, pending.Kind, "interrupted");
        CancelProviderCall(pending.Cancellation, underRuntimeGate);
        _ = pending.Task.ContinueWith(_ => pending.Cancellation.Dispose(), TaskScheduler.Default);
    }

    private void ReconcileIdentityMoments()
    {
        foreach (var (id, pending) in pendingIdentityMoments.ToArray())
            if (!agents.ContainsKey(id) || society.Checkpoint.RunEpoch != pending.Request.Observation.RunEpoch ||
                !IsIdentityMomentProviderCurrent(pending))
                CancelIdentityMoment(id);
        // A saved request has no live provider task after restore. Its single
        // opportunity remains consumed; returning to the game never retries it.
        foreach (var physical in agents.Values.ToArray())
            foreach (var moment in (physical.IdentityMoments ?? []).Where(item => item.Outcome == "requested"))
                if (!pendingIdentityMoments.ContainsKey(physical.AgentId))
                    FinishIdentityMoment(physical.AgentId, moment.Kind, "interrupted");
    }
}
