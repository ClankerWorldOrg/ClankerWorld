using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal interface IInventoryExecutionWorld
{
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    IReadOnlyList<TownRuntimeState> Towns { get; }
    WorldContentSimulationState Simulation { get; }
    DeclarativeWorldContentState Content { get; }
    int ReservedShopStorageSpace(string building, InventoryCheckpoint inventory);
    int ReservedShopCarrySpace(string actor, InventoryCheckpoint inventory);
    int HorseCargoCapacity(string actor);
}
