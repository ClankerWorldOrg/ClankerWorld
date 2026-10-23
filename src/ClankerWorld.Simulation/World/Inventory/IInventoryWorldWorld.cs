using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

internal interface IInventoryWorldWorld
{
    string UsedToolLotId(string lotId, long tick, string actor);
    long WorldTick { get; }
    string MaterialLotId(long tick, string actor);
    InventoryCheckpoint MarkBuildingMaterialDelivery(InventoryCheckpoint inventory, string lotId, string? deliveryBuildingId);
    string TreeSeedLotId(long tick, string actor);
    InventoryCheckpoint ApplyToolWorkToInventory(InventoryCheckpoint inventory, string actor, long tick, IReadOnlyList<ToolWorkPlan> plans);
    string RepairedToolLotId(string lotId, long tick, string actor);
    string ToolRepairReservationId(long tick, string actor, string repairedId, string material);
    string ToolRepairGiftId(long tick, string actor, string repairedId, string material);
}
