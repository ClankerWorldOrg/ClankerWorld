using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    private void OfferTownAdmission(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownAdmissionChoiceKind kind, string subject, string text, int priority)
    {
        var id = CivicAction(world, town.Id, TownAdmissionChoice.Verb(kind), subject);
        if (TownAdmissionChoice.Decode(id) is { } choice && PlanTownAdmission(world, actor, choice) is { } plan &&
            CheckTownAdmissionPlan(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }

    internal TownAdmissionActionPlan? PlanTownAdmission(ITownWorld world, string actor, TownAdmissionChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { } town) return null;
        var subject = choice.Kind == TownAdmissionChoiceKind.Sponsor ? ResolveCivicAgentToken(world, choice.SubjectToken) : actor;
        if (!world.Agents.ContainsKey(subject)) return null;
        var previous = TownForResident(world, subject);
        return new(actor, choice, subject, previous, TownCareGroup(world, subject, previous),
            choice.Kind == TownAdmissionChoiceKind.Accept ? ApprovedAdmission(world, town, actor) : null);
    }

    internal ActionCheck CheckTownAdmissionPlan(ITownWorld world, TownAdmissionActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? CivicMembershipCapabilities);
        if (!capability.Allowed) return capability;
        if (world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId) is not { Council: not null } town ||
            !world.Agents.ContainsKey(plan.Subject) || TownForResident(world, plan.Subject) != plan.PreviousTown ||
            !TownCareGroup(world, plan.Subject, plan.PreviousTown).SequenceEqual(plan.CareGroup))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected admission, affiliation or dependent group has changed");
        var allowed = plan.Choice.Kind switch
        {
            TownAdmissionChoiceKind.Request => NearCivicBoard(world, plan.Actor, town) && MayRequestOwnAdmission(world, plan.Actor, town),
            TownAdmissionChoiceKind.Sponsor => TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) &&
                plan.Subject != plan.Actor && MayBeSponsoredForAdmission(world, plan.Subject, town) &&
                world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, world.Agents[plan.Subject].Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange),
            _ => plan.Approval is { } approval && approval.ProposalId == plan.Choice.SubjectToken &&
                ApprovedAdmission(world, town, plan.Actor) == approval && MayAcceptAdmission(world, plan.Actor, town, approval),
        };
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that selected Town admission response is no longer available");
    }
}
