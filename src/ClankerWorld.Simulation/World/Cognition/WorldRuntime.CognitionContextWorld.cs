namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionContextWorld CognitionContextWorld => new CognitionContextAdapter(this);

    private sealed class CognitionContextAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionContextWorld
    {
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => Runtime.PendingInstructionFor(agentId);
        public bool IsFinishedOrderDecisionAwaitingReply(global::ClankerWorld.Simulation.Cognition.AgentObservation observation) => Runtime.IsFinishedOrderDecisionAwaitingReply(observation);
        public string? UnderstoodTaskFor(string? candidate) => WorldRuntime.UnderstoodTaskFor(candidate);
        public bool HasNewObserverGuidanceFor(string agentId) => OrderSystem.HasNewObserverGuidanceFor(Runtime.ControlWorld, agentId);
        public long CognitionReevaluationIntervalTicks => WorldRuntime.CognitionReevaluationIntervalTicks;
        public bool CanContinueLesson(string actor, bool waitingForModel = false) => CommunitySystem.CanContinueLesson(Runtime.CommunityLearningWorld, actor, waitingForModel);
        public string DecisionContext(global::ClankerWorld.Simulation.World.AgentState state, global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string? conversationChoiceContext = null) => CognitionSystem.DecisionContext(Runtime.CognitionContextWorld, state, candidates, conversationChoiceContext);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public string MedicalAllowPrefix => global::ClankerWorld.Simulation.World.MedicalConsentChoice.AllowPrefix;
    }

    private ICognitionAdmissionWorld CognitionAdmissionWorld => new CognitionAdmissionAdapter(this);

    private sealed class CognitionAdmissionAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionAdmissionWorld
    {
        public void ApplyMemorySummary(string ownerId, global::ClankerWorld.Simulation.Cognition.CognitionMemorySummaryOption summary) => CognitionSystem.ApplyMemorySummary(Runtime.CognitionMemoriesWorld, ownerId, summary);
        public void ApplyMemoryCompaction(string ownerId, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Cognition.CognitionMemoryCompactionScore> scores) => CognitionSystem.ApplyMemoryCompaction(Runtime.CognitionMemoriesWorld, ownerId, scores);
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
        public global::System.Collections.Generic.List<string> ApplyObserverGuidanceResult(string agentId, global::ClankerWorld.Simulation.Cognition.CognitionAdmissionResult admission) => CognitionSystem.ApplyObserverGuidanceResult(Runtime.CognitionAdmissionWorld, agentId, admission);
        public bool ApplyMedicalConsentDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision) => Runtime.ApplyMedicalConsentDecision(decision);
        public bool ApplyOrnamentDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision) => Runtime.ApplyOrnamentDecision(decision);
        public bool ApplyAnimalConsentDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision) => Runtime.ApplyAnimalConsentDecision(decision);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => Runtime.PendingInstructionFor(agentId);
        public global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor) => TransportSystem.PassengerBoat(Runtime.BoatWorld, actor);
        public void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle) => Runtime.ApplyCandidate(agentId, state, candidateId, reportIdle);
        public void ApplyTalkOrderConversationDecision(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision) => OrderSystem.ApplyTalkOrderConversationDecision(Runtime.TalkOrdersWorld, instruction, decision);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? UrgentSurvivalCandidateFor(string agentId, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction = null) => OrderSystem.UrgentSurvivalCandidateFor(Runtime.OrderSurvivalWorld, agentId, person, instruction);
        public bool IsMarketFoodCandidate(string actor, string candidateId) => MarketSystem.IsMarketFoodCandidate(Runtime.MarketStallWorld, actor, candidateId);
        public bool ApplyMarketCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string id, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.ApplyMarketCandidate(actor, state, id, originalPlan);
        public bool IsShopFoodCandidate(string actor, string candidateId, global::ClankerWorld.Simulation.World.ShopBuyActionPlan? selectedPlan = null) => Runtime.IsShopFoodCandidate(actor, candidateId, selectedPlan);
        public void ApplyShopCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string candidate, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.ApplyShopCandidate(actor, state, candidate, originalPlan);
        public void ExecuteOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Cognition.CognitionCandidate candidate) => OrderSystem.ExecuteOrderStep(Runtime.OrderExecutionWorld, instruction, person, candidate);
        public void ApplyPlannedTownCivicCandidate(string actor, string candidate, string? proposalText = null, global::System.Collections.Generic.IReadOnlyList<string>? ballot = null, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Cognition.CognitionLandTile>? landTiles = null, global::ClankerWorld.Simulation.Cognition.CognitionLandHearingChoice? hearingChoice = null, global::ClankerWorld.Simulation.Cognition.CognitionNonLandChoice? nonLandChoice = null, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.ApplyPlannedTownCivicCandidate(actor, candidate, proposalText, ballot, landTiles, hearingChoice, nonLandChoice, originalPlan);
        public bool IsTownProjectDonationCandidate(string id) => WorldRuntime.IsTownProjectDonationCandidate(id);
        public bool ApplyTownProjectDonation(string actor, string id, global::ClankerWorld.Simulation.World.TownProjectDonationActionPlan? originalPlan = null) => Runtime.ApplyTownProjectDonation(actor, id, originalPlan);
        public bool IsMarketCandidate(string id) => WorldRuntime.IsMarketCandidate(id);
        public bool ApplyToolMakingRequestDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision) => Runtime.ApplyToolMakingRequestDecision(decision);
        public void ApplyRegisteredCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle, global::ClankerWorld.Simulation.World.ActionPlan? plan = null) => Runtime.ApplyRegisteredCandidate(agentId, state, candidateId, reportIdle, plan);
        public string ToWireValue(global::ClankerWorld.Simulation.World.OwnerInstructionKind kind) => WorldRuntime.ToWireValue(kind);
        public string? UnderstoodTaskFor(string? candidate) => WorldRuntime.UnderstoodTaskFor(candidate);
    }

    private ICognitionCandidatesWorld CognitionCandidatesWorld => new CognitionCandidatesAdapter(this);

    private sealed class CognitionCandidatesAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionCandidatesWorld
    {
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateRegisteredCandidates(string agentId, global::ClankerWorld.Simulation.World.AgentState state, bool restrictForOrder) => Runtime.CreateRegisteredCandidates(agentId, state, restrictForOrder);
        public global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor) => TransportSystem.PassengerBoat(Runtime.BoatWorld, actor);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate WaitingCandidate(string actor, string text) => Runtime.WaitingCandidate(actor, text);
        public int ComfortableFullness => WorldRuntime.ComfortableFullness;
        public void AddFoodPlanCandidate(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.FoodActionPlan? plan, string description, int priority, string? destination = null) => Runtime.AddFoodPlanCandidate(candidates, plan, description, priority, destination);
        public global::ClankerWorld.Simulation.World.ConsumeFoodActionPlan? PlanFoodConsumption(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot? selected = null) => Runtime.PlanFoodConsumption(actor, selected);
        public void AddMilkCandidate(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string actor) => Runtime.AddMilkCandidate(candidates, actor);
        public global::ClankerWorld.Simulation.Cognition.AgentConversation? ConversationFor(string agentId) => Runtime.ConversationFor(agentId);
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> ConversationCandidates(string agentId) => Runtime.ConversationCandidates(agentId);
        public void AddRegisteredChoices(global::ClankerWorld.Simulation.World.WorldRuntime.ChoiceGroup group, global::ClankerWorld.Simulation.World.WorldRuntime.ChoiceContext context) => Runtime.AddRegisteredChoices(group, context);
        public bool IsAdult(string actor) => Runtime.IsAdult(actor);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> WarehousesAccessibleTo(string actor) => Runtime.WarehousesAccessibleTo(actor);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => Runtime.PendingInstructionFor(agentId);
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> RestrictCandidatesForOrder(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction order, global::ClankerWorld.Simulation.World.AgentState state) => Runtime.RestrictCandidatesForOrder(candidates, order, state);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? SelectOrderSurvivalCandidate(global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction) => Runtime.SelectOrderSurvivalCandidate(candidates, person, instruction);
        public bool IsSurvivalCandidate(string actor, string candidateId) => Runtime.IsSurvivalCandidate(actor, candidateId);
        public bool IsShelterOrder(string action) => WorldRuntime.IsShelterOrder(action);
        public void AddHouseholdBuildingPlans(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.Society.SocietyAgent agent, bool needsWarmth, string householdId, global::System.Collections.Generic.List<string> buildingsWithoutSites) => Runtime.AddHouseholdBuildingPlans(candidates, agent, needsWarmth, householdId, buildingsWithoutSites);
        public global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation => Runtime.worldSimulation;
        public bool IsGenericFoodRecipe(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe) => WorldRuntime.IsGenericFoodRecipe(recipe);
        public bool IsHouseholdLayout(global::ClankerWorld.Simulation.Content.BuildingDefinition? building) => WorldRuntime.IsHouseholdLayout(building);
        public bool IsHandcartRecipe(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe) => ProductionSystem.IsHandcartRecipe(recipe);
        public string ProductionOwnerFor(global::ClankerWorld.Simulation.World.PlacedBuilding? building, string workerId) => Runtime.ProductionOwnerFor(building, workerId);
        public bool HasCarriedUnreservedQuantities(string actor, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> quantities) => Runtime.HasCarriedUnreservedQuantities(actor, quantities);
        public bool NeedsRecipeOutput(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, string? ownerId = null, string? requestWorker = null) => Runtime.NeedsRecipeOutput(recipe, ownerId, requestWorker);
        public bool AnotherAgentWaitsForWorkSite(string actor, global::ClankerWorld.Simulation.Content.RecipeDefinition recipe) => Runtime.AnotherAgentWaitsForWorkSite(actor, recipe);
        public bool CanAcquireProjectInputs(global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> inputs, string? ownerId = null, string? residentId = null, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?>? reachableToolCache = null, global::System.Lazy<global::ClankerWorld.Simulation.World.PlacedBuilding[]>? accessibleWarehouses = null) => Runtime.CanAcquireProjectInputs(inputs, ownerId, residentId, reachableToolCache, accessibleWarehouses);
        public bool HasIngredientsAtBuilding(global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> inputs, string ownerId, string buildingId) => Runtime.HasIngredientsAtBuilding(inputs, ownerId, buildingId);
        public global::ClankerWorld.Simulation.World.RecipeProjectActionPlan PlanRecipeProject(string actor, global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, global::ClankerWorld.Simulation.World.PlacedBuilding workstation) => ProductionSystem.PlanRecipeProject(Runtime.RecipeWorld, actor, recipe, workstation);
        public int OutdoorExposure(global::ClankerWorld.Simulation.World.GridPoint position) => Runtime.OutdoorExposure(position);
        public global::ClankerWorld.Simulation.World.AnimalState? Animal(string id) => Runtime.Animal(id);
    }

    private ICognitionConstructionWorld CognitionConstructionWorld => new CognitionConstructionAdapter(this);

    private sealed class CognitionConstructionAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionConstructionWorld
    {
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
        public global::ClankerWorld.Simulation.World.ConstructionActionPlan? RetainedConstruction(string actor, global::ClankerWorld.Simulation.World.AgentProject project) => Runtime.RetainedConstruction(actor, project);
        public global::ClankerWorld.Simulation.World.ActionCheck CheckConstruction(global::ClankerWorld.Simulation.World.ConstructionActionPlan plan, bool checkDestination = true, global::ClankerWorld.Simulation.World.ActorCapabilityPolicy? policy = null) => Runtime.CheckConstruction(plan, checkDestination, policy);
        public global::ClankerWorld.Simulation.World.TownLayoutContext CreateTownLayoutContext(string actor, global::ClankerWorld.Simulation.World.GridPoint? selectedSite = null, global::ClankerWorld.Simulation.Content.BuildingDefinition? building = null, bool forTownProject = false, string? townProjectId = null) => Runtime.CreateTownLayoutContext(actor, selectedSite, building, forTownProject, townProjectId);
        public string? TownForResident(string residentId) => Runtime.TownForResident(residentId);
        public bool IsHouseholdBuildingTag(string tag) => WorldRuntime.IsHouseholdBuildingTag(tag);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId) => Runtime.HouseForHousehold(householdId);
        public void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => Runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? ConstructionInstructionForProject(string actor, global::ClankerWorld.Simulation.World.AgentProject? project) => OrderSystem.ConstructionInstructionForProject(Runtime.ConstructionOrdersWorld, actor, project);
        public int ProjectWorkTicks => global::ClankerWorld.Simulation.World.ProductionSystem.ProjectWorkTicks;
        public bool TryConstructionOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, out global::ClankerWorld.Simulation.Content.BuildingDefinition? definition, out string? household, out global::ClankerWorld.Simulation.World.GridPoint position, out string failure) => OrderSystem.TryConstructionOrderSite(Runtime.ConstructionOrdersWorld, instruction, person, out definition, out household, out position, out failure);
        public string BuildingConstructionOwner(string actor, global::ClankerWorld.Simulation.Content.BuildingDefinition definition) => Runtime.BuildingConstructionOwner(actor, definition);
        public global::ClankerWorld.Simulation.World.BuildingPlacementResult PlaceBuildingCore(string instanceId, string definitionId, global::ClankerWorld.Simulation.World.GridPoint position, string eventKind, string? assignedTownId = null, string? householdId = null, string? constructionOwnerId = null, string? constructionInstructionId = null, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.ConstructionMaterialInput>? selectedMaterials = null) => Runtime.PlaceBuildingCore(instanceId, definitionId, position, eventKind, assignedTownId, householdId, constructionOwnerId, constructionInstructionId, selectedMaterials);
        public string BuildInstanceId(string agentId, global::ClankerWorld.Simulation.Content.BuildingDefinition definition) => Runtime.BuildInstanceId(agentId, definition);
        public void CreditCompletedWork(string actor, string domain, global::ClankerWorld.Simulation.World.SkillKind? skill = null) => Runtime.CreditCompletedWork(actor, domain, skill);
        public void RecordConstructionOrderCompletion(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string materialOwner) => OrderSystem.RecordConstructionOrderCompletion(Runtime.ConstructionOrdersWorld, instruction, materialOwner);
        public global::ClankerWorld.Simulation.World.RecipeProjectActionPlan? RetainedRecipeProject(string actor, global::ClankerWorld.Simulation.World.AgentProject project) => Runtime.RetainedRecipeProject(actor, project);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => Runtime.PendingInstructionFor(agentId);
        public bool IsProductionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project) => WorldRuntime.IsProductionOrderProject(instruction, project);
        public global::ClankerWorld.Simulation.World.ProductionStartResult StartProductionCore(string recipeId, string buildingInstanceId, string workerId, string eventKind, string? orderInstructionId = null, global::ClankerWorld.Simulation.World.RecipeProjectActionPlan? originalPlan = null) => Runtime.StartProductionCore(recipeId, buildingInstanceId, workerId, eventKind, orderInstructionId, originalPlan);
    }

    private ICognitionContinuationWorld CognitionContinuationWorld => new CognitionContinuationAdapter(this);

    private sealed class CognitionContinuationAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionContinuationWorld
    {
        public global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor) => TransportSystem.PassengerBoat(Runtime.BoatWorld, actor);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => Runtime.PendingInstructionFor(agentId);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle) => Runtime.ApplyCandidate(agentId, state, candidateId, reportIdle);
        public bool TryApplyWaitingRoutine(string id, global::ClankerWorld.Simulation.World.AgentState state) => CognitionSystem.TryApplyWaitingRoutine(Runtime.CognitionWaitingRoutinesWorld, id, state);
        public bool ContinueGuardianPlacement(string actor) => FamilySystem.ContinueGuardianPlacement(Runtime.FamilyGuardianWorld, actor);
        public bool IsConversationBusy(string agentId) => Runtime.IsConversationBusy(agentId);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? UrgentSurvivalCandidateFor(string agentId, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction = null) => OrderSystem.UrgentSurvivalCandidateFor(Runtime.OrderSurvivalWorld, agentId, person, instruction);
        public bool IsMarketFoodCandidate(string actor, string candidateId) => MarketSystem.IsMarketFoodCandidate(Runtime.MarketStallWorld, actor, candidateId);
        public void ContinueMarketWalk(string actor, string id) => MarketSystem.ContinueMarketWalk(Runtime.MarketStallWorld, actor, id);
        public void ExecuteOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Cognition.CognitionCandidate candidate) => OrderSystem.ExecuteOrderStep(Runtime.OrderExecutionWorld, instruction, person, candidate);
        public global::ClankerWorld.Simulation.World.EquipmentRepairWork? ContinueEquipmentRepair(string actor) => Runtime.ContinueEquipmentRepair(actor);
        public bool CanContinueLesson(string actor, bool waitingForModel = false) => CommunitySystem.CanContinueLesson(Runtime.CommunityLearningWorld, actor, waitingForModel);
        public void ContinueLesson(string actor, bool waitingForModel = false, global::ClankerWorld.Simulation.World.LearningActionPlan? originalPlan = null) => CommunitySystem.ContinueLesson(Runtime.CommunityLearningWorld, actor, waitingForModel, originalPlan);
        public bool ContinueFarmWork(string workerId) => Runtime.farmingSystem.ContinueFarmWork(Runtime.farmingWorld, workerId);
        public void ContinueProject(string agentId, global::ClankerWorld.Simulation.World.AgentState state) => Runtime.ContinueProject(agentId, state);
        public string NonLandRemedyPrefix => WorldRuntime.NonLandRemedyPrefix;
        public string NonLandRelayPrefix => WorldRuntime.NonLandRelayPrefix;
        public void ContinueTownCivicVisit(string actor, string candidate) => Runtime.townSystem.ContinueTownCivicVisit(Runtime.civicWorld, actor, candidate);
        public void ContinueTownLandSaleWalk(string actor, string candidate) => Runtime.townSystem.ContinueTownLandSaleWalk(Runtime.civicWorld, actor, candidate);
        public bool IsTownProjectDonationCandidate(string id) => WorldRuntime.IsTownProjectDonationCandidate(id);
        public void ContinueTownProjectDonationWalk(string actor, string id) => Runtime.ContinueTownProjectDonationWalk(actor, id);
        public bool IsMarketCandidate(string id) => WorldRuntime.IsMarketCandidate(id);
        public bool IsOrnamentCandidate(string candidate) => WorldRuntime.IsOrnamentCandidate(candidate);
        public void ContinueOrnamentWalk(string actor, string candidate) => Runtime.ContinueOrnamentWalk(actor, candidate);
        public bool CanContinuePendingTask(global::ClankerWorld.Simulation.World.AgentState state, string id) => CognitionSystem.CanContinuePendingTask(Runtime.CognitionWaitingRoutinesWorld, state, id);
    }

    private ICognitionConversationTasksWorld CognitionConversationTasksWorld => new CognitionConversationTasksAdapter(this);

    private sealed class CognitionConversationTasksAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionConversationTasksWorld
    {
        public void ApplyConversationTurnOutcome(global::ClankerWorld.Simulation.World.CognitionSystem.PendingConversationTurn pending, global::ClankerWorld.Simulation.World.CognitionSystem.ConversationTurnOutcome outcome, long worldTick, global::System.Func<global::ClankerWorld.Simulation.World.CognitionSystem.PendingConversationTurn, bool> providerRouteIsCurrent) => CognitionSystem.ApplyConversationTurnOutcome(Runtime.CognitionConversationTasksWorld, pending, outcome, worldTick, providerRouteIsCurrent);
        public int MaximumConversationProviderCallsPerTick => WorldRuntime.MaximumConversationProviderCallsPerTick;
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.Cognition.AgentConversation> Conversations => Runtime.conversations;
        public bool CanContinueConversation(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation) => Runtime.CanContinueConversation(conversation);
        public void SuspendConversation(string conversationId, global::ClankerWorld.Simulation.Cognition.AgentConversationInterruption interruption) => Runtime.SuspendConversation(conversationId, interruption);
        public global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest CreateConversationRequest(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation, string speakerId) => CognitionSystem.CreateConversationRequest(Runtime.CognitionConversationTasksWorld, conversation, speakerId);
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
        public bool CanProposeMarriage(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation) => Runtime.CanProposeMarriage(conversation);
        public string? RequestedConversationActivity(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation, string speaker) => OrderSystem.RequestedConversationActivity(Runtime.MarriageOrdersWorld, conversation, speaker);
        public void CancelPendingConversationTurn(string conversationId, global::ClankerWorld.Simulation.Cognition.AgentConversationInterruption interruption, bool suspendCurrent = true, bool underRuntimeGate = true) => Runtime.CancelPendingConversationTurn(conversationId, interruption, suspendCurrent, underRuntimeGate);
        public void RecordStartedConversation(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation) => Runtime.conversationSystem.SetConversation(conversation);
        public bool ApplyConversationTurnOutcome(global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest request, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnResponse? response, Exception? failure, long tick, Func<bool> providerRouteIsCurrent) => Runtime.conversationSystem.ApplyConversationTurnOutcome(Runtime.conversationWorld, request, response, failure, tick, providerRouteIsCurrent);
        public Task<CognitionSystem.ConversationTurnOutcome> RunConversationProviderUnderGate(global::ClankerWorld.Simulation.Cognition.IAgentConversationProvider provider, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest request, CancellationToken cancellationToken)
        {
            var outerInvocation = providerInvocationUnderGate;
            providerInvocationUnderGate = Runtime;
            try
            {
                return Runtime.ScheduleModelWork(global::ClankerWorld.Simulation.Cognition.ModelWorkKind.Conversation, request.SpeakerId, request.RequestId,
                    () => CognitionSystem.RunConversationProviderAsync(provider, request, Runtime.modelTimeProvider, cancellationToken),
                    conversation: request, startInline: true);
            }
            finally { providerInvocationUnderGate = outerInvocation; }
        }
    }

    private ICognitionIdentityMomentsWorld CognitionIdentityMomentsWorld => new CognitionIdentityMomentsAdapter(this);

    private sealed class CognitionIdentityMomentsAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionIdentityMomentsWorld
    {
        public void PrepareIdentityMoments(global::System.Action<global::ClankerWorld.Simulation.World.CognitionSystem.PreparedIdentityMoment> prepare, global::System.Collections.Generic.IReadOnlySet<string>? activeModelCallIds = null, global::System.Collections.Generic.IReadOnlySet<string>? activeIdentityIds = null) => Runtime.PrepareIdentityMoments(prepare, activeModelCallIds, activeIdentityIds);
        public void StartPreparedIdentityMoment(global::ClankerWorld.Simulation.World.CognitionSystem.PreparedIdentityMoment prepared) => Runtime.StartPreparedIdentityMoment(prepared);
        public bool IsConversationBusy(string agentId) => Runtime.IsConversationBusy(agentId);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public global::ClankerWorld.Simulation.Cognition.AgentObservation CreateIdentityMomentObservation(global::ClankerWorld.Simulation.Society.SocietyAgent person, global::ClankerWorld.Simulation.World.AgentState physical, global::ClankerWorld.Simulation.World.AgentIdentityMoment moment) => CognitionSystem.CreateIdentityMomentObservation(Runtime.CognitionIdentityMomentsWorld, person, physical, moment);
        public void FinishIdentityMoment(string id, global::ClankerWorld.Simulation.World.AgentIdentityMomentKind kind, string outcome, string? personality = null, string? aspiration = null) => Runtime.FinishIdentityMoment(id, kind, outcome, personality, aspiration);
        public void SetIdentityMoment(string id, global::ClankerWorld.Simulation.World.AgentIdentityMoment moment) => CognitionSystem.SetIdentityMoment(Runtime.CognitionIdentityMomentsWorld, id, moment);
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
        public global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.IdentityMomentOutcome> ScheduleModelWork(global::ClankerWorld.Simulation.Cognition.ModelWorkKind kind, string actorId, string requestId, global::System.Func<global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.IdentityMomentOutcome>> work, global::ClankerWorld.Simulation.Cognition.CognitionDecisionRequest? decision = null, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest? conversation = null, bool startInline = false) => Runtime.ScheduleModelWork(kind, actorId, requestId, work, decision, conversation, startInline);
        public void CancelIdentityMoment(string id, bool underRuntimeGate = true) => CognitionSystem.CancelIdentityMoment(Runtime.CognitionIdentityMomentsWorld, id, underRuntimeGate);
        public bool IsIdentityMomentProviderCurrent(global::ClankerWorld.Simulation.World.CognitionSystem.PendingIdentityMoment pending) => Runtime.IsIdentityMomentProviderCurrent(pending);
    }

    private ICognitionMemoriesWorld CognitionMemoriesWorld => new CognitionMemoriesAdapter(this);

    private sealed class CognitionMemoriesAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionMemoriesWorld
    {
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
        public void RecordMemorySummaryState(global::ClankerWorld.Simulation.Society.SocietyCheckpoint next) => Runtime.society.Apply(_ => new global::ClankerWorld.Simulation.Society.SocietyOperationResult(next));
        public void RecordMemoryCompactionState(global::ClankerWorld.Simulation.Society.SocietyCheckpoint next) => Runtime.society.Apply(_ => new global::ClankerWorld.Simulation.Society.SocietyOperationResult(next));
    }

    private ICognitionModelOutcomesWorld CognitionModelOutcomesWorld => new CognitionModelOutcomesAdapter(this);

    private sealed class CognitionModelOutcomesAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionModelOutcomesWorld
    {
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
    }

    private ICognitionModelTasksWorld CognitionModelTasksWorld => new CognitionModelTasksAdapter(this);

    private sealed class CognitionModelTasksAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionModelTasksWorld
    {
        public bool IsConversationBusy(string agentId) => Runtime.IsConversationBusy(agentId);
        public bool IsQueuedObservationReady(global::ClankerWorld.Simulation.Cognition.AgentObservation observation) => Runtime.IsQueuedObservationReady(observation);
        public global::ClankerWorld.Simulation.Cognition.AgentObservation RefreshOfferedPlans(global::ClankerWorld.Simulation.Cognition.AgentObservation observation) => Runtime.RefreshOfferedPlans(observation);
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Society.SocietyDeferredCognitionRequest> PreviewModelRequests(global::System.Collections.Generic.IReadOnlySet<string> excludedIds, global::System.Func<global::ClankerWorld.Simulation.Cognition.AgentObservation, bool>? observationIsCurrent, global::System.Func<global::ClankerWorld.Simulation.Cognition.AgentObservation, global::ClankerWorld.Simulation.Cognition.AgentObservation>? refreshObservation = null) => Runtime.society.PreviewModelRequests(excludedIds, observationIsCurrent, refreshObservation);
        public global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.ModelDecisionOutcome> ScheduleModelWork(global::ClankerWorld.Simulation.Cognition.ModelWorkKind kind, string actorId, string requestId, global::System.Func<global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.ModelDecisionOutcome>> work, global::ClankerWorld.Simulation.Cognition.CognitionDecisionRequest? decision = null, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest? conversation = null, bool startInline = false) => Runtime.ScheduleModelWork(kind, actorId, requestId, work, decision, conversation, startInline);
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
    }

    private ICognitionObservationsWorld CognitionObservationsWorld => new CognitionObservationsAdapter(this);

    private sealed class CognitionObservationsAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionObservationsWorld
    {
        public global::System.Collections.Generic.IReadOnlySet<string> PendingModelAgentIds() => Runtime.society.PendingModelAgentIds();
        public bool IsOrderDecisionObservationCurrent(global::ClankerWorld.Simulation.Cognition.AgentObservation observation) => Runtime.IsOrderDecisionObservationCurrent(observation);
        public void CloseOrdersNotUnderstood(string agentId) => Runtime.CloseOrdersNotUnderstood(agentId);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => Runtime.PendingInstructionFor(agentId);
        public bool HasNewObserverGuidanceFor(string agentId) => OrderSystem.HasNewObserverGuidanceFor(Runtime.ControlWorld, agentId);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool IsConversationBusy(string agentId) => Runtime.IsConversationBusy(agentId);
        public global::ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId) => Runtime.farmingSystem.FarmWorkFor(Runtime.farmingWorld, workerId);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? GuardianPlacementCandidate(string actor) => FamilySystem.GuardianPlacementCandidate(Runtime.FamilyGuardianWorld, actor);
        public bool IsProductionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project) => WorldRuntime.IsProductionOrderProject(instruction, project);
        public bool IsConstructionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project) => WorldRuntime.IsConstructionOrderProject(instruction, project);
        public void SetProject(string agentId, global::ClankerWorld.Simulation.World.AgentProject project) => Runtime.SetProject(agentId, project);
        public bool IsProtectiveProject(global::ClankerWorld.Simulation.World.AgentProject? project) => Runtime.IsProtectiveProject(project);
        public string? DestinationNameForModel(string? id) => CognitionSystem.DestinationNameForModel(Runtime.CognitionCandidatesWorld, id);
        public bool PauseExpiredUnfillableHouseholdRecipeProject(string agentId) => Runtime.PauseExpiredUnfillableHouseholdRecipeProject(agentId);
        public bool NeedsCognition(string agentId, global::ClankerWorld.Simulation.Cognition.CognitionIntention? current, global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string? conversationChoiceContext) => CognitionSystem.NeedsCognition(Runtime.CognitionContextWorld, agentId, current, candidates, conversationChoiceContext);
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Cognition.CognitionObserverGuidance> ObserverGuidanceFor(string agentId) => OrderSystem.ObserverGuidanceFor(Runtime.ControlWorld, agentId);
        public global::ClankerWorld.Simulation.Cognition.CognitionKnowledgeFact[] KnownMapFactsForCognition(string agentId) => Runtime.KnownMapFactsForCognition(agentId);
        public string? GuardianPlacementModelNote(string actor) => Runtime.GuardianPlacementModelNote(actor);
        public string EquipmentNote(string actor) => Runtime.EquipmentNote(actor);
        public string? ContinuityNote(string actor) => Runtime.ContinuityNote(actor);
        public string DepartureNote(string actor) => Runtime.DepartureNote(actor);
        public string? CivicNote(string actor) => TownSystem.CivicNote(Runtime.civicWorld, actor);
        public string? MedicalCareNoteCore(string actor) => Runtime.MedicalCareNoteCore(actor);
        public string? TownMembershipNote(string actor) => Runtime.townSystem.TownMembershipNote(Runtime.civicWorld, actor);
        public string? ToolMakingRequestNoteCore(string actor) => Runtime.ToolMakingRequestNoteCore(actor);
        public global::ClankerWorld.Simulation.Cognition.CognitionParentIdentity[]? InitialChildFamilyBackground(string id, global::ClankerWorld.Simulation.World.AgentState physical) => CognitionSystem.InitialChildFamilyBackground(Runtime.CognitionPersonalIdentityWorld, id, physical);
        public global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge => Runtime.knowledge;
        public bool JevEnabled => Runtime.jevEnabled;
        public bool IsAdult(string actor) => Runtime.IsAdult(actor);
        public bool EnqueueCognition(global::ClankerWorld.Simulation.Society.SocietyCognitionScheduleEntry entry) => Runtime.society.EnqueueCognition(entry);
        public int PriorityFor(global::ClankerWorld.Simulation.World.AgentState state) => CognitionSystem.PriorityFor(Runtime.CognitionCandidatesWorld, state);
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
        public string DecisionContext(global::ClankerWorld.Simulation.World.AgentState state, global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string? conversationChoiceContext = null) => CognitionSystem.DecisionContext(Runtime.CognitionContextWorld, state, candidates, conversationChoiceContext);
    }

    private ICognitionPersonalIdentityWorld CognitionPersonalIdentityWorld => new CognitionPersonalIdentityAdapter(this);

    private sealed class CognitionPersonalIdentityAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionPersonalIdentityWorld
    {
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.DeceasedAgentState> DeceasedAgents => Runtime.deceasedAgents;
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
    }

    private ICognitionWaitingRoutinesWorld CognitionWaitingRoutinesWorld => new CognitionWaitingRoutinesAdapter(this);

    private sealed class CognitionWaitingRoutinesAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionWaitingRoutinesWorld
    {
        public bool HasGuardianPlacementTask(string actor) => FamilySystem.HasGuardianPlacementTask(Runtime.FamilyGuardianWorld, actor);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => Runtime.PendingInstructionFor(agentId);
        public bool TryApplyWaitingRoutine(string id, global::ClankerWorld.Simulation.World.AgentState state) => CognitionSystem.TryApplyWaitingRoutine(Runtime.CognitionWaitingRoutinesWorld, id, state);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool IsWaitingSurvivalCandidate(string id) => WorldRuntime.IsWaitingSurvivalCandidate(id);
        public void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle) => Runtime.ApplyCandidate(agentId, state, candidateId, reportIdle);
        public bool IsAdult(string actor) => Runtime.IsAdult(actor);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.AgentState> ChildrenNeedingCare(string actor) => FamilySystem.ChildrenNeedingCare(Runtime.FamilyParenthoodWorld, actor);
        public global::System.Collections.Generic.IEnumerable<string> IllDependentsNeedingCare(string adult) => FamilySystem.IllDependentsNeedingCare(Runtime.FamilyCareWorld, adult);
        public bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange) => Runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public void CareForChild(string actor, string childId, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.CareForChild(actor, childId, originalPlan);
        public void TendToIllDependent(string adult, string dependent, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.TendToIllDependent(adult, dependent, originalPlan);
        public bool IsTownProjectCandidate(string id) => WorldRuntime.IsTownProjectCandidate(id);
        public global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor) => Runtime.KnowledgeWritingFor(actor);
        public string ExplorationGoalCandidateId(global::ClankerWorld.Simulation.World.ExplorationGoal goal) => WorldRuntime.ExplorationGoalCandidateId(goal);
        public string GatherBlacksmithInputPrefix => WorldRuntime.GatherBlacksmithInputPrefix;
        public string GatherBuildingMaterialPrefix => WorldRuntime.GatherBuildingMaterialPrefix;
        public string RareMiningPrefix => WorldRuntime.RareMiningPrefix;
        public string SupplyWorkstationPrefix => WorldRuntime.SupplyWorkstationPrefix;
        public string ReturnEmptyVesselPrefix => WorldRuntime.ReturnEmptyVesselPrefix;
        public string FillWaterJugPrefix => WorldRuntime.FillWaterJugPrefix;
        public string PullCartPrefix => global::ClankerWorld.Simulation.World.HandcartPullChoice.Prefix;
    }
}

