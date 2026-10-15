using FoodHarvestEffect = ClankerWorld.Simulation.World.OrderSystem.FoodHarvestEffect;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IOrderExecutionWorld
{
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    long WorldTick { get; }
    AgentStore PhysicalAgents { get; }
    int ResourceInteractionRange { get; }
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    void RecordAttemptedAction(string actor, string candidate);
    ClankerWorld.Simulation.World.AgentState CompleteExplorationGoal(string actor, AgentState person);
    void CancelEquipmentRepair(string actor);
    void ExecuteGuardianOrder(OwnerQueuedInstruction instruction, CognitionCandidate candidate);
    bool IsCartOrder(string action);
    void ExecuteCartOrderStep(OwnerQueuedInstruction instruction, AgentState person);
    bool IsAnimalOrder(string action);
    void ExecuteAnimalOrder(OwnerQueuedInstruction instruction);
    void ExecuteBoatOrder(OwnerQueuedInstruction instruction);
    bool IsTreePlantingOrder(string action);
    void ExecuteTreePlantingOrder(OwnerQueuedInstruction instruction, AgentState person);
    void ExecuteKnowledgeReadOrder(OwnerQueuedInstruction instruction);
    void ExecuteTalkOrderStep(OwnerQueuedInstruction instruction, AgentState person);
    void ExecuteKnowledgeOrderStep(OwnerQueuedInstruction instruction, AgentState person);
    void ExecuteShelterOrderStep(OwnerQueuedInstruction instruction, AgentState person);
    void ExecuteConstructionOrderStep(OwnerQueuedInstruction instruction, AgentState person);
    void ExecuteExpansionOrderStep(OwnerQueuedInstruction instruction, AgentState person);
    void ExecuteDeliveryOrderStep(OwnerQueuedInstruction instruction, AgentState person);
    void ExecuteProductionOrderStep(OwnerQueuedInstruction instruction, AgentState person);
    void ExecuteFieldOrderStep(OwnerQueuedInstruction instruction, AgentState person);
    void ExecuteToolRepairOrderStep(OwnerQueuedInstruction instruction, AgentState person);
    void ExecuteRepairOrderStep(OwnerQueuedInstruction instruction, AgentState person);
    void ExecuteCollectionOrderStep(OwnerQueuedInstruction instruction, AgentState person);
    void ExecuteStorageOrderStep(OwnerQueuedInstruction instruction, AgentState person);
    void ExecuteReturnOrderStep(OwnerQueuedInstruction instruction, AgentState person);
    void ExecuteMaterialOrderStep(OwnerQueuedInstruction instruction, AgentState person, string candidate);
    void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    bool RecordKnowledgeFact(string actor, GridPoint position);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    string MovementOrderEffectId(GridPoint destination);
    string? ConsumeFood(string agentId, AgentState state, string? requiredItemKind = null);
    string ConsumptionOrderReceipt(long tick, string actor, string lot);
    void CollectSharedFood(string agentId, AgentState state, string? requiredItemKind = null);
    void Explore(string actor, AgentState person, ExplorationGoal? goal = null, bool replaceGoal = false, ExplorationActionPlan? originalPlan = null);
    ClankerWorld.Simulation.World.ExplorationGoal? ExplorationGoalForOrder(OwnerQueuedInstruction instruction);
    ClankerWorld.Simulation.World.MapResource? KnownFoodSourceForOrder(OwnerQueuedInstruction instruction, AgentState person);
    string OrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange);
    string FoodArrivalReceipt(string resourceId, GridPoint position);
    ClankerWorld.Simulation.World.OrderSystem.FoodHarvestEffect? HarvestFood(string agentId, AgentState state, MapResource? requestedSource = null);
    string FoodHarvestReceipt(string lot);
}
