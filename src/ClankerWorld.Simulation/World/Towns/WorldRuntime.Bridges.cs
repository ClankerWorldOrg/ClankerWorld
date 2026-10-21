using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IReadOnlyList<BridgeState> bridges => transportSystem.Bridges;
    private BridgeTrafficState bridgeTraffic => transportSystem.Traffic;
    private IReadOnlySet<GridPoint> roadBridgeDecks => transportSystem.RoadDecks;

    public IReadOnlyList<BridgeState> Bridges => bridges.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();

    public BridgeTrafficState BridgeTraffic => bridgeTraffic;

    /// <summary>Rebuilds the passable decks that movement reads from the saved bridges.</summary>
    private void ApplyBridgeDecks()
    {
        var decks = RiverBridgeRules.Decks(bridges);
        if (!RiverBridgeRules.SameDecks(map.BridgeDecks, decks)) map = map with { BridgeDecks = decks };
        transportSystem.RebuildRoadDecks();
    }

    /// <summary>Physical occupancy that even an existing Road cannot cut diagonally past.</summary>
    private HashSet<GridPoint> RoadOccupiedTiles() =>
        TransportSystem.RoadOccupiedTiles(RoadWorld);

    /// <summary>Road tiles and bridge entrances, which buildings and new resources must leave clear.</summary>
    private IEnumerable<GridPoint> RoadAndBridgeTiles() =>
        Ground.RoadsAndBridgeEntrances;

    /// <summary>Counts one committed foot step toward the separate traffic bridge trigger.</summary>
    private void RecordBridgeTraffic(string agentId, GridPoint from, GridPoint to) =>
        TransportSystem.RecordBridgeTraffic(RoadWorld, agentId, from, to);

    /// <summary>
    /// End-of-tick traffic check: expire old evidence, then build a bridge at
    /// each crossing that reached the threshold, unless a bank is occupied or
    /// an existing bridge already joins the same banks. It never paints Roads.
    /// </summary>
    private void AdvanceBridgeTraffic() =>
        TransportSystem.AdvanceBridgeTraffic(RoadWorld);

    private static void ValidateBridges(
        IReadOnlyList<BridgeState> savedBridges,
        BridgeTrafficState traffic,
        WorldMap map,
        IReadOnlyList<GridPoint> roads,
        WorldContentSimulationState simulation,
        DeclarativeWorldContentState content,
        SocietyCheckpoint society,
        IEnumerable<AgentState> agents) =>
        TransportSystem.ValidateBridges(savedBridges, traffic, map, roads, simulation, content, society, agents);
}