internal interface ICognitionAdmissionWorld
{
    global::ClankerWorld.Simulation.World.ActionCheck CheckActionPlan(global::ClankerWorld.Simulation.World.ActionPlan plan);
    void RefuseChoice(global::ClankerWorld.Simulation.World.ActionPlan plan, global::ClankerWorld.Simulation.World.ActionRefusal refusal);
    global::System.Collections.Generic.Dictionary<string, string>? AttemptedActions { get; }
    global::System.Collections.Generic.IReadOnlySet<string>? WithheldActions { get; }
    void ApplyMemorySummary(string ownerId, global::ClankerWorld.Simulation.Cognition.CognitionMemorySummaryOption summary);
    void ApplyMemoryCompaction(string ownerId, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Cognition.CognitionMemoryCompactionScore> scores);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    global::System.Collections.Generic.List<string> ApplyObserverGuidanceResult(string agentId, global::ClankerWorld.Simulation.Cognition.CognitionAdmissionResult admission);
    bool ApplyMedicalConsentDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision);
    bool ApplyOrnamentDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision);
    bool ApplyAnimalConsentDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor);
    void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle);
    global::ClankerWorld.Simulation.World.FamilyTurnState GuardianPlacementActions { get; }
    bool TalkOrderHasConversation(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void ApplyTalkOrderConversationDecision(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? OrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? UrgentSurvivalCandidateFor(string agentId, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction = null);
    bool ShouldInterruptOrder(global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? orderCandidate, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? urgentCandidate);
    bool IsMarketFoodCandidate(string actor, string candidateId);
    bool ApplyMarketCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string id, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    bool IsShopFoodCandidate(string actor, string candidateId, global::ClankerWorld.Simulation.World.ShopBuyActionPlan? selectedPlan = null);
    void ApplyShopCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string candidate, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    void ExecuteOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Cognition.CognitionCandidate candidate);
    string OrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void ApplyPlannedTownCivicCandidate(string actor, string candidate, string? proposalText = null, global::System.Collections.Generic.IReadOnlyList<string>? ballot = null, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Cognition.CognitionLandTile>? landTiles = null, global::ClankerWorld.Simulation.Cognition.CognitionLandHearingChoice? hearingChoice = null, global::ClankerWorld.Simulation.Cognition.CognitionNonLandChoice? nonLandChoice = null, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.TownRuntimeState> Towns { get; }
    bool ApplyNonLandRemedyAction(global::ClankerWorld.Simulation.World.TownRuntimeState town, string actor, string candidateId, global::ClankerWorld.Simulation.World.ActionPlan? plan = null);
    bool IsTownProjectDonationCandidate(string id);
    bool ApplyTownProjectDonation(string actor, string id, global::ClankerWorld.Simulation.World.TownProjectDonationActionPlan? originalPlan = null);
    bool IsMarketCandidate(string id);
    bool ApplyToolMakingRequestDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision);
    void ApplyRegisteredCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle, global::ClankerWorld.Simulation.World.ActionPlan? plan = null);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    OrderStore Orders { get; }
    string ToWireValue(global::ClankerWorld.Simulation.World.OwnerInstructionKind kind);
    string? UnderstoodTaskFor(string? candidate);
    bool IsInstructionComplete(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void MarkCheckpointCurrent();
}

