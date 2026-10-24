using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private abstract class GoodsRuntimeAdapter(WorldRuntime runtime)
    {
        protected WorldRuntime Runtime { get; } = runtime;

        public long WorldTick => Runtime.WorldTick;
        public void SetOrderStatus(OwnerQueuedInstruction instruction, OrderStatus status, string? reason, bool waitForDecision = false) => Runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, Identity effectId, int units) => Runtime.CreditOrderEffect(instruction, effectId, units);
        public IReadOnlyDictionary<string, AgentState> Agents => Runtime.agents;
        public SocietyCheckpoint Society => Runtime.society.Checkpoint;
        public bool IsAdult(string actor) => Runtime.IsAdult(actor);
        public OrderStore Orders => Runtime.orderStore;
        public void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => Runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public WorldContentSimulationState Simulation => Runtime.worldSimulation;
        public WorldMap Map => Runtime.map;
        public DeclarativeWorldContentState Content => Runtime.worldContent;
        public void AppendEvent(WorldEventPayload payload, GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
        public int FreeCarryCapacity(string actor) => Runtime.FreeCarryCapacity(actor);
        public bool ReadyForBriefInteraction(string actor) => Runtime.ReadyForBriefInteraction(actor);
        public ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = (ReachObstacles)0) => Runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public bool MayFollowOwnerOrder(string actor, OrderTask order) => Runtime.MayFollowOwnerOrder(actor, order);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => Runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public void MarkCheckpointCurrent() => Runtime.checkpointSchemaVersion = StateSchemaVersion;
        public string HouseholdFor(string actor) => Runtime.HouseholdFor(actor);
        public bool NeedsUrgentWarmth(AgentState person) => Runtime.NeedsUrgentWarmth(person);
        public PlacedBuilding? FarmhouseForHousehold(string householdId) => Runtime.FarmhouseForHousehold(householdId);
        public MapResource? KnownFoodSourceForOrder(OwnerQueuedInstruction instruction, AgentState person) => Runtime.KnownFoodSourceForOrder(instruction, person);
        public bool CanExploreForOrder(AgentState person) => Runtime.CanExploreForOrder(person);
        public PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag) => Runtime.HouseholdBuildingWithTag(householdId, tag);
        public AgentStore PhysicalAgents => Runtime.agentStore;
        public IReadOnlyList<TownRuntimeState> Towns => Runtime.towns;
        public IEnumerable<InventoryLot> PreferredFood(string owner, string? actor = null) => Runtime.PreferredFood(owner, actor);
        public bool CanPullHandcart(InventoryLot cart) => TransportSystem.CanPullHandcart(Runtime.HandcartWorld, cart);
        public string? CartOrderBlocker(OwnerQueuedInstruction instruction, AgentState person) => Runtime.CartOrderBlocker(instruction, person);
        public InventoryLot? CartForOrder(OwnerQueuedInstruction instruction, AgentState person) => Runtime.CartForOrder(instruction, person);
        public string? CartCargoOrderBlocker(OwnerQueuedInstruction instruction, AgentState person) => Runtime.CartCargoOrderBlocker(instruction, person);
        public bool ApplyHandcartCandidate(string actor, AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = 2147483647, ActionPlan? originalPlan = null) => Runtime.ApplyHandcartCandidate(actor, person, candidateId, targetCartId, maximumQuantity, originalPlan);
        public string BoatOrderBlockedReason(OwnerQueuedInstruction instruction) => Runtime.BoatOrderBlockedReason(instruction);
        public bool CanReachLeg(string actor, GridPoint origin, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = (ReachObstacles)0) => Runtime.CanReachLeg(actor, origin, target, purpose, range, load, swimming, obstacles);
        public string FieldOrderBlockedReason(OwnerQueuedInstruction instruction) => Runtime.FieldOrderBlockedReason(instruction);
        public AgentKnowledgeArtifact? KnowledgeCopySourceFor(OwnerQueuedInstruction instruction) => OrderSystem.KnowledgeCopySourceFor(Runtime.KnowledgeCopyOrdersWorld, instruction);
        public AgentKnowledgeFact[] FactsToWrite(string actor, string kind, AgentKnowledgeArtifact? source = null) => Runtime.FactsToWrite(actor, kind, source);
        public AgentRecipeKnowledge[] RecipesToWrite(string actor, string kind, AgentKnowledgeArtifact? source = null) => Runtime.RecipesToWrite(actor, kind, source);
        public bool AlreadyWrote(string actor, string kind, AgentKnowledgeFact[] facts, AgentRecipeKnowledge[] recipes) => Runtime.AlreadyWrote(actor, kind, facts, recipes);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? KnowledgeOrderCandidateFor(OwnerQueuedInstruction instruction) => OrderSystem.KnowledgeOrderCandidateFor(Runtime.KnowledgeOrdersWorld, instruction);
        public string KnowledgeOrderBlockedReason(OwnerQueuedInstruction instruction) => OrderSystem.KnowledgeOrderBlockedReason(Runtime.KnowledgeOrdersWorld, instruction);
        public OwnerQueuedInstruction? PendingInstructionFor(string agentId) => Runtime.PendingInstructionFor(agentId);
        public string GuardianOrderBlockedReason(string adult, string child) => Runtime.GuardianOrderBlockedReason(adult, child);
        public string TreePlantingOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) => OrderSystem.TreePlantingOrderBlockedReason(Runtime.TreePlantingOrdersWorld, instruction, person);
        public string ExpansionOrderBlockedReason(OwnerQueuedInstruction instruction) => OrderSystem.ExpansionOrderBlockedReason(Runtime.ExpansionOrdersWorld, instruction);
        public string DeliveryOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) => Runtime.DeliveryOrderBlockedReason(instruction, person);
        public string ProductionOrderBlockedReason(OwnerQueuedInstruction instruction) => OrderSystem.ProductionOrderBlockedReason(Runtime.ProductionOrdersWorld, instruction);
        public string ToolRepairOrderBlockedReason(OwnerQueuedInstruction instruction) => OrderSystem.ToolRepairOrderBlockedReason(Runtime.ToolRepairOrdersWorld, instruction);
        public string RepairOrderBlockedReason(OwnerQueuedInstruction instruction) => Runtime.RepairOrderBlockedReason(instruction);
        public string MaterialOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) => OrderSystem.MaterialOrderBlockedReason(Runtime.MaterialOrdersWorld, instruction, person);
        public bool MovementOrderNeedsHouseInvitation(string actor, GridPoint destination) => Runtime.MovementOrderNeedsHouseInvitation(actor, destination);
        public InventoryLot? AvailableSharedFood(string actor, string? requiredItemKind = null) => Runtime.AvailableSharedFood(actor, requiredItemKind);
        public GridPoint HouseholdStockPosition(InventoryLot lot) => Runtime.HouseholdStockPosition(lot);
        public InventoryLot? SharedItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0) => Runtime.SharedItem(kind, actor, returnTo, returnRange);
        public string? TownForResident(string residentId) => Runtime.TownForResident(residentId);
        public void PauseExpansionForOrder(OwnerQueuedInstruction instruction) => OrderSystem.PauseExpansionForOrder(Runtime.ExpansionOrdersWorld, instruction);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ExpansionOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => OrderSystem.ExpansionOrderCandidateFor(Runtime.ExpansionOrdersWorld, instruction, person);
        public bool IsConversationBusy(string agentId) => Runtime.IsConversationBusy(agentId);
        public PlacedBuilding? HouseForHousehold(string householdId) => Runtime.HouseForHousehold(householdId);
        public OrderSystem.MaterialGatherEffect? GatherProjectMaterial(string agentId, AgentState state, string itemKind, MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null, GridPoint? returnTo = null, int returnRange = 0) => Runtime.GatherProjectMaterial(agentId, state, itemKind, source, useHarvestBonus, deliveryBuildingId, returnTo, returnRange);
        public void CancelEquipmentRepair(string actor) => Runtime.CancelEquipmentRepair(actor);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? RepairOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => Runtime.RepairOrderCandidateFor(instruction, person);
        public WorldProductionJob? BoundProductionJob(OwnerQueuedInstruction instruction) => OrderSystem.BoundProductionJob(Runtime.ProductionOrdersWorld, instruction);
        public void PauseProductionOrderJob(OwnerQueuedInstruction instruction, WorldProductionJob job) => Runtime.PauseProductionOrderJob(instruction, job);
        public WorldKnowledgeState Knowledge => Runtime.knowledge;
        public Dictionary<string, ResourceState> Resources => Runtime.resources;
        public bool RecordKnowledgeFact(string actor, GridPoint position) => Runtime.RecordKnowledgeFact(actor, position);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? MaterialOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => OrderSystem.MaterialOrderCandidateFor(Runtime.MaterialOrdersWorld, instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ProductionOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => OrderSystem.ProductionOrderCandidateFor(Runtime.ProductionOrdersWorld, instruction, person);
        public void SetProject(string agentId, AgentProject project) => Runtime.SetProject(agentId, project);
        public void ContinueProject(string agentId, AgentState state) => Runtime.ContinueProject(agentId, state);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ToolRepairOrderCandidateFor(OwnerQueuedInstruction instruction) => OrderSystem.ToolRepairOrderCandidateFor(Runtime.ToolRepairOrdersWorld, instruction);
    }
}
