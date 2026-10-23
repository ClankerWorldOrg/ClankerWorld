namespace ClankerWorld.Simulation.World;

internal interface IInventoryTransportWorld
{
    long WorldTick { get; }
    string CartBuildMaterialTransferId(long tick, string actor);
    string CartRepairReservationId(long tick, string actor, string material);
}