internal interface ICognitionCandidatesWorld
{
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateRegisteredCandidates(string agentId, global::ClankerWorld.Simulation.World.AgentState state, bool restrictForOrder);
    global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate WaitingCandidate(string actor, string text);
    int ComfortableFullness { get; }
    void AddFoodPlanCandidate(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.FoodActionPlan? plan, string description, int priority, string? destination = null);
    global::ClankerWorld.Simulation.World.ConsumeFoodActionPlan? PlanFoodConsumption(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot? selected = null);
    void AddMilkCandidate(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string actor);
    global::ClankerWorld.Simulation.Cognition.AgentConversation? ConversationFor(string agentId);
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> ConversationCandidates(string agentId);
    void AddRegisteredChoices(global::ClankerWorld.Simulation.World.WorldRuntime.ChoiceGroup group, global::ClankerWorld.Simulation.World.WorldRuntime.ChoiceContext context);
    bool IsAdult(string actor);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    bool IsChild(string actor);
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.PlacedBuilding> WarehousesAccessibleTo(string actor);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> RestrictCandidatesForOrder(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction order, global::ClankerWorld.Simulation.World.AgentState state);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? SelectOrderSurvivalCandidate(global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction);
    bool TalkOrderHasConversation(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool IsSurvivalCandidate(string actor, string candidateId);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? OrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    bool ShouldInterruptOrder(global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? orderCandidate, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? urgentCandidate);
    bool IsShelterOrder(string action);
    void AddHouseholdBuildingPlans(global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, global::ClankerWorld.Simulation.Society.SocietyAgent agent, bool needsWarmth, string householdId, global::System.Collections.Generic.List<string> buildingsWithoutSites);
    global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation { get; }
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    bool IsGenericFoodRecipe(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe);
    bool IsHouseholdLayout(global::ClankerWorld.Simulation.Content.BuildingDefinition? building);
    bool IsHandcartRecipe(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe);
    string ProductionOwnerFor(global::ClankerWorld.Simulation.World.PlacedBuilding? building, string workerId);
    bool HasCarriedUnreservedQuantities(string actor, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> quantities);
    bool NeedsRecipeOutput(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, string? ownerId = null, string? requestWorker = null);
    bool AnotherAgentWaitsForWorkSite(string actor, global::ClankerWorld.Simulation.Content.RecipeDefinition recipe);
    bool CanAcquireProjectInputs(global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> inputs, string? ownerId = null, string? residentId = null, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?>? reachableToolCache = null, global::System.Lazy<global::ClankerWorld.Simulation.World.PlacedBuilding[]>? accessibleWarehouses = null);
    bool TryFindRecipeSite(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, out string siteId, out global::ClankerWorld.Simulation.World.GridPoint position, string? actorId = null);
    bool HasIngredientsAtBuilding(global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> inputs, string ownerId, string buildingId);
    global::ClankerWorld.Simulation.World.RecipeProjectActionPlan PlanRecipeProject(string actor, global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, global::ClankerWorld.Simulation.World.PlacedBuilding workstation);
    global::ClankerWorld.Simulation.World.ActionCheck CheckActionPlan(global::ClankerWorld.Simulation.World.ActionPlan plan);
    int OutdoorExposure(global::ClankerWorld.Simulation.World.GridPoint position);
    global::ClankerWorld.Simulation.World.AnimalState? Animal(string id);
    global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.TownRuntimeState> Towns { get; }
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
}

internal interface ICognitionConstructionWorld
{
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    global::ClankerWorld.Simulation.World.ConstructionActionPlan? RetainedConstruction(string actor, global::ClankerWorld.Simulation.World.AgentProject project);
    global::ClankerWorld.Simulation.World.ActionCheck CheckConstruction(global::ClankerWorld.Simulation.World.ConstructionActionPlan plan, bool checkDestination = true, global::ClankerWorld.Simulation.World.ActorCapabilityPolicy? policy = null);
    void RefuseChoice(global::ClankerWorld.Simulation.World.ActionPlan plan, global::ClankerWorld.Simulation.World.ActionRefusal refusal);
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    global::ClankerWorld.Simulation.World.TownLayoutContext CreateTownLayoutContext(string actor, global::ClankerWorld.Simulation.World.GridPoint? selectedSite = null, global::ClankerWorld.Simulation.Content.BuildingDefinition? building = null, bool forTownProject = false, string? townProjectId = null);
    string? TownForResident(string residentId);
    bool IsHouseholdBuildingTag(string tag);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId);
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? ConstructionInstructionForProject(string actor, global::ClankerWorld.Simulation.World.AgentProject? project);
    int ProjectWorkTicks { get; }
    bool TryConstructionOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, out global::ClankerWorld.Simulation.Content.BuildingDefinition? definition, out string? household, out global::ClankerWorld.Simulation.World.GridPoint position, out string failure);
    string BuildingConstructionOwner(string actor, global::ClankerWorld.Simulation.Content.BuildingDefinition definition);
    global::ClankerWorld.Simulation.World.BuildingPlacementResult PlaceBuildingCore(string instanceId, string definitionId, global::ClankerWorld.Simulation.World.GridPoint position, string eventKind, string? assignedTownId = null, string? householdId = null, string? constructionOwnerId = null, string? constructionInstructionId = null, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.ConstructionMaterialInput>? selectedMaterials = null);
    string BuildInstanceId(string agentId, global::ClankerWorld.Simulation.Content.BuildingDefinition definition);
    void CreditCompletedWork(string actor, string domain, global::ClankerWorld.Simulation.World.SkillKind? skill = null);
    void RecordConstructionOrderCompletion(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string materialOwner);
    global::ClankerWorld.Simulation.World.RecipeProjectActionPlan? RetainedRecipeProject(string actor, global::ClankerWorld.Simulation.World.AgentProject project);
    bool TryFindRecipeSite(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, out string siteId, out global::ClankerWorld.Simulation.World.GridPoint position, string? actorId = null);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    bool IsProductionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project);
    global::ClankerWorld.Simulation.World.ProductionStartResult StartProductionCore(string recipeId, string buildingInstanceId, string workerId, string eventKind, string? orderInstructionId = null, global::ClankerWorld.Simulation.World.RecipeProjectActionPlan? originalPlan = null);
}

