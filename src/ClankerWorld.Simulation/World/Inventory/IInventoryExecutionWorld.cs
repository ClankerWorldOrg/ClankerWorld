using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

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
