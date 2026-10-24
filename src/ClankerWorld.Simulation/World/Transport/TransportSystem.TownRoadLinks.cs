
namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    internal static GridPoint[] BuildingRoadEntrances(IRoadWorld world, PlacedBuilding building, HashSet<GridPoint> occupied)
    {
        var design = BuildingStorageRules.EffectiveDefinition(
            world.Definitions.Single(item => item.CanonicalId == building.DefinitionId), building);
        return BuildingMovementRules.EntrancesFor(world.Map, design, building.Position)
            .Where(point => (!occupied.Contains(point) || world.Transport.Roads.Contains(point)) && world.Map.IsBuildable(point) &&
                WorldContentSimulationRules.IsEntrance(design, building.Position, point) &&
                (!design.Tags.Contains(TownHallContent.HallTag, StringComparer.Ordinal) || point == TownHallContent.Entrance(building.Position)) &&
                (!design.Tags.Any(tag => tag is MarketContent.HallTag or MarketContent.StallTag) || point == building.Entrance) &&
                (!PortNavigationRules.IsPort(design) || point == building.Entrance))
            .Distinct().OrderBy(point => point.Y).ThenBy(point => point.X).ToArray();
    }

    internal static void GenerateRoadBetweenTowns(IRoadWorld world, TownRuntimeState town, PlacedBuilding firstBuilding)
    {
        if (town.OriginSite is not { } origin) return;
        var nearest = world.Towns.Where(other => other.Id != town.Id && other.OriginSite is not null)
            .OrderBy(other => world.Map.FootDistance(origin, other.OriginSite!.Value))
            .ThenBy(other => other.Id, StringComparer.Ordinal).FirstOrDefault();
        if (nearest is null) return;
        var physical = RoadOccupiedTiles(world);
        var occupied = physical.Concat(world.HouseholdLandHeldByOthers(null)).ToHashSet();
        var nearestBorder = nearest.BorderTiles.ToHashSet();
        var network = world.Transport.Roads.Where(nearestBorder.Contains).ToHashSet();
        if (network.Count == 0)
        {
            network.UnionWith(world.Buildings.Where(building => building.TownId == nearest.Id && nearestBorder.Contains(building.Position))
                .SelectMany(building => BuildingRoadEntrances(world, building, occupied)).Where(nearestBorder.Contains));
            if (network.Count == 0 && world.Map.IsBuildable(nearest.OriginSite!.Value) && !occupied.Contains(nearest.OriginSite.Value))
                network.Add(nearest.OriginSite.Value);
        }
        var localBuilding = town.BorderTiles.Contains(firstBuilding.Position);
        var starts = localBuilding ? BuildingRoadEntrances(world, firstBuilding, occupied) :
            world.Map.IsBuildable(origin) && (!occupied.Contains(origin) || world.Transport.Roads.Contains(origin)) ? new[] { origin } : [];
        var request = new RoadRouteRequest(world.Map, starts, network,
            occupied, world.SavedBridges, world.Transport.Roads, ReuseRoads: true, Occupied: physical);
        var result = RoadRoutePlanner.Plan(request);
        var failure = result.Proposal is { } proposal ? RoadRoutePlanner.Validate(request, proposal) : result.Outcome;
        if (failure is not null)
        {
            world.AppendEvent(new TownRoadLinkUnconnectedEventPayload(null, town.Id, nearest.Id, failure));
            return;
        }
        var route = result.Proposal!;
        var added = world.Transport.LayRoadLink(route.RoadTiles);
        if (localBuilding) world.SetBuildingEntrance(firstBuilding.InstanceId, route.RoadTiles[0]);
        CommitRoadBridges(world, route.NewCrossings, IdentityRules.PrefixedReference("road:town-link:", $"{town.Id}:{nearest.Id}"));
        world.AppendEvent(new TownRoadLinkedEventPayload(null, town.Id, nearest.Id, added));
    }
}
