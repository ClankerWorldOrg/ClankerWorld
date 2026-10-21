namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    private readonly List<HandcartHitch> hitches = [];
    private readonly List<BridgeState> bridges = [];
    private readonly HashSet<GridPoint> roads = [];
    internal IReadOnlyList<HandcartHitch> Hitches => hitches;
    internal IReadOnlyList<BridgeState> Bridges => bridges;
    internal IReadOnlySet<GridPoint> Roads => roads;
    internal IReadOnlySet<GridPoint> RoadDecks { get; private set; } = new HashSet<GridPoint>();
    internal BoatTransportState Boats { get; private set; } = BoatTransportState.Empty();
    internal BridgeTrafficState Traffic { get; private set; } = BridgeTrafficState.Empty;

    internal void RestoreVehicles(IEnumerable<HandcartHitch> savedHitches, BoatTransportState boats)
    {
        var restored = savedHitches.ToArray();
        if (SystemInvariantChecks.Enabled) ValidateLocalHitches(restored);
        hitches.Clear();
        hitches.AddRange(restored);
        if (SystemInvariantChecks.Enabled) ValidateLocalBoats(boats);
        Boats = boats;
    }

    internal void RestoreRoads(IEnumerable<GridPoint> savedRoads)
    {
        var restored = savedRoads.ToArray();
        roads.Clear();
        roads.UnionWith(restored);
    }

    internal void RestoreBridges(IEnumerable<BridgeState> savedBridges, BridgeTrafficState traffic)
    {
        var restored = savedBridges.ToArray();
        if (SystemInvariantChecks.Enabled) ValidateLocalBridges(restored);
        bridges.Clear();
        bridges.AddRange(restored);
        if (SystemInvariantChecks.Enabled) ValidateLocalTraffic(traffic);
        Traffic = traffic;
    }

    internal long BeginBoatRequestSequence()
    {
        var sequence = checked(Boats.Sequence + 1);
        Boats = Boats with { Sequence = sequence };
        return sequence;
    }

    internal void RecordBoatRequest(BoatTripRequest request)
    {
        var next = Boats with { Requests = Boats.Requests.Where(item => item.Id != request.Id).Append(request).OrderBy(item => item.Sequence).ToArray() };
        if (SystemInvariantChecks.Enabled) ValidateLocalBoats(next);
        Boats = next;
    }

    internal void CancelBoatRequest(BoatTripRequest request, long tick) => RecordBoatRequest(request with { Status = "cancelled", SettledTick = tick });

    internal void RecordBoat(BoatState boat)
    {
        var next = Boats with { Boats = Boats.Boats.Where(item => item.Id != boat.Id).Append(boat).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray() };
        if (SystemInvariantChecks.Enabled) ValidateLocalBoats(next);
        Boats = next;
    }

    internal void AttachHandcart(string cart, string actor)
    {
        var hitch = new HandcartHitch(cart, actor);
        if (SystemInvariantChecks.Enabled) ValidateLocalHitches(hitches.Append(hitch));
        hitches.Add(hitch);
    }
    internal void DetachHandcart(HandcartHitch hitch) => hitches.Remove(hitch);
    internal void AddRoadBridge(BridgeState bridge)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalBridges(bridges.Append(bridge));
        bridges.Add(bridge);
        Traffic = BridgeTrafficRules.Forget(Traffic, bridge.Id);
    }
    internal void AddTrafficBridge(BridgeState bridge)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalBridges(bridges.Append(bridge));
        bridges.Add(bridge);
    }
    internal void RecordBridgeStep(BridgeTrafficState next)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalTraffic(next);
        Traffic = next;
    }
    internal void PruneBridgeTraffic(BridgeTrafficState next) => RecordBridgeStep(next);
    internal void ForgetBridgeTraffic(string id) => Traffic = BridgeTrafficRules.Forget(Traffic, id);
    internal void RebuildRoadDecks() => RoadDecks = bridges.Where(item => item.Trigger == BridgeTriggers.Road).SelectMany(item => item.Span).ToHashSet();
    internal int RemoveOccupiedRoads(IReadOnlySet<GridPoint> occupied) => roads.RemoveWhere(occupied.Contains);
    internal bool LayRoadTile(GridPoint tile) => roads.Add(tile);
    internal void ExtendRoads(IEnumerable<GridPoint> tiles) => roads.UnionWith(tiles);
    internal int LayRoadLink(IEnumerable<GridPoint> tiles) => tiles.Count(roads.Add);
}
