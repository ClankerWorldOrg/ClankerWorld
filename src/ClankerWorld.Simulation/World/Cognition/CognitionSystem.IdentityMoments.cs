using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal static void DiscoverIdentityMoments(ICognitionIdentityMomentsWorld world)
    {
        var checkpoint = world.Society;
        foreach (var person in checkpoint.Agents.Where(item => item.Status == SocietyAgentStatus.Active))
        {
            var physical = world.Agents[person.Id];
            var kinds = new List<AgentIdentityMomentKind>();
            var age = checkpoint.AgeAt(person, world.WorldTick);
            if (checkpoint.Config.DayLifecycle is { } days && age >= days.MaximumDay / 2)
                kinds.Add(AgentIdentityMomentKind.Midlife);
            if (LifeStageRules.IsElder(person.AgeBand))
                kinds.Add(AgentIdentityMomentKind.Elder);
            if (checkpoint.Relationships.Any(edge => edge.Type == SocietyRelationshipType.BiologicalParentage && edge.ProposerId == person.Id))
                kinds.Add(AgentIdentityMomentKind.Parenthood);
            if (checkpoint.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Partnership && edge.State == SocietyRelationshipState.EndedByDeath && (edge.ProposerId == person.Id || edge.TargetId == person.Id)))
                kinds.Add(AgentIdentityMomentKind.PartnerLoss);
            if (checkpoint.Relationships.Any(edge => edge.Type == SocietyRelationshipType.BiologicalParentage && edge.TargetId == person.Id && checkpoint.GetAgent(edge.ProposerId).Status == SocietyAgentStatus.Dead))
                kinds.Add(AgentIdentityMomentKind.ParentLoss);
            var existing = physical.IdentityMoments ?? [];
            var added = kinds.Where(kind => !existing.Any(moment => moment.Kind == kind)).Select(kind => new AgentIdentityMoment(kind, world.WorldTick)).ToArray();
            if (added.Length == 0)
                continue;
            world.PhysicalAgents.RecordIdentityMoments(person.Id, physical, existing.Concat(added).ToArray());
            world.MarkCheckpointCurrent();
        }
    }

    internal static void StartIdentityMoments(ICognitionIdentityMomentsWorld world) => world.PrepareIdentityMoments(world.StartPreparedIdentityMoment);

    internal static void PrepareIdentityMoments(ICognitionIdentityMomentsWorld world, Action<PreparedIdentityMoment> prepare, IReadOnlySet<string>? activeModelCallIds = null, IReadOnlySet<string>? activeIdentityIds = null)
    {
        // Bounded separately from ordinary decisions. The installation usage
        // store still reserves each potentially paid request before HTTP.
        var capacity = Math.Max(0, world.Host.MaximumDispatch - (activeIdentityIds?.Count ?? world.Host.IdentityMoments.Count));
        foreach (var person in world.Society.Agents.Where(item => item.Status == SocietyAgentStatus.Active && !LifeStageRules.IsInfant(item.AgeBand)).OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (capacity == 0)
                break;
            var physical = world.Agents[person.Id];
            if (physical.IdentityChoicePending || (activeIdentityIds?.Contains(person.Id) ?? world.Host.IdentityMoments.ContainsKey(person.Id)) || (activeModelCallIds?.Contains(person.Id) ?? world.Host.ModelCalls.ContainsKey(person.Id)) || world.IsConversationBusy(person.Id) || world.NeedsUrgentFood(physical) || world.NeedsUrgentWarmth(physical))
                continue;
            var moment = (physical.IdentityMoments ?? []).FirstOrDefault(item => item.Outcome == "waiting");
            if (moment is null)
                continue;
            try
            {
                var observation = world.CreateIdentityMomentObservation(person, physical, moment);
                var provider = world.Host.ProviderFactory?.Invoke(person.Id) ?? new DeterministicDecisionProvider();
                if (provider.KindFor(observation) != DecisionProviderKind.LargeLanguageModel)
                {
                    world.FinishIdentityMoment(person.Id, moment.Kind, "kept");
                    continue;
                }

                var request = new CognitionDecisionRequest($"identity:{Guid.NewGuid():N}", provider.ProviderEpoch, observation);
                request.Validate();
                world.SetIdentityMoment(person.Id, moment with { Outcome = "requested", RequestedTick = world.WorldTick });
                world.AppendEvent("agent_identity_moment_started", $"{person.Id}:{moment.Kind}");
                prepare(new(moment.Kind, request, provider));
                capacity--;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                world.FinishIdentityMoment(person.Id, moment.Kind, "kept");
            }
        }
    }

    internal static void StartPreparedIdentityMoment(ICognitionIdentityMomentsWorld world, PreparedIdentityMoment prepared)
    {
        var cancellation = new CancellationTokenSource(IdentityMomentTimeout);
        var task = Task.Run(async () =>
        {
            try
            {
                cancellation.Token.ThrowIfCancellationRequested();
                return new IdentityMomentOutcome(await prepared.Provider.DecideAsync(prepared.Request, cancellation.Token).AsTask().WaitAsync(cancellation.Token).ConfigureAwait(false));
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // Neither provider replies nor their exception messages enter events or logs.
                return new IdentityMomentOutcome(null);
            }
        });
        world.Host.QueueIdentityMoment(prepared.Request.Observation.AgentId, new(prepared.Kind, prepared.Request, task, cancellation));
    }

    internal static AgentObservation CreateIdentityMomentObservation(ICognitionIdentityMomentsWorld world, SocietyAgent person, AgentState physical, AgentIdentityMoment moment)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{person.Id}|{moment.Kind}|{moment.TriggeredTick}|{physical.Personality}|{physical.Aspiration}")));
        return new AgentObservation(person.Id, world.WorldTick, world.Society.RunEpoch, moment.TriggeredTick, digest, physical.FullnessBasisPoints, [new("identity_optional", "Keep your current identity or choose an optional change for this life moment.")], RequiresPersonalProvider: true, Self: new(person.Id, person.Name, person.AgeBand.ToString(), physical.Personality, physical.Aspiration, person.HouseholdId, null, null, null), NeedsPersonality: true, NeedsAspiration: true)
        {
            IdentityMoment = moment.Reason
        };
    }

    internal static void CompleteIdentityMoments(ICognitionIdentityMomentsWorld world, IReadOnlyList<PendingIdentityMoment> completed, Func<PendingIdentityMoment, bool> providerIsCurrent)
    {
        foreach (var pending in completed)
        {
            var id = pending.Request.Observation.AgentId;
            if (!world.Agents.TryGetValue(id, out var physical) || world.Society.GetAgent(id).Status != SocietyAgentStatus.Active || !(physical.IdentityMoments ?? []).Any(moment => moment.Kind == pending.Kind && moment.Outcome == "requested"))
                continue;
            if (world.Society.RunEpoch != pending.Request.Observation.RunEpoch || physical.Personality != pending.Request.Observation.Self!.Personality || physical.Aspiration != pending.Request.Observation.Self.Aspiration || !providerIsCurrent(pending))
            {
                world.FinishIdentityMoment(id, pending.Kind, "interrupted");
                continue;
            }

            var response = pending.Task.GetAwaiter().GetResult().Response;
            var valid = IsMatchingIdentityMomentResponse(pending.Request, response);

            var personality = valid ? response!.ChosenPersonality : null;
            var aspiration = valid ? response!.ChosenAspiration : null;
            if (personality == physical.Personality)
                personality = null;
            if (aspiration == physical.Aspiration)
                aspiration = null;
            var accepted = personality is not null || aspiration is not null;
            world.PhysicalAgents.FinishIdentityMoment(id, physical, personality ?? physical.Personality, aspiration ?? physical.Aspiration, accepted ? null : physical.LastDecisionContext);
            world.FinishIdentityMoment(id, pending.Kind, accepted ? "accepted" : "kept", personality, aspiration);
        }
    }

    internal static bool IsIdentityMomentProviderCurrent(ICognitionIdentityMomentsWorld world, PendingIdentityMoment pending)
    {
        try
        {
            var provider = world.Host.ProviderFactory?.Invoke(pending.Request.Observation.AgentId);
            return provider is not null && provider.KindFor(pending.Request.Observation) == DecisionProviderKind.LargeLanguageModel && provider.ProviderEpoch == pending.Request.ProviderEpoch;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return false;
        }
    }

    internal static void FinishIdentityMoment(ICognitionIdentityMomentsWorld world, string id, AgentIdentityMomentKind kind, string outcome, string? personality = null, string? aspiration = null)
    {
        var moment = (world.Agents[id].IdentityMoments ?? []).Single(item => item.Kind == kind);
        world.SetIdentityMoment(id, moment with { Outcome = outcome, CompletedTick = world.WorldTick, Personality = personality, Aspiration = aspiration, });
        world.AppendEvent(outcome == "accepted" ? "agent_identity_revised" : "agent_identity_moment_kept", $"{id}:{kind}");
    }

    internal static void SetIdentityMoment(ICognitionIdentityMomentsWorld world, string id, AgentIdentityMoment moment)
    {
        var physical = world.Agents[id];
        world.PhysicalAgents.RecordIdentityMoments(id, physical, (physical.IdentityMoments ?? []).Select(item => item.Kind == moment.Kind ? moment : item).ToArray());
        world.MarkCheckpointCurrent();
    }

    internal static void CancelIdentityMoments(ICognitionIdentityMomentsWorld world, bool underRuntimeGate = true)
    {
        foreach (var id in world.Host.IdentityMoments.Keys.ToArray())
            world.CancelIdentityMoment(id, underRuntimeGate);
    }

    internal static void CancelIdentityMoment(ICognitionIdentityMomentsWorld world, string id, bool underRuntimeGate = true)
    {
        if (!world.Host.ForgetIdentityMoment(id, out var pending))
            return;
        if (world.Agents.TryGetValue(id, out var physical) && (physical.IdentityMoments ?? []).Any(moment => moment.Kind == pending.Kind && moment.Outcome == "requested"))
            world.FinishIdentityMoment(id, pending.Kind, "interrupted");
        world.CancelProviderCall(pending.Cancellation, underRuntimeGate);
        _ = pending.Task.ContinueWith(_ => pending.Cancellation.Dispose(), TaskScheduler.Default);
    }

    internal static void ReconcileIdentityMoments(ICognitionIdentityMomentsWorld world)
    {
        foreach (var (id, pending) in world.Host.IdentityMoments.ToArray())
            if (!world.Agents.ContainsKey(id) || world.Society.RunEpoch != pending.Request.Observation.RunEpoch || !world.IsIdentityMomentProviderCurrent(pending))
                world.CancelIdentityMoment(id);
        // A saved request has no live provider task after restore. Its single
        // opportunity remains consumed; returning to the game never retries it.
        foreach (var physical in world.Agents.Values.ToArray())
            foreach (var moment in (physical.IdentityMoments ?? []).Where(item => item.Outcome == "requested"))
                if (!world.Host.IdentityMoments.ContainsKey(physical.AgentId))
                    world.FinishIdentityMoment(physical.AgentId, moment.Kind, "interrupted");
    }
}