internal interface ICognitionContextWorld
{
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    bool IsFinishedOrderDecisionAwaitingReply(global::ClankerWorld.Simulation.Cognition.AgentObservation observation);
    OrderStore Orders { get; }
    bool IsInstructionComplete(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    string? UnderstoodTaskFor(string? candidate);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    bool HasNewObserverGuidanceFor(string agentId);
    long WorldTick { get; }
    long CognitionReevaluationIntervalTicks { get; }
    bool CanContinueEquipmentRepair(string actor);
    bool CanContinueLesson(string actor, bool waitingForModel = false);
    bool CanContinueProject(global::ClankerWorld.Simulation.World.AgentState state, bool waitingForModel = false);
    string DecisionContext(global::ClankerWorld.Simulation.World.AgentState state, global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string? conversationChoiceContext = null);
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    string MedicalAllowPrefix { get; }
}

internal interface ICognitionContinuationWorld
{
    global::ClankerWorld.Simulation.Society.SocietyWorldCapture Capture();
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::System.Collections.Generic.IReadOnlySet<string>? WithheldActions { get; }
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    global::System.Collections.Generic.Dictionary<string, string>? AttemptedActions { get; }
    global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateCandidates(string agentId, global::ClankerWorld.Simulation.World.AgentState state, bool restrictForOrder = true);
    void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    bool TryApplyWaitingRoutine(string id, global::ClankerWorld.Simulation.World.AgentState state);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    bool ContinueGuardianPlacement(string actor);
    bool IsConversationBusy(string agentId);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? OrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? UrgentSurvivalCandidateFor(string agentId, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction = null);
    bool ShouldInterruptOrder(global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? orderCandidate, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? urgentCandidate);
    bool IsMarketFoodCandidate(string actor, string candidateId);
    void ContinueMarketWalk(string actor, string id);
    void ExecuteOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Cognition.CognitionCandidate candidate);
    string OrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    bool CanContinueEquipmentRepair(string actor);
    global::ClankerWorld.Simulation.World.EquipmentRepairWork? ContinueEquipmentRepair(string actor);
    bool CanContinueLesson(string actor, bool waitingForModel = false);
    void ContinueLesson(string actor, bool waitingForModel = false, global::ClankerWorld.Simulation.World.LearningActionPlan? originalPlan = null);
    bool ContinueFarmWork(string workerId);
    bool CanContinueProject(global::ClankerWorld.Simulation.World.AgentState state, bool waitingForModel = false);
    void ContinueProject(string agentId, global::ClankerWorld.Simulation.World.AgentState state);
    string NonLandRemedyPrefix { get; }
    global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.TownRuntimeState> Towns { get; }
    bool ApplyNonLandRemedyAction(global::ClankerWorld.Simulation.World.TownRuntimeState town, string actor, string candidateId, global::ClankerWorld.Simulation.World.ActionPlan? plan = null);
    string NonLandRelayPrefix { get; }
    void ContinueTownCivicVisit(string actor, string candidate);
    void ContinueTownLandSaleWalk(string actor, string candidate);
    bool IsTownProjectDonationCandidate(string id);
    void ContinueTownProjectDonationWalk(string actor, string id);
    bool IsMarketCandidate(string id);
    bool IsOrnamentCandidate(string candidate);
    void ContinueOrnamentWalk(string actor, string candidate);
    bool CanContinuePendingTask(global::ClankerWorld.Simulation.World.AgentState state, string id);
}

