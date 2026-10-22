namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderBlockersWorld OrderBlockersWorld => new OrderBlockersAdapter(this);

    private sealed class OrderBlockersAdapter(WorldRuntime runtime) : IOrderBlockersWorld
    {
        public string BoatOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.BoatOrderBlockedReason(instruction);
        public string? KnowledgeReadOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.KnowledgeReadOrderBlocker(instruction);
        public string? TalkOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.TalkOrderBlocker(instruction, person);
        public string TreePlantingOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.TreePlantingOrderBlockedReason(runtime.TreePlantingOrdersWorld, instruction, person);
        public string KnowledgeOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.KnowledgeOrderBlockedReason(instruction);
        public bool IsCartOrder(string action) => WorldRuntime.IsCartOrder(action);
        public string? CartOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.CartOrderBlocker(instruction, person);
        public bool IsAnimalOrder(string action) => WorldRuntime.IsAnimalOrder(action);
        public string AnimalOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.AnimalOrderBlockedReason(instruction);
        public string ShelterOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ShelterOrderBlockedReason(instruction, person);
        public string ConstructionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ConstructionOrderBlockedReason(instruction, person);
        public string ExpansionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.ExpansionOrderBlockedReason(instruction);
        public string DeliveryOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.DeliveryOrderBlockedReason(instruction, person);
        public string ProductionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.ProductionOrderBlockedReason(runtime.ProductionOrdersWorld, instruction);
        public string FieldOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.FieldOrderBlockedReason(instruction);
        public string ToolRepairOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.ToolRepairOrderBlockedReason(runtime.ToolRepairOrdersWorld, instruction);
        public string RepairOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.RepairOrderBlockedReason(instruction);
        public string CollectionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.CollectionOrderBlockedReason(instruction, person);
        public string StorageOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.StorageOrderBlockedReason(instruction, person);
        public string ReturnOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ReturnOrderBlockedReason(instruction, person);
        public string MaterialOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.MaterialOrderBlockedReason(runtime.MaterialOrdersWorld, instruction, person);
        public global::ClankerWorld.Simulation.World.WorldMap Map => runtime.map;
        public bool MovementOrderNeedsHouseInvitation(string actor, global::ClankerWorld.Simulation.World.GridPoint destination) => runtime.MovementOrderNeedsHouseInvitation(actor, destination);
        public string GuardianOrderBlockedReason(string adult, string child) => runtime.GuardianOrderBlockedReason(adult, child);
        public int ComfortableFullness => WorldRuntime.ComfortableFullness;
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? AvailableSharedFood(string actor, string? requiredItemKind = null) => runtime.AvailableSharedFood(actor, requiredItemKind);
        public global::ClankerWorld.Simulation.World.MapResource? KnownFoodSourceForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.KnownFoodSourceForOrder(instruction, person);
        public int FoodHarvestCarryUnits(global::ClankerWorld.Simulation.World.MapResource source) => WorldRuntime.FoodHarvestCarryUnits(source);
        public bool CanExploreForOrder(global::ClankerWorld.Simulation.World.AgentState person) => runtime.CanExploreForOrder(person);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> BuildingsWithTag(string tag) => runtime.BuildingsWithTag(tag);
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public bool HasHouseGuestInvitation(string actor, string houseId) => runtime.HasHouseGuestInvitation(actor, houseId);
        public global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content => runtime.worldContent;
    }
}
