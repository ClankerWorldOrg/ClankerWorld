using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryWorldWorld InventoryWorldWorld => new InventoryWorldAdapter(this);
    private sealed class InventoryWorldAdapter(WorldRuntime runtime) : IInventoryWorldWorld
    {
        public long WorldTick => runtime.WorldTick;
        public string UsedToolLotId(string lotId, long tick, string actor) => WorldRuntime.UsedToolLotId(lotId, tick, actor);
        public string MaterialLotId(long tick, string actor) => WorldRuntime.MaterialLotId(tick, actor);
        public InventoryCheckpoint MarkBuildingMaterialDelivery(InventoryCheckpoint inventory, string lotId, string? deliveryBuildingId) => WorldRuntime.MarkBuildingMaterialDelivery(inventory, lotId, deliveryBuildingId);
        public string TreeSeedLotId(long tick, string actor) => WorldRuntime.TreeSeedLotId(tick, actor);
        public InventoryCheckpoint ApplyToolWorkToInventory(InventoryCheckpoint inventory, string actor, long tick, IReadOnlyList<ToolWorkPlan> plans) => WorldRuntime.ApplyToolWorkToInventory(inventory, actor, tick, plans);
        public string RepairedToolLotId(string lotId, long tick, string actor) => WorldRuntime.RepairedToolLotId(lotId, tick, actor);
        public string ToolRepairReservationId(long tick, string actor, string repairedId, string material) => WorldRuntime.ToolRepairReservationId(tick, actor, repairedId, material);
        public string ToolRepairGiftId(long tick, string actor, string repairedId, string material) => WorldRuntime.ToolRepairGiftId(tick, actor, repairedId, material);
    }
}
