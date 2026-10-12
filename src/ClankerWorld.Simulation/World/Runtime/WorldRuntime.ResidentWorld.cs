using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private sealed class ResidentWorldAdapter(WorldRuntime runtime) : IResidentWorld
    {
        public long WorldTick => runtime.WorldTick;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public GridPoint SettlementStoragePosition => runtime.SettlementStoragePosition;
        public GridPoint BuildingPosition(string id) => runtime.worldSimulation.Buildings.Single(building => building.InstanceId == id).Position;
        public int FootDistance(GridPoint origin, GridPoint destination) => runtime.map.FootDistance(origin, destination);
        public int WarmthChange(AgentState person) => runtime.WarmthChange(person);
        public GridPoint? LivingPosition(string id) => runtime.agents.TryGetValue(id, out var person) ? person.Position : null;
        public GridPoint? DeceasedPosition(string id) => runtime.deceasedAgents.TryGetValue(id, out var person) ? person.LastPhysical.Position : null;
    }
}
