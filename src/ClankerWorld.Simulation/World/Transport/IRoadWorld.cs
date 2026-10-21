using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal interface IRoadWorld
{
    TransportSystem Transport { get; }
    WorldMap Map { get; }
    GroundOccupancy Ground { get; }
    IReadOnlyList<BridgeState> SavedBridges { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    IReadOnlyList<PlacedBuilding> Buildings { get; }
    IReadOnlyList<BuildingDefinition> Definitions { get; }
    IReadOnlyList<TownRuntimeState> Towns { get; }
    string Seed { get; }
    long WorldTick { get; }
    int TicksPerWorldDay { get; }
    IEnumerable<GridPoint> HouseholdLandHeldByOthers(string? household);
    void AppendEvent(WorldEventPayload payload, GridPoint? position = null);
    void ApplyBridgeDecks();
    void SetBuildingEntrance(string building, GridPoint entrance);
    string RoadRouteId(string town, string building);
}
