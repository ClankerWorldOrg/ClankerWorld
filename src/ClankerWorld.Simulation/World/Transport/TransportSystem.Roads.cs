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

    /// <summary>
    /// Joins a building that has joined a Town to the Road network and returns
    /// the new Road tiles. A building beside a Road faces it and needs no new
    /// Road; otherwise a new street runs from its best entrance to the nearest
    /// Road, inside the Town border where it can, crossing a river up to two
    /// tiles wide on a new bridge where its route needs one. Either way,
    /// streets near its door then run on past it
    /// (<see cref="ExtendStreetsPastDoors"/>). Each proposal is validated whole
    /// before any Road tile or bridge is saved.
    /// </summary>
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

    internal static int RoadHeading(WorldMap map, GridPoint from, GridPoint to,
        IReadOnlyList<RiverCrossing> existing, IReadOnlyList<RiverCrossing> pending)
    {
        foreach (var crossing in existing.Concat(pending))
        {
            var fromA = crossing.EntranceA == from && crossing.EntranceB == to;
            var fromB = crossing.EntranceB == from && crossing.EntranceA == to;
            if (!fromA && !fromB) continue;
            // A to B is east or south. The shortest wrapped delta can point
            // backwards across the same bridge on a narrow map.
            var direction = crossing.Axis == BridgeAxis.EastWest ? 0 : 2;
            return fromA ? direction : TownStreets.Turn(direction, 4);
        }
        return TownStreets.DirectionBetween(map, from, to);
    }

    internal static IEnumerable<GridPoint> LinkedRoads(WorldMap map, IReadOnlySet<GridPoint> roads, GridPoint tile,
        IReadOnlyList<RiverCrossing> existing, IReadOnlyList<RiverCrossing> pending)
    {
        foreach (var next in TownStreets.Linked(map, roads, tile)) yield return next;
        foreach (var crossing in existing.Concat(pending))
        {
            if (crossing.EntranceA == tile && roads.Contains(crossing.EntranceB)) yield return crossing.EntranceB;
            else if (crossing.EntranceB == tile && roads.Contains(crossing.EntranceA)) yield return crossing.EntranceA;
        }
    }

    internal static int? StepsToDoor(WorldMap map, IReadOnlySet<GridPoint> roads, GridPoint start, HashSet<GridPoint> doors,
        IReadOnlyList<RiverCrossing> existing, IReadOnlyList<RiverCrossing> pending)
    {
        var steps = new Dictionary<GridPoint, int> { [start] = 0 };
        var queue = new Queue<GridPoint>([start]);
        while (queue.TryDequeue(out var current))
        {
            if (doors.Contains(current)) return steps[current];
            if (steps[current] >= TownStreets.RunOnTiles) continue;
            foreach (var next in LinkedRoads(map, roads, current, existing, pending))
                if (steps.TryAdd(next, steps[current] + 1)) queue.Enqueue(next);
        }
        return null;
    }

    internal static bool IsRoadSurface(TransportSystem state, GridPoint point) => state.Roads.Contains(point) || state.RoadDecks.Contains(point);
    internal static int RoadStepCost(WorldMap map, TransportSystem state, GridPoint from, GridPoint to) => RoadAdjustedStepCost(state, from, to, map.FootStepCost(from, to));
    internal static int RoadAdjustedStepCost(TransportSystem state, GridPoint from, GridPoint to, int cost) => RoadRoutePlanner.AdjustFootCost(cost, IsRoadSurface(state, from) && IsRoadSurface(state, to));

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

    internal static HashSet<GridPoint> RoadBlockedTiles(IRoadWorld world) =>
        RoadOccupiedTiles(world).Concat(world.HouseholdLandHeldByOthers(null)).ToHashSet();

    internal static HashSet<GridPoint> RoadOccupiedTiles(IRoadWorld world)
    {
        var ground = world.Ground;
        return GroundOccupancy.Combine(ground.Resources, ground.Fields, ground.MapObjects,
            ground.BuildingFootprints, ground.ExpansionSites, ground.ProjectFootprints(), ground.MarketStallSlots);
    }

    internal static void CommitRoadBridges(IRoadWorld world, IReadOnlyList<RiverCrossing> crossings, string routeId)
    {
        if (crossings.Count == 0) return;
        var built = crossings
            .Select(crossing => RiverBridgeRules.ToBridge(crossing, BridgeTriggers.Road, world.WorldTick, routeId))
            .ToArray();
        foreach (var bridge in built)
        {
            world.Transport.AddRoadBridge(bridge);
        }
        world.ApplyBridgeDecks();
        foreach (var bridge in built)
            world.AppendEvent(new BridgeBuiltEventPayload(null, BridgeTriggers.Road, bridge.Id, routeId));
    }

    /// <summary>Counts one committed foot step toward the separate traffic bridge trigger.</summary>
    internal static void RecordBridgeTraffic(IRoadWorld world, string agentId, GridPoint from, GridPoint to)
    {
        var next = BridgeTrafficRules.RecordStep(world.Transport.Traffic, world.Map, agentId, from, to, world.WorldTick);
        if (ReferenceEquals(next, world.Transport.Traffic) ||
            next.InProgress.SequenceEqual(world.Transport.Traffic.InProgress) && next.Completed.SequenceEqual(world.Transport.Traffic.Completed))
            return;
        world.Transport.RecordBridgeStep(next);
    }

    /// <summary>
    /// End-of-tick traffic check: expire old evidence, then build a bridge at
    /// each crossing that reached the threshold, unless a bank is occupied or
    /// an existing bridge already joins the same banks. It never paints Roads.
    /// </summary>
    internal static void AdvanceBridgeTraffic(IRoadWorld world)
    {
        if (world.Transport.Traffic.IsEmpty) return;
        var positions = world.Agents.ToDictionary(item => item.Key, item => item.Value.Position, StringComparer.Ordinal);
        var pruned = BridgeTrafficRules.Prune(world.Transport.Traffic, world.Map, world.WorldTick,
            world.TicksPerWorldDay, positions);
        if (!ReferenceEquals(pruned, world.Transport.Traffic))
        {
            world.Transport.PruneBridgeTraffic(pruned);
        }
        var ready = BridgeTrafficRules.ReadyCrossings(world.Transport.Traffic);
        if (ready.Count == 0) return;
        var blocked = RoadBlockedTiles(world);
        foreach (var id in ready)
        {
            world.Transport.ForgetBridgeTraffic(id);
            if (!RiverBridgeRules.TryResolve(world.Map, id, out var crossing)) continue;
            if (crossing!.Entrances.Any(blocked.Contains))
            {
                world.AppendEvent(new TrafficBridgeNotBuiltEventPayload(null, id, "occupied_bank"));
                continue;
            }
            if (RiverBridgeRules.IsRedundant(world.Map, crossing, world.Transport.Bridges.Select(RiverBridgeRules.ToCrossing)))
            {
                world.AppendEvent(new TrafficBridgeNotBuiltEventPayload(null, id, "same_banks_bridged"));
                continue;
            }
            world.Transport.AddTrafficBridge(RiverBridgeRules.ToBridge(crossing, BridgeTriggers.Traffic, world.WorldTick, routeId: null));
            world.ApplyBridgeDecks();
            world.AppendEvent(new BridgeBuiltEventPayload(null, BridgeTriggers.Traffic, id, null));
        }
    }
}