internal interface ICognitionConversationTasksWorld
{
    CognitionSystem Host { get; }
    void ApplyConversationTurnOutcome(global::ClankerWorld.Simulation.World.CognitionSystem.PendingConversationTurn pending, global::ClankerWorld.Simulation.World.CognitionSystem.ConversationTurnOutcome outcome, long worldTick, global::System.Func<global::ClankerWorld.Simulation.World.CognitionSystem.PendingConversationTurn, bool> providerRouteIsCurrent);
    int MaximumConversationProviderCallsPerTick { get; }
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.Cognition.AgentConversation> Conversations { get; }
    bool CanContinueConversation(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation);
    void SuspendConversation(string conversationId, global::ClankerWorld.Simulation.Cognition.AgentConversationInterruption interruption);
    long WorldTick { get; }
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest CreateConversationRequest(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation, string speakerId);
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    bool CanProposeMarriage(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation);
    string? RequestedConversationActivity(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation, string speaker);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.AgentMarriage> Marriages { get; }
    void FinishModelWork(string actorId, string requestId, global::ClankerWorld.Simulation.Cognition.ModelWorkStage stage, long? admittedTick = null);
    void CancelProviderCall(global::System.Threading.CancellationTokenSource cancellation, bool underRuntimeGate);
    void CancelPendingConversationTurn(string conversationId, global::ClankerWorld.Simulation.Cognition.AgentConversationInterruption interruption, bool suspendCurrent = true, bool underRuntimeGate = true);
    void MarkModelReplyAdmitted(string requestId);
    void RecordStartedConversation(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation);
    bool ApplyConversationTurnOutcome(global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest request, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnResponse? response, Exception? failure, long tick, Func<bool> providerRouteIsCurrent);
    Task<CognitionSystem.ConversationTurnOutcome> RunConversationProviderUnderGate(global::ClankerWorld.Simulation.Cognition.IAgentConversationProvider provider, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest request, CancellationToken cancellationToken);
}

