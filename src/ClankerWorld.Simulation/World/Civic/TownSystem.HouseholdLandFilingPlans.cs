using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal HouseholdLandFilingActionPlan? PlanHouseholdLandFiling(ITownWorld world, string actor, HouseholdLandFilingChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            !world.Agents.TryGetValue(actor, out var person) || world.Society.GetAgent(actor).HouseholdId is not { } household) return null;
        if (choice.BuildingId is null) return new(actor, choice, household, RequestableLandNear(world, town, person.Position, 6), null, null);
        var building = world.WorldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == choice.BuildingId);
        return building is not null && ExpansionLandRequestTiles(world, actor, building) is { } tiles
            ? new(actor, choice, household, tiles, tiles, building) : null;
    }

    internal static HouseholdLandFilingActionPlan BindHouseholdLandFiling(HouseholdLandFilingActionPlan plan, IReadOnlyList<CognitionLandTile>? tiles) =>
        plan.SelectedTiles is not null ? plan : plan with { SelectedTiles = tiles?.Select(tile => new GridPoint(tile.X, tile.Y)).ToArray() ?? [] };

    internal ActionCheck CheckHouseholdLandFiling(ITownWorld world, HouseholdLandFilingActionPlan plan, TownRuntimeState? currentTown = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            world.Society.GetAgent(plan.Actor).HouseholdId != plan.Household || !TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) ||
            plan.SelectedTiles is null && plan.OfferedTiles.Count == 0)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected household or requestable plot has changed");
        if (plan.Building is { } selected && (!world.WorldSimulation.Buildings.Contains(selected) ||
                selected.HouseholdId != plan.Household || !SelectedExpansionPlotAvailable(world, plan, selected) ||
                plan.Choice.PlotToken != CivicAgentToken(TownLandClaimRules.DescribeTiles(plan.SelectedTiles!))))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected House or expansion plot has changed");
        var tiles = plan.SelectedTiles;
        if (tiles is null) return new();
        var failure = HouseholdLandFilingFailure(world, TownRecordIds.LandRequest(town.Id, town.Council.Notices.Count + 1),
            plan.Actor, town.Id, tiles, null);
        return failure is null ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, failure);
    }

    private bool SelectedExpansionPlotAvailable(ITownWorld world, HouseholdLandFilingActionPlan plan, PlacedBuilding building)
    {
        if (plan.SelectedTiles is not { Count: > 0 } selected || ExpansionLandRequestTiles(world, plan.Actor, building) is null ||
            selected.Any(world.PendingTownProjectSiteTiles().Contains)) return false;
        var definition = world.WorldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        var matching = false;
        foreach (var shape in world.ExpansionShapes(building))
        {
            if (!world.CanFitExpansion(building, shape.Position, shape.Footprint, out _, requireLandRights: false)) continue;
            var extra = TownLandRightsRules.OrderTiles(WorldContentSimulationRules.Footprint(
                BuildingStorageRules.WithSize(definition, shape.Footprint.Width, shape.Footprint.Height), shape.Position)
                .Except(WorldContentSimulationRules.Footprint(definition, building))
                .Where(tile => !householdLandUseRights.Any(right => right.HouseholdId == plan.Household && right.Tiles.Contains(tile))));
            if (extra.SequenceEqual(selected)) matching = true;
        }
        return matching;
    }

    private void OfferPersonalLandFiling(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town, string siteRecovery)
    {
        if (PlanHouseholdLandFiling(world, actor, new(town.Id, null, "")) is not { } plan || !CheckHouseholdLandFiling(world, plan).Allowed) return;
        var here = world.Agents[actor].Position;
        candidates.Add(new(plan.CandidateId, siteRecovery + $"Ask {town.Name}'s council for household use of a connected plot of Town-titled land; include exact coordinates in civic_land_tiles. " +
            FormattableString.Invariant($"You stand at ({here.X}, {here.Y}); free Town land nearest you: {string.Join("; ", plan.OfferedTiles.Select(tile => FormattableString.Invariant($"({tile.X}, {tile.Y})")))}. ") +
            "Filing grants nothing and supplies no household acceptance.", 190)
        { ActionPlan = plan });
    }
}
