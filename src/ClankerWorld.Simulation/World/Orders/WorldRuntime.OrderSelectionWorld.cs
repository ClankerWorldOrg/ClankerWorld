namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderSelectionWorld OrderSelectionWorld => new OrderSelectionAdapter(this);

    private sealed class OrderSelectionAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IOrderSelectionWorld
    {
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? KnowledgeReadOrderCandidate(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.KnowledgeReadOrderCandidate(instruction);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? TalkOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.TalkOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? CartOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.CartOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? AnimalOrderCandidate(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.AnimalOrderCandidate(instruction);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? BoatOrderCandidate(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.BoatOrderCandidate(instruction);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? TreePlantingOrderCandidate(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.TreePlantingOrderCandidate(Runtime.TreePlantingOrdersWorld, instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? KnowledgeOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.KnowledgeOrderCandidateFor(Runtime.KnowledgeOrdersWorld, instruction);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ShelterOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ShelterOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ConstructionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ConstructionOrderCandidateFor(Runtime.ConstructionOrdersWorld, instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ExpansionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ExpansionOrderCandidateFor(Runtime.ExpansionOrdersWorld, instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? DeliveryOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.DeliveryOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ProductionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ProductionOrderCandidateFor(Runtime.ProductionOrdersWorld, instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? FieldOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.FieldOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ToolRepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.ToolRepairOrderCandidateFor(Runtime.ToolRepairOrdersWorld, instruction);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? RepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.RepairOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? CollectionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.CollectionOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? StorageOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.StorageOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ReturnOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ReturnOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? MaterialOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.MaterialOrderCandidateFor(Runtime.MaterialOrdersWorld, instruction, person);
        public bool MovementOrderNeedsHouseInvitation(string actor, global::ClankerWorld.Simulation.World.GridPoint destination) => Runtime.MovementOrderNeedsHouseInvitation(actor, destination);
        public bool CanSwim(string actor, global::ClankerWorld.Simulation.World.GridPoint? origin = null, int additionalCarriedUnits = 0) => Runtime.CanSwim(actor, origin, additionalCarriedUnits);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? GuardianOrderCandidate(string adult, string child) => Runtime.GuardianOrderCandidate(adult, child);
        public int ComfortableFullness => WorldRuntime.ComfortableFullness;
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> PreferredFood(string owner, string? actor = null) => Runtime.PreferredFood(owner, actor);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? AvailableSharedFood(string actor, string? requiredItemKind = null) => Runtime.AvailableSharedFood(actor, requiredItemKind);
        public int FoodHarvestCarryUnits(global::ClankerWorld.Simulation.World.MapResource source) => WorldRuntime.FoodHarvestCarryUnits(source);
        public bool KnownTargetIsUnavailable(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.KnownTargetIsUnavailable(instruction);
        public string FoodArrivalReceipt(string resourceId, global::ClankerWorld.Simulation.World.GridPoint position) => WorldRuntime.FoodArrivalReceipt(resourceId, position);
        public global::System.Collections.Generic.Dictionary<string, global::ClankerWorld.Simulation.World.ResourceState> Resources => Runtime.resources;
        public bool MatchesFoodArrivalReceipt(string? receipt, global::ClankerWorld.Simulation.World.MapResource resource) => Runtime.MatchesFoodArrivalReceipt(receipt, resource);
        public global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge => Runtime.knowledge;
        public bool CanReachTerrain(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming) => Runtime.CanReachTerrain(actor, target, purpose, load, swimming);
        public bool HasWarmthForOuting(global::ClankerWorld.Simulation.World.AgentState person) => Runtime.HasWarmthForOuting(person);
        public int OutingFullnessReserve => WorldRuntime.OutingFullnessReserve;
    }
}
