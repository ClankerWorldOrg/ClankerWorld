using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private sealed partial class CivicWorldAdapter(WorldRuntime runtime) : ITownWorld
    {
        public long WorldTick => runtime.WorldTick;
        public bool HasSurvivalState => runtime.survivalState is not null;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public bool IsEdibleFood(string kind) => WorldRuntime.IsEdibleFood(kind);
        public int AvailableLotQuantity(InventoryLot lot) => runtime.AvailableLotQuantity(lot);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);
        public void AppendEvent(WorldEventPayload payload) => runtime.AppendEvent(payload);
    }
}
