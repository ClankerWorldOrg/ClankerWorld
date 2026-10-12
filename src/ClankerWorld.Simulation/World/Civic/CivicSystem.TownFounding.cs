using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CivicSystem
{
    internal static IEnumerable<GridPoint> TownFoundingBuildingTiles(ICivicTownWorld world, PlacedBuilding building) => WorldContentSimulationRules.Footprint(
            world.WorldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building);

    internal TownRuntimeState? TownFoundingLayout(ICivicTownWorld world, string actor)
    {
        if (founderSetup is not { Started: true } || !world.AdultResident(actor) || !world.ReadyForBriefInteraction(actor)) return null;
        var site = world.Agents[actor].Position;
        if (!world.Map.IsBuildable(site)) return null;
        if (towns.Any(town => town.BorderTiles.Contains(site)) || townLandTitles.Any(title => title.Tiles.Contains(site))) return null;
        var claimed = towns.SelectMany(town => town.BorderTiles)
            .Concat(townLandTitles.SelectMany(title => title.Tiles)).ToHashSet();
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
        if (towns.Any(town => town.Id == id)) return null;
        return new TownRuntimeState(id, $"Town at {site.X.ToString(CultureInfo.InvariantCulture)}, {site.Y.ToString(CultureInfo.InvariantCulture)}",
            "founded", world.WorldTick, [], assigned.Order(StringComparer.Ordinal).ToArray(), border, site,
            Government: TownGovernmentState.Create(world.WorldTick));
    }

    internal void FoundTown(ICivicTownWorld world, string actor, string candidate)
    {
        // Bind the model choice to the adult's current physical site, and recheck after
        // earlier choices have committed their titles. A stale or invented choice does nothing.
        if (TownFoundingLayout(world, actor) is not { } town || CivicAction(world, town.Id, "found") != candidate) return;
        var previous = TownForResident(world, actor);
        var group = TownCareGroup(world, actor, previous);
        var moving = group.ToHashSet(StringComparer.Ordinal);
        foreach (var other in towns.Where(item => item.ResidentIds.Any(moving.Contains)).ToArray())
            SetTown(world, other with { ResidentIds = other.ResidentIds.Where(id => !moving.Contains(id)).ToArray() });
        town = town with { ResidentIds = group };
        SetTownState(town);
        townLandTitles = [.. townLandTitles, .. TownLandRightsRules.InitialTitles(world.Map, town, world.WorldTick)];
        townLandTitles = townLandTitles.OrderBy(title => title.Id, StringComparer.Ordinal).ToList();
        var assigned = town.AssignedBuildingIds.ToHashSet(StringComparer.Ordinal);
        var household = world.Society.GetAgent(actor).HouseholdId;
        if (household is not null)
        {
            var used = world.WorldSimulation.Buildings.Where(building => assigned.Contains(building.InstanceId)).SelectMany(item => TownFoundingBuildingTiles(world, item))
                .Concat(world.Fields.Where(field => field.HouseholdId == household && town.BorderTiles.Contains(field.Position)).Select(field => field.Position));
            var rights = TownLandRightsRules.InitialUseRights(world.Map, town.Id, [(household, used)], world.WorldTick);
            householdLandUseRights = [.. householdLandUseRights, .. rights.Select(right => right with { Id = DerivedId.Create(right.Id, "household-use:") })];
            householdLandUseRights = householdLandUseRights.OrderBy(right => right.Id, StringComparer.Ordinal).ToList();
        }
        world.WorldSimulation = world.WorldSimulation with
        {
            Buildings = world.WorldSimulation.Buildings.Select(building => assigned.Contains(building.InstanceId)
                ? building with { TownId = town.Id } : building).ToArray()
        };
        AdvanceTownGovernance(world);
        SettleTownAdmissions(world);
        world.AppendEvent("town_founded", $"{town.Id}|{actor}|{previous ?? "none"}|{group.Length}", world.Agents[actor].Position);
        if (world.WorldSimulation.Buildings.Where(building => assigned.Contains(building.InstanceId))
            .OrderBy(building => building.PlacedTick).ThenBy(building => building.InstanceId, StringComparer.Ordinal)
            .FirstOrDefault() is { } firstBuilding)
            world.GenerateRoadBetweenTowns(town, firstBuilding);
    }

}
