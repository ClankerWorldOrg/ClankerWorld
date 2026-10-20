using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IFamilyMedicalWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    int ResourceInteractionRange { get; }
    bool IsAdult(string actor);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    IEnumerable<GoodsMatch> MedicalDoseStock(string actor);
    bool HasCarriedOwnItem(string actor, string kind);
    InventoryLot? SharedPreparationItem(string kind, string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    OwnerQueuedInstruction? PendingInstructionFor(string actor);
    GoodsMatch? RecheckGoods(GoodsRequest request, string lot);
    void MarkCurrentSchema();
    void AppendEvent(WorldEventPayload payload);
}

internal interface IFamilyTreatmentWorld : IFamilyMedicalWorld
{
    long NextEventId { get; }
    string MedicalDoseId(long tick, long eventId, string caregiver, string patient);
    ActionCheck CheckActionPlan(ActionPlan plan);
    void ConsumeMedicalDose(string id, InventoryLot supply, string caregiver, string patient, string kind);
    void CloseMedicalDoseReceipt(Identity reservation, string purpose);
}

internal interface IFamilySupplyWorld : IFamilyMedicalWorld
{
    int FreeCarryCapacity(string actor);
    InventoryLot? SharedItem(string kind, string actor, GridPoint destination, int range);
    GridPoint HouseholdStockPosition(InventoryLot lot);
    int HouseholdStockInteractionRange(InventoryLot lot);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    ActionCheck CheckEquipmentPickup(string actor, PlannedEquipmentPickup pickup, string description, ReachPurpose purpose);
    bool CanPrepareSharedPickup(string actor, InventoryLot lot, GridPoint destination, int range);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range);
    void ApplyPlannedEquipmentPickup(string actor, PlannedEquipmentPickup pickup);
}
