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

    private IOrderBlockersWorld OrderBlockersWorld => new OrderBlockersAdapter(this);

    private sealed class OrderBlockersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IOrderBlockersWorld
    {
        public string? KnowledgeReadOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.KnowledgeReadOrderBlocker(instruction);
        public string? TalkOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.TalkOrderBlocker(instruction, person);
        public bool IsCartOrder(string action) => WorldRuntime.IsCartOrder(action);
        public bool IsAnimalOrder(string action) => WorldRuntime.IsAnimalOrder(action);
        public string AnimalOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.AnimalOrderBlockedReason(instruction);
        public string ShelterOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ShelterOrderBlockedReason(instruction, person);
        public string ConstructionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ConstructionOrderBlockedReason(Runtime.ConstructionOrdersWorld, instruction, person);
        public string CollectionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.CollectionOrderBlockedReason(instruction, person);
        public string StorageOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.StorageOrderBlockedReason(instruction, person);
        public string ReturnOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ReturnOrderBlockedReason(instruction, person);
        public int ComfortableFullness => WorldRuntime.ComfortableFullness;
        public int FoodHarvestCarryUnits(global::ClankerWorld.Simulation.World.MapResource source) => WorldRuntime.FoodHarvestCarryUnits(source);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> BuildingsWithTag(string tag) => Runtime.BuildingsWithTag(tag);
        public bool HasHouseGuestInvitation(string actor, string houseId) => Runtime.HasHouseGuestInvitation(actor, houseId);
    }

    private IOrderCapabilitiesWorld OrderCapabilitiesWorld => new OrderCapabilitiesAdapter(this);

    private sealed class OrderCapabilitiesAdapter(WorldRuntime runtime) : IOrderCapabilitiesWorld
    {
        public global::ClankerWorld.Simulation.World.ActorCapabilityState? CapabilitiesFor(string actor) => runtime.CapabilitiesFor(actor);
    }

    private IOrderIngressWorld OrderIngressWorld => new OrderIngressAdapter(this);

    private sealed class OrderIngressAdapter(WorldRuntime runtime) : IOrderIngressWorld
    {
        public OrderStore Orders => runtime.orderStore;
        public string InstructionRequestDigest(global::ClankerWorld.Simulation.World.OwnerInstructionRequest request) => WorldRuntime.InstructionRequestDigest(request);
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseOrderTask(string text, string actor) => OrderSystem.ParseOrderTask(runtime.ControlWorld, text, actor);
        public void ReplacePendingOrder(string agentId) => OrderSystem.ReplacePendingOrder(runtime.ControlWorld, agentId);
        public string InstructionIdFor(long sequence) => WorldRuntime.InstructionIdFor(sequence);
        public long WorldTick => runtime.WorldTick;
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public string ToWireValue(global::ClankerWorld.Simulation.World.OwnerInstructionKind kind) => WorldRuntime.ToWireValue(kind);
        public void CloseOrdersNotUnderstood(string agentId) => runtime.CloseOrdersNotUnderstood(agentId);
        public void PruneClosedOrderHistory() => runtime.PruneClosedOrderHistory();
        public bool Matches(global::ClankerWorld.Simulation.World.OwnerOrderCancellation existing, global::ClankerWorld.Simulation.World.OwnerOrderCancelRequest request) => WorldRuntime.Matches(existing, request);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public bool IsInstructionComplete(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.IsInstructionComplete(instruction);
        public void CancelActiveOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string cause, string? reason) => OrderSystem.CancelActiveOrder(runtime.ControlWorld, instruction, cause, reason);
        public long NextEventId => runtime.nextEventId;
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }

    private IOrderLifecycleWorld OrderLifecycleWorld => new OrderLifecycleAdapter(this);

    private sealed class OrderLifecycleAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IOrderLifecycleWorld
    {
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public void PruneClosedOrderHistory() => Runtime.PruneClosedOrderHistory();
        public void PauseConstructionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.PauseConstructionForOrder(Runtime.ConstructionOrdersWorld, instruction);
    }

    private IOrderSelectionWorld OrderSelectionWorld => new OrderSelectionAdapter(this);

    private sealed class OrderSelectionAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IOrderSelectionWorld
    {
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? KnowledgeReadOrderCandidate(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.KnowledgeReadOrderCandidate(instruction);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? TalkOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.TalkOrderCandidateFor(instruction, person);
        public bool IsCartOrder(string action) => WorldRuntime.IsCartOrder(action);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? CartOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.CartOrderCandidateFor(instruction, person);
        public bool IsAnimalOrder(string action) => WorldRuntime.IsAnimalOrder(action);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? AnimalOrderCandidate(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.AnimalOrderCandidate(instruction);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? BoatOrderCandidate(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.BoatOrderCandidate(instruction);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? TreePlantingOrderCandidate(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.TreePlantingOrderCandidate(Runtime.TreePlantingOrdersWorld, instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ShelterOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ShelterOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ConstructionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ConstructionOrderCandidateFor(Runtime.ConstructionOrdersWorld, instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? DeliveryOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.DeliveryOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? FieldOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.FieldOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? CollectionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.CollectionOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? StorageOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.StorageOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ReturnOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ReturnOrderCandidateFor(instruction, person);
        public bool CanSwim(string actor, global::ClankerWorld.Simulation.World.GridPoint? origin = null, int additionalCarriedUnits = 0) => Runtime.CanSwim(actor, origin, additionalCarriedUnits);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? GuardianOrderCandidate(string adult, string child) => Runtime.GuardianOrderCandidate(adult, child);
        public int ComfortableFullness => WorldRuntime.ComfortableFullness;
        public int FoodHarvestCarryUnits(global::ClankerWorld.Simulation.World.MapResource source) => WorldRuntime.FoodHarvestCarryUnits(source);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public bool KnownTargetIsUnavailable(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.KnownTargetIsUnavailable(instruction);
        public string FoodArrivalReceipt(string resourceId, global::ClankerWorld.Simulation.World.GridPoint position) => WorldRuntime.FoodArrivalReceipt(resourceId, position);
        public bool MatchesFoodArrivalReceipt(string? receipt, global::ClankerWorld.Simulation.World.MapResource resource) => Runtime.MatchesFoodArrivalReceipt(receipt, resource);
        public string FoodKnowledgeKind(global::ClankerWorld.Simulation.World.MapResource resource) => WorldRuntime.FoodKnowledgeKind(resource);
        public bool CanReachTerrain(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming) => Runtime.CanReachTerrain(actor, target, purpose, load, swimming);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool HasWarmthForOuting(global::ClankerWorld.Simulation.World.AgentState person) => Runtime.HasWarmthForOuting(person);
        public int OutingFullnessReserve => WorldRuntime.OutingFullnessReserve;
    }

    private IOrderSurvivalWorld OrderSurvivalWorld => new OrderSurvivalAdapter(this);

    private sealed class OrderSurvivalAdapter(WorldRuntime runtime) : IOrderSurvivalWorld
    {
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool IsFoodSurvivalCandidate(string actor, string candidateId) => OrderSystem.IsFoodSurvivalCandidate(runtime.OrderSurvivalWorld, actor, candidateId);
        public bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person) => runtime.NeedsUrgentWarmth(person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? SelectOrderSurvivalCandidate(global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction) => runtime.SelectOrderSurvivalCandidate(candidates, person, instruction);
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateCandidates(string agentId, global::ClankerWorld.Simulation.World.AgentState state, bool restrictForOrder = true) => runtime.CreateCandidates(agentId, state, restrictForOrder);
        public bool IsSurvivalCandidate(string actor, string candidateId) => runtime.IsSurvivalCandidate(actor, candidateId);
        public string TownProjectReturnPrefix => WorldRuntime.TownProjectReturnPrefix;
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public bool IsShopFoodCandidate(string actor, string candidateId, global::ClankerWorld.Simulation.World.ShopBuyActionPlan? selectedPlan = null) => runtime.IsShopFoodCandidate(actor, candidateId, selectedPlan);
        public bool IsMarketFoodCandidate(string actor, string candidateId) => MarketSystem.IsMarketFoodCandidate(runtime.MarketStallWorld, actor, candidateId);
    }

    private IControlWorld ControlWorld => new ControlAdapter(this);

    private sealed class ControlAdapter(WorldRuntime runtime) : IControlWorld
    {
        public void CancelRepairForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.CancelRepairForOrder(runtime.RepairOrdersWorld, instruction);
        public void CancelFieldWorkForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.CancelFieldWorkForOrder(runtime.FieldOrdersWorld, instruction);
        public void CancelProductionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.CancelProductionForOrder(instruction);
        public void CancelConstructionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.CancelConstructionForOrder(runtime.ConstructionOrdersWorld, instruction);
        public void CancelExpansionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.CancelExpansionForOrder(runtime.ExpansionOrdersWorld, instruction);
        public void CancelAnimalSupplyForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.CancelAnimalSupplyForOrder(instruction);
        public void CancelBoatTravelForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.CancelBoatTravelForOrder(runtime.BoatOrdersWorld, instruction);
        public void CancelKnowledgeWritingForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.CancelKnowledgeWritingForOrder(runtime.KnowledgeOrdersWorld, instruction);
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction[] ObserverGuidanceInstructionsFor(string agentId) => OrderSystem.ObserverGuidanceInstructionsFor(runtime.ControlWorld, agentId);
        public OrderStore Orders => runtime.orderStore;
        public bool IsInstructionComplete(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.IsInstructionComplete(instruction);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public string ToWireValue(global::ClankerWorld.Simulation.World.OwnerInstructionKind kind) => WorldRuntime.ToWireValue(kind);
        public string? UnderstoodTaskFor(string? candidate) => WorldRuntime.UnderstoodTaskFor(candidate);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseMarriageOrder(string text, string actor) => OrderSystem.ParseMarriageOrder(runtime.MarriageOrdersWorld, text, actor);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseKnowledgeReadOrder(string text) => OrderSystem.ParseKnowledgeReadOrder(runtime.KnowledgeReadOrdersWorld, text);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseTalkOrder(string text, string actor) => OrderSystem.ParseTalkOrder(runtime.TalkOrdersWorld, text, actor);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseCartCargoOrder(string text, string actor) => OrderSystem.ParseCartCargoOrder(runtime.CartCargoOrdersWorld, text, actor);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseCartOrder(string text, string actor) => runtime.ParseCartOrder(text, actor);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseAnimalOrder(string text, string actor) => runtime.ParseAnimalOrder(text, actor);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseGuardianOrder(string text, string actor) => OrderSystem.ParseGuardianOrder(runtime.GuardianOrdersWorld, text, actor);
        public global::ClankerWorld.Simulation.World.WorldMap Map => runtime.map;
        public string FoodKnowledgeKind(global::ClankerWorld.Simulation.World.MapResource resource) => WorldRuntime.FoodKnowledgeKind(resource);
        public global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content => runtime.worldContent;
        public global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation => runtime.worldSimulation;
        public global::ClankerWorld.Simulation.World.PlacedBuilding? Port(string id) => runtime.Port(id);
        public void CancelActiveOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string cause, string? reason) => OrderSystem.CancelActiveOrder(runtime.ControlWorld, instruction, cause, reason);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
