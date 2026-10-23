using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal LandTransferProposalActionPlan? PlanLandTransferProposal(ITownWorld world, string actor, LandTransferProposalChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            CivicBoard(world, town) is not { } board || world.Society.GetAgent(actor).HouseholdId is not { } household) return null;
        var own = State.HouseholdLandUseRights.Where(right => right.TownId == town.Id && right.HouseholdId == household &&
            (right.AgreedEndTick is null || right.AgreedEndTick > world.WorldTick)).ToArray();
        var adults = LandTransferAdultHouseholds(world);
        var options = LandTransferKnownHouseholds(world, actor, town).Where(target => target.Id != household).Select(target =>
            new LandTransferBeneficiary(target.Id, target.Name, TownLandRightsRules.OrderTiles(own.SelectMany(right => right.Tiles).Distinct()
                .Where(tile => TownLandTransferRules.CanPropose(town.LandRecords, world.Map, town.Id, actor, [tile], target.Id,
                    State.HouseholdLandUseRights, TownLandTransferRules.PartiesFor(State.HouseholdLandUseRights, [tile], target.Id, adults),
                    State.HouseholdLandUseRequests, world.WorldTick))))).Where(option => option.Tiles.Count > 0).ToArray();
        return new(actor, choice, board, options);
    }

    internal static LandTransferProposalActionPlan BindLandTransferProposal(LandTransferProposalActionPlan plan,
        IReadOnlyList<CognitionLandTile>? tiles, CognitionLandHearingChoice? terms)
    {
        if (plan.Bound) return plan;
        var invalid = terms?.AgreedEndTick is not null || (plan.Choice.Sale
            ? terms?.PaymentItemKind is not { } kind || terms.PaymentQuantity is not > 0 || !LandSaleKind(kind)
            : terms?.PaymentItemKind is not null || terms?.PaymentQuantity is not null);
        return plan with
        {
            Bound = true,
            TargetHousehold = terms?.HouseholdId,
            Tiles = TownLandRightsRules.OrderTiles(tiles?.Select(tile => new GridPoint(tile.X, tile.Y)) ?? []),
            InvalidTerms = invalid,
            Price = plan.Choice.Sale && !invalid ? new(plan.Choice.SourceHousehold, terms!.PaymentItemKind!, terms.PaymentQuantity!.Value) : null
        };
    }

    internal ActionCheck CheckLandTransferProposal(ITownWorld world, LandTransferProposalActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            world.Society.GetAgent(plan.Actor).HouseholdId != plan.Choice.SourceHousehold ||
            CivicBoard(world, town) != plan.NoticePlace || !NearCivicBoard(world, plan.Actor, town) ||
            CivicAgentToken(LandTransferSourceToken(world, town, plan.Choice.SourceHousehold)) != plan.Choice.SourceToken)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected source household, permission terms or notice place have changed");
        var known = LandTransferKnownHouseholds(world, plan.Actor, town).Select(household => household.Id).ToHashSet(StringComparer.Ordinal);
        var adults = LandTransferAdultHouseholds(world);
        bool Available(IReadOnlyList<GridPoint> plot, string target) => TownLandTransferRules.CanPropose(town.LandRecords, world.Map, town.Id, plan.Actor,
            plot, target, State.HouseholdLandUseRights, TownLandTransferRules.PartiesFor(State.HouseholdLandUseRights, plot, target, adults), State.HouseholdLandUseRequests, world.WorldTick);
        if (!plan.Bound)
            return plan.Beneficiaries.Any(option => known.Contains(option.HouseholdId) && option.Tiles.Any(tile => Available([tile], option.HouseholdId)))
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "none of the selected beneficiary plots remains available");
        return !plan.InvalidTerms && plan.TargetHousehold is { } target && target != plan.Choice.SourceHousehold && known.Contains(target) &&
            plan.Tiles is { Count: > 0 } selected && selected.All(tile => TownLandRightsRules.IsCoveredByTownTitle(tile, town.Id, State.TownLandTitles) &&
                State.HouseholdLandUseRights.Any(right => right.TownId == town.Id && right.HouseholdId == plan.Choice.SourceHousehold && right.Tiles.Contains(tile))) &&
            Available(selected, target) ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected beneficiary, exact plot or goods price is unavailable");
    }
}
