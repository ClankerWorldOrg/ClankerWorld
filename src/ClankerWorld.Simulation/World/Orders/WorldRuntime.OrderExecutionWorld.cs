namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderExecutionWorld OrderExecutionWorld => new OrderExecutionAdapter(this);

    private sealed class OrderExecutionAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IOrderExecutionWorld
    {
        public void RecordAttemptedAction(string actor, string candidate) => Runtime.RecordAttemptedAction(actor, candidate);
        public global::ClankerWorld.Simulation.World.AgentState CompleteExplorationGoal(string actor, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.CompleteExplorationGoal(actor, person);
        public void ExecuteGuardianOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Cognition.CognitionCandidate candidate) => Runtime.ExecuteGuardianOrder(instruction, candidate);
        public bool IsCartOrder(string action) => WorldRuntime.IsCartOrder(action);
        public void ExecuteCartOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ExecuteCartOrderStep(instruction, person);
        public bool IsAnimalOrder(string action) => WorldRuntime.IsAnimalOrder(action);
        public void ExecuteAnimalOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.ExecuteAnimalOrder(instruction);
        public void ExecuteBoatOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.ExecuteBoatOrder(instruction);
        public void ExecuteTreePlantingOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ExecuteTreePlantingOrder(Runtime.TreePlantingOrdersWorld, instruction, person);
        public void ExecuteKnowledgeReadOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.ExecuteKnowledgeReadOrder(instruction);
        public void ExecuteTalkOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ExecuteTalkOrderStep(instruction, person);
        public void ExecuteKnowledgeOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ExecuteKnowledgeOrderStep(Runtime.KnowledgeOrdersWorld, instruction, person);
        public void ExecuteShelterOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ExecuteShelterOrderStep(instruction, person);
        public void ExecuteConstructionOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ExecuteConstructionOrderStep(Runtime.ConstructionOrdersWorld, instruction, person);
        public void ExecuteExpansionOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ExecuteExpansionOrderStep(Runtime.ExpansionOrdersWorld, instruction, person);
        public void ExecuteDeliveryOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ExecuteDeliveryOrderStep(instruction, person);
        public void ExecuteProductionOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ExecuteProductionOrderStep(Runtime.ProductionOrdersWorld, instruction, person);
        public void ExecuteFieldOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ExecuteFieldOrderStep(instruction, person);
        public void ExecuteToolRepairOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ExecuteToolRepairOrderStep(Runtime.ToolRepairOrdersWorld, instruction, person);
        public void ExecuteRepairOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ExecuteRepairOrderStep(instruction, person);
        public void ExecuteCollectionOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ExecuteCollectionOrderStep(instruction, person);
        public void ExecuteStorageOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ExecuteStorageOrderStep(instruction, person);
        public void ExecuteReturnOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ExecuteReturnOrderStep(instruction, person);
        public void ExecuteMaterialOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, string candidate) => OrderSystem.ExecuteMaterialOrderStep(Runtime.MaterialOrdersWorld, instruction, person, candidate);
        public string MovementOrderEffectId(global::ClankerWorld.Simulation.World.GridPoint destination) => WorldRuntime.MovementOrderEffectId(destination);
        public string? ConsumeFood(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string? requiredItemKind = null) => Runtime.ConsumeFood(agentId, state, requiredItemKind);
        public string ConsumptionOrderReceipt(long tick, string actor, string lot) => WorldRuntime.ConsumptionOrderReceipt(tick, actor, lot);
        public void CollectSharedFood(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string? requiredItemKind = null) => Runtime.CollectSharedFood(agentId, state, requiredItemKind);
        public void Explore(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.ExplorationGoal? goal = null, bool replaceGoal = false, global::ClankerWorld.Simulation.World.ExplorationActionPlan? originalPlan = null) => Runtime.Explore(actor, person, goal, replaceGoal, originalPlan);
        public global::ClankerWorld.Simulation.World.ExplorationGoal? ExplorationGoalForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => WorldRuntime.ExplorationGoalForOrder(instruction);
        public string OrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.OrderBlockedReason(instruction, person);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public string FoodArrivalReceipt(string resourceId, global::ClankerWorld.Simulation.World.GridPoint position) => WorldRuntime.FoodArrivalReceipt(resourceId, position);
        public global::ClankerWorld.Simulation.World.OrderSystem.FoodHarvestEffect? HarvestFood(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.MapResource? requestedSource = null) => Runtime.HarvestFood(agentId, state, requestedSource);
        public string FoodHarvestReceipt(string lot) => WorldRuntime.FoodHarvestReceipt(lot);
    }
}
