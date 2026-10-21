namespace ClankerWorld.Simulation.World;

internal interface IOrderExecutionWorld
{
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    void RecordAttemptedAction(string actor, string candidate);
    global::ClankerWorld.Simulation.World.AgentState CompleteExplorationGoal(string actor, global::ClankerWorld.Simulation.World.AgentState person);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    void CancelEquipmentRepair(string actor);
    void ExecuteGuardianOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Cognition.CognitionCandidate candidate);
    bool IsCartOrder(string action);
    void ExecuteCartOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    bool IsAnimalOrder(string action);
    void ExecuteAnimalOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void ExecuteBoatOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void ExecuteTreePlantingOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void ExecuteKnowledgeReadOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void ExecuteTalkOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void ExecuteKnowledgeOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void ExecuteShelterOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void ExecuteConstructionOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void ExecuteExpansionOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void ExecuteDeliveryOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void ExecuteProductionOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void ExecuteFieldOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void ExecuteToolRepairOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void ExecuteRepairOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void ExecuteCollectionOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void ExecuteStorageOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void ExecuteReturnOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void ExecuteMaterialOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, string candidate);
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    bool RecordKnowledgeFact(string actor, global::ClankerWorld.Simulation.World.GridPoint position);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string MovementOrderEffectId(global::ClankerWorld.Simulation.World.GridPoint destination);
    string? ConsumeFood(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string? requiredItemKind = null);
    string ConsumptionOrderReceipt(long tick, string actor, string lot);
    long WorldTick { get; }
    void CollectSharedFood(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string? requiredItemKind = null);
    global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents { get; }
    void Explore(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.ExplorationGoal? goal = null, bool replaceGoal = false, global::ClankerWorld.Simulation.World.ExplorationActionPlan? originalPlan = null);
    global::ClankerWorld.Simulation.World.ExplorationGoal? ExplorationGoalForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::ClankerWorld.Simulation.World.MapResource? KnownFoodSourceForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string OrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange);
    int ResourceInteractionRange { get; }
    string FoodArrivalReceipt(string resourceId, global::ClankerWorld.Simulation.World.GridPoint position);
    global::ClankerWorld.Simulation.World.OrderSystem.FoodHarvestEffect? HarvestFood(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.MapResource? requestedSource = null);
    string FoodHarvestReceipt(string lot);
}
