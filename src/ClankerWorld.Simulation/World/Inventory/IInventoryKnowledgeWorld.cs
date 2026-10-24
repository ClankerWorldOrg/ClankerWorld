namespace ClankerWorld.Simulation.World;

internal interface IInventoryKnowledgeWorld
{
    string WritingMaterialTransferId(long tick, string actor, string kind);
    long WorldTick { get; }
}
