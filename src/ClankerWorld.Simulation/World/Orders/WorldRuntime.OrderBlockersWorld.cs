using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderBlockersWorld OrderBlockersWorld => new OrderBlockersAdapter(this);

    private sealed class OrderBlockersAdapter(WorldRuntime runtime) : IOrderBlockersWorld
    {
        public WorldMap Map => runtime.map;
        public int ComfortableFullness => WorldRuntime.ComfortableFullness;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public string BoatOrderBlockedReason(OwnerQueuedInstruction instruction) => runtime.BoatOrderBlockedReason(instruction);
        public string? KnowledgeReadOrderBlocker(OwnerQueuedInstruction instruction) => runtime.KnowledgeReadOrderBlocker(instruction);
        public bool IsConversationOrder(string action) => WorldRuntime.IsConversationOrder(action);
        public string? TalkOrderBlocker(OwnerQueuedInstruction instruction, AgentState person) => runtime.TalkOrderBlocker(instruction, person);
        public bool IsTreePlantingOrder(string action) => WorldRuntime.IsTreePlantingOrder(action);
        public string TreePlantingOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) => runtime.TreePlantingOrderBlockedReason(instruction, person);
        public bool IsKnowledgeOrder(string action) => WorldRuntime.IsKnowledgeOrder(action);
        public string KnowledgeOrderBlockedReason(OwnerQueuedInstruction instruction) => runtime.KnowledgeOrderBlockedReason(instruction);
        public bool IsCartOrder(string action) => WorldRuntime.IsCartOrder(action);
        public string? CartOrderBlocker(OwnerQueuedInstruction instruction, AgentState person) => runtime.CartOrderBlocker(instruction, person);
        public bool IsAnimalOrder(string action) => WorldRuntime.IsAnimalOrder(action);
        public string AnimalOrderBlockedReason(OwnerQueuedInstruction instruction) => runtime.AnimalOrderBlockedReason(instruction);
        public bool IsShelterOrder(string action) => WorldRuntime.IsShelterOrder(action);
        public string ShelterOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) => runtime.ShelterOrderBlockedReason(instruction, person);
        public string ConstructionOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) => runtime.ConstructionOrderBlockedReason(instruction, person);
        public string ExpansionOrderBlockedReason(OwnerQueuedInstruction instruction) => runtime.ExpansionOrderBlockedReason(instruction);
        public string DeliveryOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) => runtime.DeliveryOrderBlockedReason(instruction, person);
        public string ProductionOrderBlockedReason(OwnerQueuedInstruction instruction) => runtime.ProductionOrderBlockedReason(instruction);
        public bool IsFieldOrder(string action) => WorldRuntime.IsFieldOrder(action);
        public string FieldOrderBlockedReason(OwnerQueuedInstruction instruction) => runtime.FieldOrderBlockedReason(instruction);
        public string ToolRepairOrderBlockedReason(OwnerQueuedInstruction instruction) => runtime.ToolRepairOrderBlockedReason(instruction);
        public string RepairOrderBlockedReason(OwnerQueuedInstruction instruction) => runtime.RepairOrderBlockedReason(instruction);
        public string CollectionOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) => runtime.CollectionOrderBlockedReason(instruction, person);
        public string StorageOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) => runtime.StorageOrderBlockedReason(instruction, person);
        public string ReturnOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) => runtime.ReturnOrderBlockedReason(instruction, person);
        public string MaterialOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) => runtime.MaterialOrderBlockedReason(instruction, person);
        public string GuardianOrderBlockedReason(string adult, string child) => runtime.GuardianOrderBlockedReason(adult, child);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public ClankerWorld.Simulation.Kernel.InventoryLot? AvailableSharedFood(string actor, string? requiredItemKind = null) => runtime.AvailableSharedFood(actor, requiredItemKind);
        public ClankerWorld.Simulation.World.MapResource? KnownFoodSourceForOrder(OwnerQueuedInstruction instruction, AgentState person) => runtime.KnownFoodSourceForOrder(instruction, person);
        public int FoodHarvestCarryUnits(MapResource source) => WorldRuntime.FoodHarvestCarryUnits(source);
        public bool CanExploreForOrder(AgentState person) => runtime.CanExploreForOrder(person);
        public System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.World.PlacedBuilding> BuildingsWithTag(string tag) => runtime.BuildingsWithTag(tag);
        public bool HasHouseGuestInvitation(string actor, string houseId) => runtime.HasHouseGuestInvitation(actor, houseId);
    }
}
