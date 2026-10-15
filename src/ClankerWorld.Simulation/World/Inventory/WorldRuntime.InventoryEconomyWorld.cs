using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryEconomyWorld InventoryEconomyWorld => new InventoryEconomyAdapter(this);
    private sealed class InventoryEconomyAdapter(WorldRuntime runtime) : IInventoryEconomyWorld
    {
        public long WorldTick => runtime.WorldTick;
        public AgentStore agentStore => runtime.agentStore;
        public IReadOnlyDictionary<string, AgentState> agents => runtime.agents;
        public long nextEventId => runtime.nextEventId;
        public string EquipmentTransferId(long tick, string actor, string lotId) => WorldRuntime.EquipmentTransferId(tick, actor, lotId);
        public string EquippedUnitId(long tick, string actor, string lotId) => WorldRuntime.EquippedUnitId(tick, actor, lotId);
        public string EquipmentRepairReservationId(long tick, string actor, string material) => WorldRuntime.EquipmentRepairReservationId(tick, actor, material);
        public string OrnamentUnitId(long tick, long eventId, string actor, string lotId) => WorldRuntime.OrnamentUnitId(tick, eventId, actor, lotId);
        public InventoryReservation? OrchardReservationForFood(InventoryCheckpoint inventory, string actor, InventoryLot lot) => runtime.OrchardReservationForFood(inventory, actor, lot);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public IEnumerable<InventoryLot> PreferredFood(string owner, string? actor = null) => runtime.PreferredFood(owner, actor);
        public InventoryGroundPosition ExpansionGroundPosition(PlacedBuilding building) => WorldRuntime.ExpansionGroundPosition(building);
    }
}