internal interface ICognitionIdentityMomentsWorld
{
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    long WorldTick { get; }
    global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents { get; }
    void PrepareIdentityMoments(global::System.Action<global::ClankerWorld.Simulation.World.CognitionSystem.PreparedIdentityMoment> prepare, global::System.Collections.Generic.IReadOnlySet<string>? activeModelCallIds = null, global::System.Collections.Generic.IReadOnlySet<string>? activeIdentityIds = null);
    void StartPreparedIdentityMoment(global::ClankerWorld.Simulation.World.CognitionSystem.PreparedIdentityMoment prepared);
    CognitionSystem Host { get; }
    bool IsConversationBusy(string agentId);
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.Cognition.AgentObservation CreateIdentityMomentObservation(global::ClankerWorld.Simulation.Society.SocietyAgent person, global::ClankerWorld.Simulation.World.AgentState physical, global::ClankerWorld.Simulation.World.AgentIdentityMoment moment);
    void FinishIdentityMoment(string id, global::ClankerWorld.Simulation.World.AgentIdentityMomentKind kind, string outcome, string? personality = null, string? aspiration = null);
    void SetIdentityMoment(string id, global::ClankerWorld.Simulation.World.AgentIdentityMoment moment);
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    global::System.TimeProvider ModelTimeProvider { get; }
    global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.IdentityMomentOutcome> ScheduleModelWork(global::ClankerWorld.Simulation.Cognition.ModelWorkKind kind, string actorId, string requestId, global::System.Func<global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.IdentityMomentOutcome>> work, global::ClankerWorld.Simulation.Cognition.CognitionDecisionRequest? decision = null, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest? conversation = null, bool startInline = false);
    void CancelIdentityMoment(string id, bool underRuntimeGate = true);
    void FinishModelWork(string actorId, string requestId, global::ClankerWorld.Simulation.Cognition.ModelWorkStage stage, long? admittedTick = null);
    void CancelProviderCall(global::System.Threading.CancellationTokenSource cancellation, bool underRuntimeGate);
    bool IsIdentityMomentProviderCurrent(global::ClankerWorld.Simulation.World.CognitionSystem.PendingIdentityMoment pending);
    void MarkCheckpointCurrent();
    void MarkModelReplyAdmitted(string requestId);
}

