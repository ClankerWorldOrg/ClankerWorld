using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
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

    internal static void RecordBridgeTraffic(IRoadWorld world, string agentId, GridPoint from, GridPoint to)
    {
        var next = BridgeTrafficRules.RecordStep(world.Transport.Traffic, world.Map, agentId, from, to, world.WorldTick);
        if (ReferenceEquals(next, world.Transport.Traffic) ||
            next.InProgress.SequenceEqual(world.Transport.Traffic.InProgress) && next.Completed.SequenceEqual(world.Transport.Traffic.Completed))
            return;
        world.Transport.RecordBridgeStep(next);
    }

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
