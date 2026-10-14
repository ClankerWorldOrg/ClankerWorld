using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private HousingActionPlan? PlanHousingAction(string actor, HousingChoice choice)
    {
        var applicant = choice.Kind == HousingChoiceKind.Ask ? actor : choice.TargetId;
        if (!agents.ContainsKey(actor) || !agents.TryGetValue(applicant, out var person)) return null;
        var request = person.Housing?.Request;
        var household = choice.Kind == HousingChoiceKind.Ask ? choice.TargetId : request?.HouseholdId;
        if (household is null || HouseForHousehold(household) is not { } house) return null;
        return new(actor, choice, applicant, household, house, request, HouseholdAdults(household));
    }

    private ActionCheck CheckHousingActionPlan(HousingActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? HouseholdCommunityCapabilities);
        if (!capability.Allowed) return capability;
        if (!agents.TryGetValue(plan.ApplicantId, out var applicant) ||
            HouseForHousehold(plan.HouseholdId) is not { } house ||
            house.InstanceId != plan.House.InstanceId || house.HouseholdId != plan.House.HouseholdId || house.TownId != plan.House.TownId ||
            !HouseholdAdults(plan.HouseholdId).SequenceEqual(plan.AdultMembers, StringComparer.Ordinal))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected House or its adult members have changed");
        if (plan.Choice.Kind == HousingChoiceKind.Ask)
            return applicant.Housing?.Request is null && AskableHouseholds(plan.Actor).Any(home => home.Id == plan.HouseholdId)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that household is no longer available for this housing request");
        // Answers by other members may accumulate in one decision batch. They
        // do not replace the selected request or change who must answer it.
        var current = applicant.Housing?.Request;
        var sameRequest = current is not null && plan.OriginalRequest is { } original &&
            current.HouseholdId == original.HouseholdId && current.RequestedTick == original.RequestedTick &&
            current.ExpiryTick == original.ExpiryTick && current.Members.SequenceEqual(original.Members, StringComparer.Ordinal);
        return sameRequest && PendingHousingRequestsFor(plan.Actor).Contains(plan.ApplicantId, StringComparer.Ordinal)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected housing request has ended or this member has already answered");
    }

    private void AddHousingCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var options = new List<CognitionCandidate>();
        AddHousingCandidateOptions(options, actor);
        foreach (var candidate in options)
        {
            if (HousingChoice.Decode(candidate.Id) is not { } choice) { candidates.Add(candidate); continue; }
            if (HousingDefinition.Offer(this, actor, choice) is { } plan)
                candidates.Add(candidate with { ActionPlan = plan });
        }
    }

    private void ApplyHousingActionPlan(HousingActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteHousingAction(plan);
    }
}