internal interface ICognitionMemoriesWorld
{
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    long WorldTick { get; }
    CognitionSystem Host { get; }
    void MarkCheckpointCurrent();
    void RecordMemorySummaryState(global::ClankerWorld.Simulation.Society.SocietyCheckpoint next);
    void RecordMemoryCompactionState(global::ClankerWorld.Simulation.Society.SocietyCheckpoint next);
}

internal interface ICognitionModelOutcomesWorld
{
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    long WorldTick { get; }
    global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    void RecordModelAttempt(string id, string status, string? acceptedCandidateId = null, string? setupBlocker = null);
    void MarkCheckpointCurrent();
}

internal interface ICognitionModelTasksWorld
{
    CognitionSystem Host { get; }
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    bool IsConversationBusy(string agentId);
    bool ShouldDispatchConversationChoice(string agentId);
    global::ClankerWorld.Simulation.Society.SocietyWorldCapture Capture();
    bool IsQueuedObservationReady(global::ClankerWorld.Simulation.Cognition.AgentObservation observation);
    global::ClankerWorld.Simulation.Cognition.AgentObservation RefreshOfferedPlans(global::ClankerWorld.Simulation.Cognition.AgentObservation observation);
    global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Society.SocietyDeferredCognitionRequest> PreviewModelRequests(global::System.Collections.Generic.IReadOnlySet<string> excludedIds, global::System.Func<global::ClankerWorld.Simulation.Cognition.AgentObservation, bool>? observationIsCurrent, global::System.Func<global::ClankerWorld.Simulation.Cognition.AgentObservation, global::ClankerWorld.Simulation.Cognition.AgentObservation>? refreshObservation = null);
    global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.ModelDecisionOutcome> ScheduleModelWork(global::ClankerWorld.Simulation.Cognition.ModelWorkKind kind, string actorId, string requestId, global::System.Func<global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.ModelDecisionOutcome>> work, global::ClankerWorld.Simulation.Cognition.CognitionDecisionRequest? decision = null, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest? conversation = null, bool startInline = false);
    void RecordModelAttempt(string id, string status, string? acceptedCandidateId = null, string? setupBlocker = null);
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    bool IsUndeliveredFinishedOrderDecision(global::ClankerWorld.Simulation.Cognition.AgentObservation observation);
    long WorldTick { get; }
    string? ConversationChoiceContextFor(string agentId);
    void FinishModelWork(string actorId, string requestId, global::ClankerWorld.Simulation.Cognition.ModelWorkStage stage, long? admittedTick = null);
    void CancelProviderCall(global::System.Threading.CancellationTokenSource cancellation, bool underRuntimeGate);
}

