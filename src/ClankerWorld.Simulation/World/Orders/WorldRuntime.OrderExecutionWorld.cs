namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderExecutionWorld OrderExecutionWorld => new OrderExecutionAdapter(this);

    private sealed class OrderExecutionAdapter(WorldRuntime runtime) : IOrderExecutionWorld
    {
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public void RecordAttemptedAction(string actor, string candidate) => runtime.RecordAttemptedAction(actor, candidate);
        public global::ClankerWorld.Simulation.World.AgentState CompleteExplorationGoal(string actor, global::ClankerWorld.Simulation.World.AgentState person) => runtime.CompleteExplorationGoal(actor, person);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public void CancelEquipmentRepair(string actor) => runtime.CancelEquipmentRepair(actor);
        public void ExecuteGuardianOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Cognition.CognitionCandidate candidate) => runtime.ExecuteGuardianOrder(instruction, candidate);
        public bool IsCartOrder(string action) => WorldRuntime.IsCartOrder(action);
        public void ExecuteCartOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ExecuteCartOrderStep(instruction, person);
        public bool IsAnimalOrder(string action) => WorldRuntime.IsAnimalOrder(action);
        public void ExecuteAnimalOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.ExecuteAnimalOrder(instruction);
        public void ExecuteBoatOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.ExecuteBoatOrder(instruction);
        public void ExecuteTreePlantingOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ExecuteTreePlantingOrder(runtime.TreePlantingOrdersWorld, instruction, person);
        public void ExecuteKnowledgeReadOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.ExecuteKnowledgeReadOrder(instruction);
        public void ExecuteTalkOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ExecuteTalkOrderStep(instruction, person);
        public void ExecuteKnowledgeOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ExecuteKnowledgeOrderStep(runtime.KnowledgeOrdersWorld, instruction, person);
        public void ExecuteShelterOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ExecuteShelterOrderStep(instruction, person);
        public void ExecuteConstructionOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ExecuteConstructionOrderStep(runtime.ConstructionOrdersWorld, instruction, person);
        public void ExecuteExpansionOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ExecuteExpansionOrderStep(runtime.ExpansionOrdersWorld, instruction, person);
        public void ExecuteDeliveryOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ExecuteDeliveryOrderStep(instruction, person);
        public void ExecuteProductionOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ExecuteProductionOrderStep(instruction, person);
        public void ExecuteFieldOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ExecuteFieldOrderStep(instruction, person);
        public void ExecuteToolRepairOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ExecuteToolRepairOrderStep(runtime.ToolRepairOrdersWorld, instruction, person);
        public void ExecuteRepairOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ExecuteRepairOrderStep(instruction, person);
        public void ExecuteCollectionOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ExecuteCollectionOrderStep(instruction, person);
        public void ExecuteStorageOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ExecuteStorageOrderStep(instruction, person);
        public void ExecuteReturnOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ExecuteReturnOrderStep(instruction, person);
        public void ExecuteMaterialOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, string candidate) => OrderSystem.ExecuteMaterialOrderStep(runtime.MaterialOrdersWorld, instruction, person, candidate);
        public void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public bool RecordKnowledgeFact(string actor, global::ClankerWorld.Simulation.World.GridPoint position) => runtime.RecordKnowledgeFact(actor, position);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string MovementOrderEffectId(global::ClankerWorld.Simulation.World.GridPoint destination) => WorldRuntime.MovementOrderEffectId(destination);
        public string? ConsumeFood(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string? requiredItemKind = null) => runtime.ConsumeFood(agentId, state, requiredItemKind);
        public string ConsumptionOrderReceipt(long tick, string actor, string lot) => WorldRuntime.ConsumptionOrderReceipt(tick, actor, lot);
        public long WorldTick => runtime.WorldTick;
        public void CollectSharedFood(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string? requiredItemKind = null) => runtime.CollectSharedFood(agentId, state, requiredItemKind);
        public global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents => runtime.agentStore;
        public void Explore(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.ExplorationGoal? goal = null, bool replaceGoal = false, global::ClankerWorld.Simulation.World.ExplorationActionPlan? originalPlan = null) => runtime.Explore(actor, person, goal, replaceGoal, originalPlan);
        public global::ClankerWorld.Simulation.World.ExplorationGoal? ExplorationGoalForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => WorldRuntime.ExplorationGoalForOrder(instruction);
        public global::ClankerWorld.Simulation.World.MapResource? KnownFoodSourceForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.KnownFoodSourceForOrder(instruction, person);
        public string OrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.OrderBlockedReason(instruction, person);
        public bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public string FoodArrivalReceipt(string resourceId, global::ClankerWorld.Simulation.World.GridPoint position) => WorldRuntime.FoodArrivalReceipt(resourceId, position);
        public global::ClankerWorld.Simulation.World.OrderSystem.FoodHarvestEffect? HarvestFood(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.MapResource? requestedSource = null) => runtime.HarvestFood(agentId, state, requestedSource);
        public string FoodHarvestReceipt(string lot) => WorldRuntime.FoodHarvestReceipt(lot);
    }
}
