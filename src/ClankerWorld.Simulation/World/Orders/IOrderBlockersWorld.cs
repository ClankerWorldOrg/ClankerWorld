namespace ClankerWorld.Simulation.World;

internal interface IOrderBlockersWorld
{
    string BoatOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    string? KnowledgeReadOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    string? TalkOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string TreePlantingOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string KnowledgeOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool IsCartOrder(string action);
    string? CartOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    bool IsAnimalOrder(string action);
    string AnimalOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    string ShelterOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string ConstructionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string ExpansionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    string DeliveryOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string ProductionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    string FieldOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    string ToolRepairOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    string RepairOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    string CollectionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string StorageOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string ReturnOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string MaterialOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
    bool MovementOrderNeedsHouseInvitation(string actor, global::ClankerWorld.Simulation.World.GridPoint destination);
    string GuardianOrderBlockedReason(string adult, string child);
    int ComfortableFullness { get; }
    int FreeCarryCapacity(string actor);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? AvailableSharedFood(string actor, string? requiredItemKind = null);
    global::ClankerWorld.Simulation.World.MapResource? KnownFoodSourceForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    int FoodHarvestCarryUnits(global::ClankerWorld.Simulation.World.MapResource source);
    bool CanExploreForOrder(global::ClankerWorld.Simulation.World.AgentState person);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> BuildingsWithTag(string tag);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    bool HasHouseGuestInvitation(string actor, string houseId);
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
}