internal interface ICognitionObservationsWorld
{
    global::ClankerWorld.Simulation.Society.SocietyWorldCapture Capture();
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::System.Collections.Generic.IReadOnlySet<string> PendingModelAgentIds();
    bool IsOrderDecisionObservationCurrent(global::ClankerWorld.Simulation.Cognition.AgentObservation observation);
    bool IsUndeliveredFinishedOrderDecision(global::ClankerWorld.Simulation.Cognition.AgentObservation observation);
    void CloseOrdersNotUnderstood(string agentId);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    bool HasNewObserverGuidanceFor(string agentId);
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    bool IsConversationBusy(string agentId);
    global::ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId);
    bool ShouldDispatchConversationChoice(string agentId);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? GuardianPlacementCandidate(string actor);
    bool IsProductionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project);
    bool IsConstructionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project);
    void SetProject(string agentId, global::ClankerWorld.Simulation.World.AgentProject project);
    bool IsProtectiveProject(global::ClankerWorld.Simulation.World.AgentProject? project);
    string? ConversationChoiceContextFor(string agentId);
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateCandidates(string agentId, global::ClankerWorld.Simulation.World.AgentState state, bool restrictForOrder = true);
    string? DestinationNameForModel(string? id);
    bool PauseExpiredUnfillableHouseholdRecipeProject(string agentId);
    bool NeedsCognition(string agentId, global::ClankerWorld.Simulation.Cognition.CognitionIntention? current, global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string? conversationChoiceContext);
    long WorldTick { get; }
    global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Cognition.CognitionObserverGuidance> ObserverGuidanceFor(string agentId);
    global::ClankerWorld.Simulation.Cognition.CognitionKnowledgeFact[] KnownMapFactsForCognition(string agentId);
    global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.TownRuntimeState> Towns { get; }
    string? GuardianPlacementModelNote(string actor);
    string EquipmentNote(string actor);
    string? ContinuityNote(string actor);
    string DepartureNote(string actor);
    string? CivicNote(string actor);
    string? MedicalCareNoteCore(string actor);
    string? TownMembershipNote(string actor);
    string? ToolMakingRequestNoteCore(string actor);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.AgentMarriage> Marriages { get; }
    global::ClankerWorld.Simulation.Cognition.CognitionParentIdentity[]? InitialChildFamilyBackground(string id, global::ClankerWorld.Simulation.World.AgentState physical);
    global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge { get; }
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    bool JevEnabled { get; }
    bool IsAdult(string actor);
    CognitionSystem Host { get; }
    bool EnqueueCognition(global::ClankerWorld.Simulation.Society.SocietyCognitionScheduleEntry entry);
    int PriorityFor(global::ClankerWorld.Simulation.World.AgentState state);
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    OrderStore Orders { get; }
    global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents { get; }
    string DecisionContext(global::ClankerWorld.Simulation.World.AgentState state, global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string? conversationChoiceContext = null);
    void MarkCheckpointCurrent();
}

internal interface ICognitionPersonalIdentityWorld
{
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.DeceasedAgentState> DeceasedAgents { get; }
    global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    void MarkCheckpointCurrent();
}

internal interface ICognitionWaitingRoutinesWorld
{
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    bool HasGuardianPlacementTask(string actor);
    global::ClankerWorld.Simulation.World.FamilyTurnState GuardianPlacementActions { get; }
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    bool TryApplyWaitingRoutine(string id, global::ClankerWorld.Simulation.World.AgentState state);
    global::System.Collections.Generic.IReadOnlySet<string>? WithheldActions { get; }
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateCandidates(string agentId, global::ClankerWorld.Simulation.World.AgentState state, bool restrictForOrder = true);
    bool IsWaitingSurvivalCandidate(string id);
    void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle);
    bool IsAdult(string actor);
    bool ReadyForBriefInteraction(string actor);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.AgentState> ChildrenNeedingCare(string actor);
    global::System.Collections.Generic.IEnumerable<string> IllDependentsNeedingCare(string adult);
    bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange);
    int ResourceInteractionRange { get; }
    void CareForChild(string actor, string childId, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    void TendToIllDependent(string adult, string dependent, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    bool IsTownProjectCandidate(string id);
    global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor);
    string ExplorationGoalCandidateId(global::ClankerWorld.Simulation.World.ExplorationGoal goal);
    string GatherBlacksmithInputPrefix { get; }
    string GatherBuildingMaterialPrefix { get; }
    string RareMiningPrefix { get; }
    string SupplyWorkstationPrefix { get; }
    string ReturnEmptyVesselPrefix { get; }
    string FillWaterJugPrefix { get; }
    string PullCartPrefix { get; }
}
