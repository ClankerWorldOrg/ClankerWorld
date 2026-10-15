using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IRoadWorld RoadWorld => new RoadWorldAdapter(this);

    private sealed class RoadWorldAdapter(WorldRuntime runtime) : IRoadWorld
    {
        public TransportSystem Transport => runtime.transportSystem;
        public WorldMap Map => runtime.map;
        public GroundOccupancy Ground => runtime.Ground;
        public IReadOnlyList<BridgeState> SavedBridges => runtime.Bridges;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public IReadOnlyList<PlacedBuilding> Buildings => runtime.worldSimulation.Buildings;
        public IReadOnlyList<BuildingDefinition> Definitions => runtime.worldContent.Buildings;
        public IReadOnlyList<TownRuntimeState> Towns => runtime.towns;
        public string Seed => runtime.worldSeed;
        public long WorldTick => runtime.WorldTick;
        public int TicksPerWorldDay => runtime.society.Checkpoint.Config.TicksPerWorldDay;
        public IEnumerable<GridPoint> HouseholdLandHeldByOthers(string? household) => runtime.HouseholdLandHeldByOthers(household);
        public void AppendEvent(string kind, string detail, GridPoint? position = null) => runtime.AppendEvent(kind, detail, position);
        public void ApplyBridgeDecks() => runtime.ApplyBridgeDecks();
        public void SetBuildingEntrance(string building, GridPoint entrance) => runtime.SetBuildingEntrance(building, entrance);
        public string RoadRouteId(string town, string building) => WorldRuntime.RoadRouteId(town, building);
    }
}
