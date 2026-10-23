using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryExecutionWorld InventoryExecutionWorld => new InventoryExecutionAdapter(this);
    private sealed class InventoryExecutionAdapter(WorldRuntime runtime) : IInventoryExecutionWorld
    {
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public IReadOnlyList<TownRuntimeState> Towns => runtime.towns;
        public WorldContentSimulationState Simulation => runtime.worldSimulation;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public int ReservedShopStorageSpace(string building, InventoryCheckpoint inventory) => runtime.marketSystem.ReservedShopStorageSpace(runtime.MarketShopWorld, building, inventory);
        public int ReservedShopCarrySpace(string actor, InventoryCheckpoint inventory) => runtime.marketSystem.ReservedShopCarrySpace(runtime.MarketShopWorld, actor, inventory);
        public int HorseCargoCapacity(string actor) => runtime.HorseCargoCapacity(actor);
    }
}
