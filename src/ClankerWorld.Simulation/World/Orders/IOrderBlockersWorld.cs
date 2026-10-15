using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IOrderBlockersWorld
{
    WorldMap Map { get; }
    int ComfortableFullness { get; }
    SocietyCheckpoint Society { get; }
    DeclarativeWorldContentState Content { get; }
    string BoatOrderBlockedReason(OwnerQueuedInstruction instruction);
    string? KnowledgeReadOrderBlocker(OwnerQueuedInstruction instruction);
    bool IsConversationOrder(string action);
    string? TalkOrderBlocker(OwnerQueuedInstruction instruction, AgentState person);
    bool IsTreePlantingOrder(string action);
    string TreePlantingOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person);
    bool IsKnowledgeOrder(string action);
    string KnowledgeOrderBlockedReason(OwnerQueuedInstruction instruction);
    bool IsCartOrder(string action);
    string? CartOrderBlocker(OwnerQueuedInstruction instruction, AgentState person);
    bool IsAnimalOrder(string action);
    string AnimalOrderBlockedReason(OwnerQueuedInstruction instruction);
    bool IsShelterOrder(string action);
    string ShelterOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person);
    string ConstructionOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person);
    string ExpansionOrderBlockedReason(OwnerQueuedInstruction instruction);
    string DeliveryOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person);
    string ProductionOrderBlockedReason(OwnerQueuedInstruction instruction);
    bool IsFieldOrder(string action);
    string FieldOrderBlockedReason(OwnerQueuedInstruction instruction);
    string ToolRepairOrderBlockedReason(OwnerQueuedInstruction instruction);
    string RepairOrderBlockedReason(OwnerQueuedInstruction instruction);
    string CollectionOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person);
    string StorageOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person);
    string ReturnOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person);
    string MaterialOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person);
    string GuardianOrderBlockedReason(string adult, string child);
    int FreeCarryCapacity(string actor);
    ClankerWorld.Simulation.Kernel.InventoryLot? AvailableSharedFood(string actor, string? requiredItemKind = null);
    ClankerWorld.Simulation.World.MapResource? KnownFoodSourceForOrder(OwnerQueuedInstruction instruction, AgentState person);
    int FoodHarvestCarryUnits(MapResource source);
    bool CanExploreForOrder(AgentState person);
    System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.World.PlacedBuilding> BuildingsWithTag(string tag);
    bool HasHouseGuestInvitation(string actor, string houseId);
}
