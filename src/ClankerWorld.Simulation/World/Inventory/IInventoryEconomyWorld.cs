using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IInventoryEconomyWorld
{
    long WorldTick { get; }
    AgentStore agentStore { get; }
    IReadOnlyDictionary<string, AgentState> agents { get; }
    long nextEventId { get; }
    string EquipmentTransferId(long tick, string actor, string lotId);
    string EquippedUnitId(long tick, string actor, string lotId);
    string EquipmentRepairReservationId(long tick, string actor, string material);
    string OrnamentUnitId(long tick, long eventId, string actor, string lotId);
    InventoryReservation? OrchardReservationForFood(InventoryCheckpoint inventory, string actor, InventoryLot lot);
    bool NeedsUrgentFood(AgentState person);
    IEnumerable<InventoryLot> PreferredFood(string owner, string? actor = null);
    InventoryGroundPosition ExpansionGroundPosition(PlacedBuilding building);
}
