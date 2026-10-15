using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderSelectionWorld OrderSelectionWorld => new OrderSelectionAdapter(this);

    private sealed class OrderSelectionAdapter(WorldRuntime runtime) : IOrderSelectionWorld
    {
        public WorldContentSimulationState Simulation => runtime.worldSimulation;
        public WorldMap Map => runtime.map;
        public int ComfortableFullness => WorldRuntime.ComfortableFullness;
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public IReadOnlyDictionary<string, ResourceState> Resources => runtime.resources;
        public WorldKnowledgeState Knowledge => runtime.knowledge;
        public int OutingFullnessReserve => WorldRuntime.OutingFullnessReserve;
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public bool MayFollowOwnerOrder(string actor, OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? KnowledgeReadOrderCandidate(OwnerQueuedInstruction instruction) => runtime.KnowledgeReadOrderCandidate(instruction);
        public bool IsConversationOrder(string action) => WorldRuntime.IsConversationOrder(action);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? TalkOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.TalkOrderCandidateFor(instruction, person);
        public bool IsCartOrder(string action) => WorldRuntime.IsCartOrder(action);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? CartOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.CartOrderCandidateFor(instruction, person);
        public bool IsAnimalOrder(string action) => WorldRuntime.IsAnimalOrder(action);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? AnimalOrderCandidate(OwnerQueuedInstruction instruction) => runtime.AnimalOrderCandidate(instruction);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? BoatOrderCandidate(OwnerQueuedInstruction instruction) => runtime.BoatOrderCandidate(instruction);
        public bool IsTreePlantingOrder(string action) => WorldRuntime.IsTreePlantingOrder(action);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? TreePlantingOrderCandidate(OwnerQueuedInstruction instruction, AgentState person) => runtime.TreePlantingOrderCandidate(instruction, person);
        public bool IsKnowledgeOrder(string action) => WorldRuntime.IsKnowledgeOrder(action);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? KnowledgeOrderCandidateFor(OwnerQueuedInstruction instruction) => runtime.KnowledgeOrderCandidateFor(instruction);
        public bool IsShelterOrder(string action) => WorldRuntime.IsShelterOrder(action);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? ShelterOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.ShelterOrderCandidateFor(instruction, person);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? ConstructionOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.ConstructionOrderCandidateFor(instruction, person);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? ExpansionOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.ExpansionOrderCandidateFor(instruction, person);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? DeliveryOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.DeliveryOrderCandidateFor(instruction, person);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? ProductionOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.ProductionOrderCandidateFor(instruction, person);
        public bool IsFieldOrder(string action) => WorldRuntime.IsFieldOrder(action);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? FieldOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.FieldOrderCandidateFor(instruction, person);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? ToolRepairOrderCandidateFor(OwnerQueuedInstruction instruction) => runtime.ToolRepairOrderCandidateFor(instruction);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? RepairOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.RepairOrderCandidateFor(instruction, person);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? CollectionOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.CollectionOrderCandidateFor(instruction, person);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? StorageOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.StorageOrderCandidateFor(instruction, person);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? ReturnOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.ReturnOrderCandidateFor(instruction, person);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? MaterialOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.MaterialOrderCandidateFor(instruction, person);
        public bool MovementOrderNeedsHouseInvitation(string actor, GridPoint destination) => runtime.MovementOrderNeedsHouseInvitation(actor, destination);
        public bool CanSwim(string actor, GridPoint? origin = null, int additionalCarriedUnits = 0) => runtime.CanSwim(actor, origin, additionalCarriedUnits);
        public System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? GuardianOrderCandidate(string adult, string child) => runtime.GuardianOrderCandidate(adult, child);
        public System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.Kernel.InventoryLot> PreferredFood(string owner, string? actor = null) => runtime.PreferredFood(owner, actor);
        public ClankerWorld.Simulation.Kernel.InventoryLot? AvailableSharedFood(string actor, string? requiredItemKind = null) => runtime.AvailableSharedFood(actor, requiredItemKind);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public int FoodHarvestCarryUnits(MapResource source) => WorldRuntime.FoodHarvestCarryUnits(source);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public string FoodArrivalReceipt(string resourceId, GridPoint position) => WorldRuntime.FoodArrivalReceipt(resourceId, position);
        public string FoodKnowledgeKind(MapResource resource) => WorldRuntime.FoodKnowledgeKind(resource);
        public bool CanReachTerrain(string actor, GridPoint target, ReachPurpose purpose, int load, SwimmingPolicy swimming) => runtime.CanReachTerrain(actor, target, purpose, load, swimming);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);
        public bool HasWarmthForOuting(AgentState person) => runtime.HasWarmthForOuting(person);
    }
}
