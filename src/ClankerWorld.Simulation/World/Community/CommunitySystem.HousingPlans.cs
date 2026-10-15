namespace ClankerWorld.Simulation.World;

internal sealed partial class CommunitySystem
{
    internal static HousingActionPlan? PlanHousingAction(ICommunityHousingWorld world, string actor, HousingChoice choice)
    {
        var applicant = choice.Kind == HousingChoiceKind.Ask ? actor : choice.TargetId;
        if (!world.PhysicalAgents.Living.ContainsKey(actor) || !world.PhysicalAgents.Living.TryGetValue(applicant, out var person)) return null;
        var request = person.Housing?.Request;
        var household = choice.Kind == HousingChoiceKind.Ask ? choice.TargetId : request?.HouseholdId;
        if (household is null || world.HouseForHousehold(household) is not { } house) return null;
        return new(actor, choice, applicant, household, house, request, world.HouseholdAdults(household));
    }

    internal static ActionCheck CheckHousingActionPlan(ICommunityHousingWorld world, HousingActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? HouseholdCommunityCapabilities);
        if (!capability.Allowed) return capability;
        if (!world.PhysicalAgents.Living.TryGetValue(plan.ApplicantId, out var applicant) ||
            world.HouseForHousehold(plan.HouseholdId) is not { } house ||
            house.InstanceId != plan.House.InstanceId || house.HouseholdId != plan.House.HouseholdId || house.TownId != plan.House.TownId ||
            !world.HouseholdAdults(plan.HouseholdId).SequenceEqual(plan.AdultMembers, StringComparer.Ordinal))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected House or its adult members have changed");
        if (plan.Choice.Kind == HousingChoiceKind.Ask)
            return applicant.Housing?.Request is null && world.AskableHouseholds(plan.Actor).Any(home => home.Id == plan.HouseholdId)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that household is no longer available for this housing request");
        // Answers by other members may accumulate in one decision batch. They
        // do not replace the selected request or change who must answer it.
        var current = applicant.Housing?.Request;
        var sameRequest = current is not null && plan.OriginalRequest is { } original &&
            current.HouseholdId == original.HouseholdId && current.RequestedTick == original.RequestedTick &&
            current.ExpiryTick == original.ExpiryTick && current.Members.SequenceEqual(original.Members, StringComparer.Ordinal);
        return sameRequest && world.PendingHousingRequestsFor(plan.Actor).Contains(plan.ApplicantId, StringComparer.Ordinal)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected housing request has ended or this member has already answered");
    }

}
