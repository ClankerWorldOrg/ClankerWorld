using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
namespace ClankerWorld.Simulation.World;

internal interface IInventoryMarketsWorld
{
    IReadOnlyList<TownRuntimeState> towns { get; }
    long WorldTick { get; }
}
