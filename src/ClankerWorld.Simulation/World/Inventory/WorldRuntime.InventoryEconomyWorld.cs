using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryEconomyWorld InventoryEconomyWorld => new InventoryEconomyAdapter(this);
    private sealed class InventoryEconomyAdapter(WorldRuntime runtime) : IInventoryEconomyWorld
    {
        public long WorldTick => runtime.WorldTick;
        public long nextEventId => runtime.nextEventId;
        public string OrnamentUnitId(long tick, long eventId, string actor, string lotId) => WorldRuntime.OrnamentUnitId(tick, eventId, actor, lotId);
        public AgentStore agentStore => runtime.agentStore;
        public InventoryGroundPosition ExpansionGroundPosition(PlacedBuilding building) => WorldRuntime.ExpansionGroundPosition(building);
        public InventoryReservation? OrchardReservationForFood(InventoryCheckpoint inventory, string actor, InventoryLot lot) => runtime.OrchardReservationForFood(inventory, actor, lot);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public IEnumerable<InventoryLot> PreferredFood(string owner, string? actor = null) => runtime.PreferredFood(owner, actor);
        public string EquipmentRepairReservationId(long tick, string actor, string material) => WorldRuntime.EquipmentRepairReservationId(tick, actor, material);
        public string EquipmentTransferId(long tick, string actor, string lotId) => WorldRuntime.EquipmentTransferId(tick, actor, lotId);
        public string EquippedUnitId(long tick, string actor, string lotId) => WorldRuntime.EquippedUnitId(tick, actor, lotId);
        public IReadOnlyDictionary<string, AgentState> agents => runtime.agents;
    }
}
