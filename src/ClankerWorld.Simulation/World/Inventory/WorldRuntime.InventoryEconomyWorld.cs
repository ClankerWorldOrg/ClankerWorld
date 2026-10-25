using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryEconomyWorld InventoryEconomyWorld => new InventoryEconomyAdapter(this);
    private sealed class InventoryEconomyAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IInventoryEconomyWorld
    {
        public long nextEventId => Runtime.nextEventId;
        public string OrnamentUnitId(long tick, long eventId, string actor, string lotId) => WorldRuntime.OrnamentUnitId(tick, eventId, actor, lotId);
        public AgentStore agentStore => Runtime.agentStore;
        public InventoryGroundPosition ExpansionGroundPosition(PlacedBuilding building) => WorldRuntime.ExpansionGroundPosition(building);
        public InventoryReservation? OrchardReservationForFood(InventoryCheckpoint inventory, string actor, InventoryLot lot) => Runtime.OrchardReservationForFood(inventory, actor, lot);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public string EquipmentRepairReservationId(long tick, string actor, string material) => WorldRuntime.EquipmentRepairReservationId(tick, actor, material);
        public string EquipmentTransferId(long tick, string actor, string lotId) => WorldRuntime.EquipmentTransferId(tick, actor, lotId);
        public string EquippedUnitId(long tick, string actor, string lotId) => WorldRuntime.EquippedUnitId(tick, actor, lotId);
        public IReadOnlyDictionary<string, AgentState> agents => Runtime.agents;
    }
}
