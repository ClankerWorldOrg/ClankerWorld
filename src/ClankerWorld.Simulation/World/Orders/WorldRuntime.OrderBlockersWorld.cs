namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderBlockersWorld OrderBlockersWorld => new OrderBlockersAdapter(this);

    private sealed class OrderBlockersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IOrderBlockersWorld
    {
        public string BoatOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.BoatOrderBlockedReason(instruction);
        public string? KnowledgeReadOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.KnowledgeReadOrderBlocker(instruction);
        public string? TalkOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.TalkOrderBlocker(instruction, person);
        public string TreePlantingOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.TreePlantingOrderBlockedReason(Runtime.TreePlantingOrdersWorld, instruction, person);
        public string KnowledgeOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.KnowledgeOrderBlockedReason(Runtime.KnowledgeOrdersWorld, instruction);
        public string? CartOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.CartOrderBlocker(instruction, person);
        public string AnimalOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.AnimalOrderBlockedReason(instruction);
        public string ShelterOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ShelterOrderBlockedReason(instruction, person);
        public string ConstructionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ConstructionOrderBlockedReason(Runtime.ConstructionOrdersWorld, instruction, person);
        public string ExpansionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.ExpansionOrderBlockedReason(Runtime.ExpansionOrdersWorld, instruction);
        public string DeliveryOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.DeliveryOrderBlockedReason(instruction, person);
        public string ProductionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.ProductionOrderBlockedReason(Runtime.ProductionOrdersWorld, instruction);
        public string FieldOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.FieldOrderBlockedReason(instruction);
        public string ToolRepairOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.ToolRepairOrderBlockedReason(Runtime.ToolRepairOrdersWorld, instruction);
        public string RepairOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.RepairOrderBlockedReason(instruction);
        public string CollectionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.CollectionOrderBlockedReason(instruction, person);
        public string StorageOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.StorageOrderBlockedReason(instruction, person);
        public string ReturnOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ReturnOrderBlockedReason(instruction, person);
        public string MaterialOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.MaterialOrderBlockedReason(Runtime.MaterialOrdersWorld, instruction, person);
        public bool MovementOrderNeedsHouseInvitation(string actor, global::ClankerWorld.Simulation.World.GridPoint destination) => Runtime.MovementOrderNeedsHouseInvitation(actor, destination);
        public string GuardianOrderBlockedReason(string adult, string child) => Runtime.GuardianOrderBlockedReason(adult, child);
        public int ComfortableFullness => WorldRuntime.ComfortableFullness;
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? AvailableSharedFood(string actor, string? requiredItemKind = null) => Runtime.AvailableSharedFood(actor, requiredItemKind);
        public int FoodHarvestCarryUnits(global::ClankerWorld.Simulation.World.MapResource source) => WorldRuntime.FoodHarvestCarryUnits(source);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> BuildingsWithTag(string tag) => Runtime.BuildingsWithTag(tag);
        public bool HasHouseGuestInvitation(string actor, string houseId) => Runtime.HasHouseGuestInvitation(actor, houseId);
    }
}
