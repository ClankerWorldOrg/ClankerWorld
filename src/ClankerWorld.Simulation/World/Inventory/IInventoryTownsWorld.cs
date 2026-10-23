using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

internal interface IInventoryTownsWorld
{
    InventoryCheckpoint ReserveTownProjectDelivery(InventoryCheckpoint inventory, string townId, TownProject project, TownProjectDelivery delivery);
    long WorldTick { get; }
}
