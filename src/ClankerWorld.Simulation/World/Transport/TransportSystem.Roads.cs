using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    internal static void RepairSavedRoadFootprints(IRoadWorld world)
    {
        var occupied = world.Ground.BuildingFootprints.ToHashSet();
        var removed = world.Transport.RemoveOccupiedRoads(occupied);
        if (removed > 0) world.AppendEvent(new SavedRoadFootprintsRepairedEventPayload(null, removed));
    }

    internal static List<GridPoint> GenerateRoadToBuilding(IRoadWorld world, PlacedBuilding building, HashSet<GridPoint>? border = null)
    {
        var laid = new List<GridPoint>();
        if (building.TownId is null) return laid;
        // A lantern's Road edge is part of its paid Council approval. It is a
        // fitting beside an existing street, not a door that grows or turns it.
        if (StreetLanternContent.IsLantern(building.DefinitionId)) return laid;
        var occupied = RoadBlockedTiles(world);
        var entrances = BuildingRoadEntrances(world, building, occupied);
        if (entrances.Length == 0)
        {
            world.AppendEvent(new TownRoadUnconnectedEventPayload(null, building.TownId, building.InstanceId, RoadRouteOutcomes.NoEntrance));
            return laid;
        }
        var network = world.Transport.Roads.Where(point => border is null || border.Contains(point)).ToHashSet();
        if (network.Count == 0)
            network.UnionWith(world.Map.MapObjects
            .Where(item => item.Id == "storage" && (border is null || border.Contains(item.Position)))
            .SelectMany(item => world.Map.FootNeighbors(item.Position)
                .Where(point => !world.Map.IsDiagonalFootStep(item.Position, point)))
            .Where(point => world.Map.IsBuildable(point) && !occupied.Contains(point))
            .ToHashSet());
        if (network.Count == 0) network.Add(entrances[0]);

        var request = new RoadRouteRequest(world.Map, entrances, network, occupied, world.SavedBridges, world.Transport.Roads, border);
        var result = border is null ? null : RoadRoutePlanner.Plan(request);
        if (result?.Proposal is null)
        {
            request = request with { Border = null };
            result = RoadRoutePlanner.Plan(request);
        }
        var failure = result.Proposal is { } proposal ? RoadRoutePlanner.Validate(request, proposal) : result.Outcome;
        if (failure is not null)
        {
            world.AppendEvent(new TownRoadUnconnectedEventPayload(null, building.TownId, building.InstanceId, failure));
            return laid;
        }
        var route = result.Proposal!;
        var routeId = world.RoadRouteId(building.TownId!, building.InstanceId);
        foreach (var tile in route.RoadTiles)
            if (world.Transport.LayRoadTile(tile)) laid.Add(tile);
        // The route starts at the entrance it was laid from, so the door faces this Road.
        world.SetBuildingEntrance(building.InstanceId, route.RoadTiles[0]);
        if (laid.Count > 0)
            world.AppendEvent(new TownRoadGeneratedEventPayload(null, building.TownId, building.InstanceId, laid.Count));
        CommitRoadBridges(world, route.NewCrossings, routeId);

        var (extended, crossings) = ExtendStreetsPastDoors(world, building, occupied);
        if (extended.Count == 0) return laid;
        var roadsAfter = world.Transport.Roads.Concat(extended).ToHashSet();
        if (RoadRoutePlanner.ValidateGrowth(world.Map, occupied, roadsAfter, world.SavedBridges, extended, crossings) is { } refused)
        {
            world.AppendEvent(new TownRoadExtensionRefusedEventPayload(null, building.TownId, building.InstanceId, refused));
            return laid;
        }
        world.Transport.ExtendRoads(extended);
        world.AppendEvent(new TownRoadExtendedEventPayload(null, building.TownId, building.InstanceId, extended.Count));
        CommitRoadBridges(world, crossings, routeId);
        laid.AddRange(extended);
        return laid;
    }

    internal static (List<GridPoint> Tiles, List<RiverCrossing> Crossings) ExtendStreetsPastDoors(IRoadWorld world, PlacedBuilding building,
        HashSet<GridPoint> occupied)
    {
        var extended = new List<GridPoint>();
        var crossings = new List<RiverCrossing>();
        var existing = world.Transport.Bridges.Select(RiverBridgeRules.ToCrossing).ToArray();
        var working = world.Transport.Roads.ToHashSet();
        var doors = world.Buildings.Where(item => item.Entrance is not null &&
                !StreetLanternContent.IsLantern(item.DefinitionId))
            .Select(item => item.Entrance!.Value).ToHashSet();
        var townBorder = building.TownId is { } townId ? world.Towns.Single(item => item.Id == townId).BorderTiles.ToHashSet() : null;
        var ordered = working.Where(point => townBorder is null || townBorder.Contains(point))
            .OrderBy(point => point.Y).ThenBy(point => point.X).ToArray();
        var streets = new TownStreets(world.Map, occupied, working);
        var random = Pcg32XshRrV1.Create(world.Seed, $"town-streets/{building.InstanceId}");
        foreach (var end in ordered)
        {
            var linked = LinkedRoads(world.Map, working, end, existing, crossings).ToArray();
            if (linked.Length != 1 || StepsToDoor(world.Map, working, end, doors, existing, crossings) is not { } steps ||
                steps >= TownStreets.RunOnTiles)
                continue;
            var path = streets.Wander(end, RoadHeading(world.Map, linked[0], end, existing, crossings),
                TownStreets.RunOnTiles - steps, 0, random,
                crossRiver: (from, direction) => RunOnCrossing(world, from, direction, occupied, existing, crossings),
                crossings: crossings);
            foreach (var tile in path)
                if (working.Add(tile)) extended.Add(tile);
        }
        return (extended, crossings);
    }

    internal static RiverCrossing? RunOnCrossing(IRoadWorld world, GridPoint from, int direction, HashSet<GridPoint> occupied,
        IReadOnlyList<RiverCrossing> existing, IReadOnlyList<RiverCrossing> pending)
    {
        var (dx, dy) = TownStreets.Directions[direction];
        if (dx != 0 && dy != 0 || !RiverBridgeRules.TryFindCrossing(world.Map, from, dx, dy, out var crossing))
            return null;
        if (crossing!.Entrances.Any(occupied.Contains) || pending.Any(item => item.Span.Intersect(crossing.Span).Any()) ||
            RiverBridgeRules.IsRedundant(world.Map, crossing, existing.Concat(pending)))
            return null;
        return crossing;
    }
}
