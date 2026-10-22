namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderSelectionWorld OrderSelectionWorld => new OrderSelectionAdapter(this);

    private sealed class OrderSelectionAdapter(WorldRuntime runtime) : IOrderSelectionWorld
    {
        public global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation => runtime.worldSimulation;
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
        public global::ClankerWorld.Simulation.World.WorldMap Map => runtime.map;
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? KnowledgeReadOrderCandidate(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.KnowledgeReadOrderCandidate(instruction);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? TalkOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.TalkOrderCandidateFor(instruction, person);
        public bool IsCartOrder(string action) => WorldRuntime.IsCartOrder(action);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? CartOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.CartOrderCandidateFor(instruction, person);
        public bool IsAnimalOrder(string action) => WorldRuntime.IsAnimalOrder(action);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? AnimalOrderCandidate(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.AnimalOrderCandidate(instruction);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? BoatOrderCandidate(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.BoatOrderCandidate(instruction);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? TreePlantingOrderCandidate(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.TreePlantingOrderCandidate(runtime.TreePlantingOrdersWorld, instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? KnowledgeOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.KnowledgeOrderCandidateFor(instruction);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ShelterOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ShelterOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ConstructionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ConstructionOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ExpansionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ExpansionOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? DeliveryOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.DeliveryOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ProductionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ProductionOrderCandidateFor(runtime.ProductionOrdersWorld, instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? FieldOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.FieldOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ToolRepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.ToolRepairOrderCandidateFor(runtime.ToolRepairOrdersWorld, instruction);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? RepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.RepairOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? CollectionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.CollectionOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? StorageOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.StorageOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ReturnOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ReturnOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? MaterialOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.MaterialOrderCandidateFor(runtime.MaterialOrdersWorld, instruction, person);
        public bool MovementOrderNeedsHouseInvitation(string actor, global::ClankerWorld.Simulation.World.GridPoint destination) => runtime.MovementOrderNeedsHouseInvitation(actor, destination);
        public bool CanSwim(string actor, global::ClankerWorld.Simulation.World.GridPoint? origin = null, int additionalCarriedUnits = 0) => runtime.CanSwim(actor, origin, additionalCarriedUnits);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? GuardianOrderCandidate(string adult, string child) => runtime.GuardianOrderCandidate(adult, child);
        public int ComfortableFullness => WorldRuntime.ComfortableFullness;
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> PreferredFood(string owner, string? actor = null) => runtime.PreferredFood(owner, actor);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? AvailableSharedFood(string actor, string? requiredItemKind = null) => runtime.AvailableSharedFood(actor, requiredItemKind);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public global::ClankerWorld.Simulation.World.MapResource? KnownFoodSourceForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.KnownFoodSourceForOrder(instruction, person);
        public int FoodHarvestCarryUnits(global::ClankerWorld.Simulation.World.MapResource source) => WorldRuntime.FoodHarvestCarryUnits(source);
        public bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public bool KnownTargetIsUnavailable(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.KnownTargetIsUnavailable(instruction);
        public bool CanExploreForOrder(global::ClankerWorld.Simulation.World.AgentState person) => runtime.CanExploreForOrder(person);
        public string FoodArrivalReceipt(string resourceId, global::ClankerWorld.Simulation.World.GridPoint position) => WorldRuntime.FoodArrivalReceipt(resourceId, position);
        public global::System.Collections.Generic.Dictionary<string, global::ClankerWorld.Simulation.World.ResourceState> Resources => runtime.resources;
        public bool MatchesFoodArrivalReceipt(string? receipt, global::ClankerWorld.Simulation.World.MapResource resource) => runtime.MatchesFoodArrivalReceipt(receipt, resource);
        public string FoodKnowledgeKind(global::ClankerWorld.Simulation.World.MapResource resource) => WorldRuntime.FoodKnowledgeKind(resource);
        public global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge => runtime.knowledge;
        public bool CanReachTerrain(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming) => runtime.CanReachTerrain(actor, target, purpose, load, swimming);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person) => runtime.NeedsUrgentWarmth(person);
        public bool HasWarmthForOuting(global::ClankerWorld.Simulation.World.AgentState person) => runtime.HasWarmthForOuting(person);
        public int OutingFullnessReserve => WorldRuntime.OutingFullnessReserve;
    }
}
