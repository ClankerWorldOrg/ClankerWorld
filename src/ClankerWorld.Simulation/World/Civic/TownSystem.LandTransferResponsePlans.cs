using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static LandTransferResponseActionPlan? PlanLandTransferResponse(ITownWorld world, string actor, LandTransferResponseChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.LandRecords.Transfers.SingleOrDefault(request => LandTransferActionToken(request) == choice.TermsToken) is not { } request ||
            LandTransferParties(world, request).SingleOrDefault(party => party.AdultIds.Contains(actor, StringComparer.Ordinal)) is not { } party) return null;
        return new(actor, choice, request, party.HouseholdId, choice.Kind == LandTransferResponseKind.Read ? CivicBoard(world, town) : null);
    }

    internal ActionCheck CheckLandTransferResponse(ITownWorld world, LandTransferResponseActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? LandRightsCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            town.LandRecords.Transfers.SingleOrDefault(request => request.Id == plan.Request.Id) is not { Status: "pending" } current ||
            LandTransferActionToken(current) != plan.Choice.TermsToken || current.FilerId != plan.Request.FilerId ||
            current.ProposedTick != plan.Request.ProposedTick || current.NoticeId != plan.Request.NoticeId ||
            !TownLandTransferRules.TermsToken(current).Equals(TownLandTransferRules.TermsToken(plan.Request), StringComparison.Ordinal) ||
            LandTransferParties(world, current).SingleOrDefault(party => party.HouseholdId == plan.PartyHousehold &&
                party.AdultIds.Contains(plan.Actor, StringComparer.Ordinal)) is not { } party)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected permission request, terms or household party have changed");
        var knows = TownLandTransferRules.HasNotice(current, plan.Actor, world.WorldTick, town.Council.Knowledge);
        if (plan.Choice.Kind == LandTransferResponseKind.Read)
            return !knows && CivicBoard(world, town) == plan.NoticePlace && NearCivicBoard(world, plan.Actor, town)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected transfer notice is unavailable or already learned");
        if (!knows) return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they have not learned those selected permission terms");
        if (plan.Choice.Kind == LandTransferResponseKind.Withdraw)
            return current.FilerId == plan.Actor ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "only the actual filer can withdraw those terms");
        var accepted = TownLandTransferRules.AcceptedAdults(current, party, town.Council.Knowledge, world.WorldTick).Contains(plan.Actor, StringComparer.Ordinal);
        return plan.Choice.PartyToken == CivicAgentToken(party.HouseholdId) &&
            TownLandTransferRules.PendingFailure(town.LandRecords, current, householdLandUseRights, householdLandUseRequests, world.WorldTick) is null &&
            (plan.Choice.Kind == LandTransferResponseKind.Decline || !accepted)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that selected personal permission response is no longer eligible");
    }

    private void OfferLandTransferResponse(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownLandTransferRequest request, LandTransferResponseKind kind, string party, string text, int priority)
    {
        var id = CivicAction(world, town.Id, LandTransferResponseChoice.Verb(kind), LandTransferActionToken(request), party);
        if (LandTransferResponseChoice.Decode(id) is { } choice && PlanLandTransferResponse(world, actor, choice) is { } plan && CheckLandTransferResponse(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }
}
