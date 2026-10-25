using System.Globalization;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static IEnumerable<GridPoint> TownFoundingBuildingTiles(ITownWorld world, PlacedBuilding building) => WorldContentSimulationRules.Footprint(
            world.WorldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building);

    internal TownRuntimeState? TownFoundingLayout(ITownWorld world, string actor)
    {
        if (State.FounderSetup is not { Started: true } || !world.IsAdult(actor) || !world.ReadyForBriefInteraction(actor)) return null;
        var site = world.Agents[actor].Position;
        if (!world.Map.IsBuildable(site)) return null;
        if (State.Towns.Any(town => town.BorderTiles.Contains(site)) || State.TownLandTitles.Any(title => title.Tiles.Contains(site))) return null;
        var claimed = State.Towns.SelectMany(town => town.BorderTiles)
            .Concat(State.TownLandTitles.SelectMany(title => title.Tiles)).ToHashSet();
        var household = world.Society.GetAgent(actor).HouseholdId;
        var core = new HashSet<GridPoint> { site };
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<GridPoint> border;
        // Preserve the normal spare-land margin around any existing household buildings
        // incorporated into the first layout. Never swallow somebody else's property.
        while (true)
        {
            border = TownBorderRules.Around(world.Map, core);
            if (border.Any(claimed.Contains)) return null;
            var tiles = border.ToHashSet();
            var added = false;
            foreach (var building in world.WorldSimulation.Buildings)
            {
                var footprint = TownFoundingBuildingTiles(world, building).ToArray();
                if (!footprint.Any(tiles.Contains)) continue;
                if (building.TownId is not null || household is null || building.HouseholdId != household) return null;
                if (assigned.Add(building.InstanceId))
                {
                    core.UnionWith(footprint);
                    added = true;
                }
            }
            if (!added) break;
        }
        if (world.Fields.Any(field => border.Contains(field.Position) && field.HouseholdId != household)) return null;
        if (world.WorldSimulation.Buildings.Count(building => assigned.Contains(building.InstanceId) &&
            world.WorldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId).Tags.Contains("warehouse", StringComparer.Ordinal)) > 1)
            return null;
        var id = "town:site:" + DerivedId.Hash(
            world.WorldSeed + "|" + site.X.ToString(CultureInfo.InvariantCulture) + "," + site.Y.ToString(CultureInfo.InvariantCulture));
        if (State.Towns.Any(town => town.Id == id)) return null;
        var layout = new TownRuntimeState(id, $"Town at {site.X.ToString(CultureInfo.InvariantCulture)}, {site.Y.ToString(CultureInfo.InvariantCulture)}",
            "founded", world.WorldTick, [], assigned.Order(StringComparer.Ordinal).ToArray(), border, site,
            Government: TownGovernmentState.Create(world.WorldTick));
        return SelectedFoundingLayoutAvailable(world, actor, layout) ? layout : null;
    }

    internal void FoundTown(ITownWorld world, string actor, string candidate, TownFoundingActionPlan? selected = null)
    {
        selected ??= TownFoundingChoice.Decode(candidate) is { } choice ? PlanTownFounding(world, actor, choice) : null;
        if (selected is null) return;
        if (RefuseIfNeeded(world, selected, CheckTownFoundingPlan(world, selected).Refusal)) return;
        var town = selected.Town with
        {
            FoundedTick = world.WorldTick,
            FirstBuildingCompletedTick = selected.Town.AssignedBuildingIds.Count > 0 ? world.WorldTick : null,
            Government = TownGovernmentState.Create(world.WorldTick),
        };
        var previous = selected.PreviousTown;
        var group = selected.CareGroup;
        var moving = group.ToHashSet(StringComparer.Ordinal);
        foreach (var other in State.Towns.Where(item => item.ResidentIds.Any(moving.Contains)).ToArray())
            SetTown(world, other with { ResidentIds = other.ResidentIds.Where(id => !moving.Contains(id)).ToArray() });
        town = town with { ResidentIds = group };
        SetTownState(town);
        RestoreTownLandTitles([.. State.TownLandTitles, .. TownLandRightsRules.InitialTitles(world.Map, town, world.WorldTick)]);
        RestoreTownLandTitles(State.TownLandTitles.OrderBy(title => title.Id, StringComparer.Ordinal).ToList());
        var assigned = town.AssignedBuildingIds.ToHashSet(StringComparer.Ordinal);
        var household = world.Society.GetAgent(actor).HouseholdId;
        if (household is not null)
        {
            var used = world.WorldSimulation.Buildings.Where(building => assigned.Contains(building.InstanceId)).SelectMany(item => TownFoundingBuildingTiles(world, item))
                .Concat(world.Fields.Where(field => field.HouseholdId == household && town.BorderTiles.Contains(field.Position)).Select(field => field.Position));
            var rights = TownLandRightsRules.InitialUseRights(world.Map, town.Id, [(household, used)], world.WorldTick);
            RestoreHouseholdLandUseRights([.. State.HouseholdLandUseRights, .. rights.Select(right => right with { Id = DerivedId.Create(right.Id, "household-use:") })]);
            RestoreHouseholdLandUseRights(State.HouseholdLandUseRights.OrderBy(right => right.Id, StringComparer.Ordinal).ToList());
        }
        world.AssignFoundingBuildings(assigned, town.Id);
        AdvanceTownCouncil(world);
        SettleTownAdmissions(world);
        world.AppendEvent(new TownFoundedEventPayload(actor, town.Id, previous ?? "none", group.Count, false), world.Agents[actor].Position);
        if (world.WorldSimulation.Buildings.Where(building => assigned.Contains(building.InstanceId))
            .OrderBy(building => building.PlacedTick).ThenBy(building => building.InstanceId, StringComparer.Ordinal)
            .FirstOrDefault() is { } firstBuilding)
            world.GenerateRoadBetweenTowns(town, firstBuilding);
    }

}
