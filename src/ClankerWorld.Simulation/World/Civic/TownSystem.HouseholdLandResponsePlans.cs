using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static HouseholdLandResponseActionPlan? PlanHouseholdLandResponse(ITownWorld world, string actor, HouseholdLandResponseChoice choice) =>
        world.HouseholdLandUseRequests.SingleOrDefault(request => request.Id == choice.RequestId) is { } request
            ? new(actor, choice, request with
            {
                Tiles = request.Tiles.ToArray(),
                Consents = request.Consents.ToArray(),
                GrantAdults = request.GrantAdults.ToArray(),
                HearingResolutions = request.HearingResolutions.ToArray()
            }) : null;

    internal static ActionCheck CheckHouseholdLandResponse(ITownWorld world, HouseholdLandResponseActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            world.HouseholdLandUseRequests.SingleOrDefault(request => request.Id == plan.Request.Id) is not { Status: "pending" } current ||
            current.TownId != town.Id || current with
            {
                CouncilProposalId = plan.Request.CouncilProposalId,
                Tiles = plan.Request.Tiles,
                Consents = plan.Request.Consents,
                GrantAdults = plan.Request.GrantAdults,
                HearingResolutions = plan.Request.HearingResolutions
            } != plan.Request ||
            !current.Tiles.SequenceEqual(plan.Request.Tiles) || !current.GrantAdults.SequenceEqual(plan.Request.GrantAdults) ||
            !current.HearingResolutions.SequenceEqual(plan.Request.HearingResolutions) || !CivicHistory(town).Knows(plan.Actor, "land_use", current.Id))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected household land request or exact terms have changed");
        var allowed = plan.Choice.Kind == HouseholdLandResponseKind.Withdraw ? current.RequestedByAgentId == plan.Actor :
            world.HouseholdAdults(current.HouseholdId).Contains(plan.Actor, StringComparer.Ordinal) &&
            !current.Consents.Any(consent => consent.AgentId == plan.Actor) &&
            (plan.Choice.Kind != HouseholdLandResponseKind.Accept || current.HearingResolutions.Count == 0);
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that personal land response is final or no longer eligible");
    }

    private static void OfferHouseholdLandResponse(ITownWorld world, List<CognitionCandidate> candidates, string actor,
        TownRuntimeState town, HouseholdLandUseRequest request, HouseholdLandResponseKind kind, string text, int priority)
    {
        if (PlanHouseholdLandResponse(world, actor, new(town.Id, kind, request.Id)) is { } plan && CheckHouseholdLandResponse(world, plan).Allowed)
            candidates.Add(new(plan.CandidateId, text, priority) { ActionPlan = plan });
    }
}
