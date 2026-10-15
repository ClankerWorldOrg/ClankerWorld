using FoodHarvestEffect = ClankerWorld.Simulation.World.OrderSystem.FoodHarvestEffect;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderExecutionWorld OrderExecutionWorld => new OrderExecutionAdapter(this);

    private sealed class OrderExecutionAdapter(WorldRuntime runtime) : IOrderExecutionWorld
    {
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public long WorldTick => runtime.WorldTick;
        public AgentStore PhysicalAgents => runtime.agentStore;
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public void RecordAttemptedAction(string actor, string candidate) => runtime.RecordAttemptedAction(actor, candidate);
        public ClankerWorld.Simulation.World.AgentState CompleteExplorationGoal(string actor, AgentState person) => runtime.CompleteExplorationGoal(actor, person);
        public void CancelEquipmentRepair(string actor) => runtime.CancelEquipmentRepair(actor);
        public void ExecuteGuardianOrder(OwnerQueuedInstruction instruction, CognitionCandidate candidate) => runtime.ExecuteGuardianOrder(instruction, candidate);
        public bool IsCartOrder(string action) => WorldRuntime.IsCartOrder(action);
        public void ExecuteCartOrderStep(OwnerQueuedInstruction instruction, AgentState person) => runtime.ExecuteCartOrderStep(instruction, person);
        public bool IsAnimalOrder(string action) => WorldRuntime.IsAnimalOrder(action);
        public void ExecuteAnimalOrder(OwnerQueuedInstruction instruction) => runtime.ExecuteAnimalOrder(instruction);
        public void ExecuteBoatOrder(OwnerQueuedInstruction instruction) => runtime.ExecuteBoatOrder(instruction);
        public bool IsTreePlantingOrder(string action) => WorldRuntime.IsTreePlantingOrder(action);
        public void ExecuteTreePlantingOrder(OwnerQueuedInstruction instruction, AgentState person) => runtime.ExecuteTreePlantingOrder(instruction, person);
        public void ExecuteKnowledgeReadOrder(OwnerQueuedInstruction instruction) => runtime.ExecuteKnowledgeReadOrder(instruction);
        public void ExecuteTalkOrderStep(OwnerQueuedInstruction instruction, AgentState person) => runtime.ExecuteTalkOrderStep(instruction, person);
        public void ExecuteKnowledgeOrderStep(OwnerQueuedInstruction instruction, AgentState person) => runtime.ExecuteKnowledgeOrderStep(instruction, person);
        public void ExecuteShelterOrderStep(OwnerQueuedInstruction instruction, AgentState person) => runtime.ExecuteShelterOrderStep(instruction, person);
        public void ExecuteConstructionOrderStep(OwnerQueuedInstruction instruction, AgentState person) => runtime.ExecuteConstructionOrderStep(instruction, person);
        public void ExecuteExpansionOrderStep(OwnerQueuedInstruction instruction, AgentState person) => runtime.ExecuteExpansionOrderStep(instruction, person);
        public void ExecuteDeliveryOrderStep(OwnerQueuedInstruction instruction, AgentState person) => runtime.ExecuteDeliveryOrderStep(instruction, person);
        public void ExecuteProductionOrderStep(OwnerQueuedInstruction instruction, AgentState person) => runtime.ExecuteProductionOrderStep(instruction, person);
        public void ExecuteFieldOrderStep(OwnerQueuedInstruction instruction, AgentState person) => runtime.ExecuteFieldOrderStep(instruction, person);
        public void ExecuteToolRepairOrderStep(OwnerQueuedInstruction instruction, AgentState person) => runtime.ExecuteToolRepairOrderStep(instruction, person);
        public void ExecuteRepairOrderStep(OwnerQueuedInstruction instruction, AgentState person) => runtime.ExecuteRepairOrderStep(instruction, person);
        public void ExecuteCollectionOrderStep(OwnerQueuedInstruction instruction, AgentState person) => runtime.ExecuteCollectionOrderStep(instruction, person);
        public void ExecuteStorageOrderStep(OwnerQueuedInstruction instruction, AgentState person) => runtime.ExecuteStorageOrderStep(instruction, person);
        public void ExecuteReturnOrderStep(OwnerQueuedInstruction instruction, AgentState person) => runtime.ExecuteReturnOrderStep(instruction, person);
        public void ExecuteMaterialOrderStep(OwnerQueuedInstruction instruction, AgentState person, string candidate) => runtime.ExecuteMaterialOrderStep(instruction, person, candidate);
        public void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public bool RecordKnowledgeFact(string actor, GridPoint position) => runtime.RecordKnowledgeFact(actor, position);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string MovementOrderEffectId(GridPoint destination) => WorldRuntime.MovementOrderEffectId(destination);
        public string? ConsumeFood(string agentId, AgentState state, string? requiredItemKind = null) => runtime.ConsumeFood(agentId, state, requiredItemKind);
        public string ConsumptionOrderReceipt(long tick, string actor, string lot) => WorldRuntime.ConsumptionOrderReceipt(tick, actor, lot);
        public void CollectSharedFood(string agentId, AgentState state, string? requiredItemKind = null) => runtime.CollectSharedFood(agentId, state, requiredItemKind);
        public void Explore(string actor, AgentState person, ExplorationGoal? goal = null, bool replaceGoal = false, ExplorationActionPlan? originalPlan = null) => runtime.Explore(actor, person, goal, replaceGoal, originalPlan);
        public ClankerWorld.Simulation.World.ExplorationGoal? ExplorationGoalForOrder(OwnerQueuedInstruction instruction) => WorldRuntime.ExplorationGoalForOrder(instruction);
        public ClankerWorld.Simulation.World.MapResource? KnownFoodSourceForOrder(OwnerQueuedInstruction instruction, AgentState person) => runtime.KnownFoodSourceForOrder(instruction, person);
        public string OrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) => runtime.OrderBlockedReason(instruction, person);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public string FoodArrivalReceipt(string resourceId, GridPoint position) => WorldRuntime.FoodArrivalReceipt(resourceId, position);
        public ClankerWorld.Simulation.World.OrderSystem.FoodHarvestEffect? HarvestFood(string agentId, AgentState state, MapResource? requestedSource = null) => runtime.HarvestFood(agentId, state, requestedSource);
        public string FoodHarvestReceipt(string lot) => WorldRuntime.FoodHarvestReceipt(lot);
    }
}
