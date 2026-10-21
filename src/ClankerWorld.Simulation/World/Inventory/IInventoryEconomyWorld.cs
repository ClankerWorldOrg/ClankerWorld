using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Content;
using System.Globalization;
using ClankerWorld.Simulation.Society;
using MaterialGatherEffect = ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
namespace ClankerWorld.Simulation.World;

internal interface IInventoryEconomyWorld
{
    long WorldTick { get; }
    long nextEventId { get; }
    string OrnamentUnitId(long tick, long eventId, string actor, string lotId);
    AgentStore agentStore { get; }
    InventoryGroundPosition ExpansionGroundPosition(PlacedBuilding building);
    InventoryReservation? OrchardReservationForFood(InventoryCheckpoint inventory, string actor, InventoryLot lot);
    bool NeedsUrgentFood(AgentState person);
    IEnumerable<InventoryLot> PreferredFood(string owner, string? actor = null);
    string EquipmentRepairReservationId(long tick, string actor, string material);
    string EquipmentTransferId(long tick, string actor, string lotId);
    string EquippedUnitId(long tick, string actor, string lotId);
    IReadOnlyDictionary<string, AgentState> agents { get; }
}
