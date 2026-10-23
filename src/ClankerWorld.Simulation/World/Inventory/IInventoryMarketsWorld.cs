namespace ClankerWorld.Simulation.World;

internal interface IInventoryMarketsWorld
{
    IReadOnlyList<TownRuntimeState> towns { get; }
    long WorldTick { get; }
}
