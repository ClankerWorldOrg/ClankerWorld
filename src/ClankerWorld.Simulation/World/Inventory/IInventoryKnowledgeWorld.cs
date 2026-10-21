using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
namespace ClankerWorld.Simulation.World;

internal interface IInventoryKnowledgeWorld
{
    string WritingMaterialTransferId(long tick, string actor, string kind);
    long WorldTick { get; }
}
