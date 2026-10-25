using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IFamilyPracticalCareWorld FamilyPracticalCareWorld => new FamilyWorldAdapter(this);
    private IFamilyChildWorld FamilyChildWorld => new FamilyWorldAdapter(this);
    private IFamilyWillWorld FamilyWillWorld => new FamilyWorldAdapter(this);
    private IFamilyMedicalWorld FamilyMedicalWorld => new FamilyWorldAdapter(this);
    private IFamilyTreatmentWorld FamilyTreatmentWorld => new FamilyWorldAdapter(this);
    private IFamilySupplyWorld FamilySupplyWorld => new FamilyWorldAdapter(this);
    private IFamilyParenthoodWorld FamilyParenthoodWorld => new FamilyWorldAdapter(this);
    private IFamilyGuardianWorld FamilyGuardianWorld => new FamilyWorldAdapter(this);
    private IFamilyCareWorld FamilyCareWorld => new FamilyWorldAdapter(this);
    private IFamilyPartnershipWorld FamilyPartnershipWorld => new FamilyWorldAdapter(this);
    private IFamilyContinuityWorld FamilyContinuityWorld => new FamilyWorldAdapter(this);

    private sealed class FamilyWorldAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), IFamilyContinuityWorld, IFamilyPartnershipWorld, IFamilyCareWorld, IFamilyGuardianWorld, IFamilyParenthoodWorld, IFamilyTreatmentWorld, IFamilySupplyWorld, IFamilyWillWorld, IFamilyChildWorld, IFamilyPracticalCareWorld
    {
        public IFamilyContinuityWorld ContinuityWorld => this;
        public bool CanHaveChildren(string actor) => FamilySystem.CanHaveChildren(Runtime.FamilyParenthoodWorld, actor);
        public bool EligibleContinuityParents(string actor, string other) => FamilySystem.EligibleContinuityParents(Runtime.FamilyParenthoodWorld, actor, other);
        public SocietyRelationship ProposePartnership(string actor, string partner) => FamilySystem.ProposePartnership(Runtime.FamilyPartnershipWorld, actor, partner);
        public void AcceptPartnership(SocietyRelationship proposal) => FamilySystem.AcceptPartnership(Runtime.FamilyPartnershipWorld, proposal);
        public int TicksPerDay => Runtime.worldSystems.Config.TicksPerDay;
        public bool IsAdult(string actor) => Runtime.IsAdult(actor);
        public int TrustScore(string owner, string subject) => CommunitySystem.TrustScore(Runtime.CommunitySkillWorld, owner, subject);
        public string NewPartnershipId(string actor, string partner, long tick) => WorldRuntime.NewPartnershipId(actor, partner, tick);
        public void ProposePartnership(SocietyRelationshipProposal relationship) => Runtime.society.Apply(checkpoint => SocietyRules.ProposeRelationship(checkpoint, relationship));
        public void AcceptPartnership(string id, int revision, string actor) => Runtime.society.Apply(checkpoint => SocietyRules.AcceptRelationship(checkpoint, id, revision, actor));
        public void RefusePartnership(string id, int revision, string actor) => Runtime.society.Apply(checkpoint => SocietyRules.RefuseRelationship(checkpoint, id, revision, actor));
        public void RevokePartnership(string id, string actor) => Runtime.society.Apply(checkpoint => SocietyRules.RevokeRelationship(checkpoint, id, actor));
        public void MaintainMarriages() => Runtime.MaintainMarriages();
        public long GuardianStageTicks => Runtime.GuardianStageTicks;
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public int IllnessCareReliefBasisPoints => WorldRuntime.IllnessCareReliefBasisPoints;
        public bool RecentlyCaredForIllness(string child) => Runtime.RecentlyCaredForIllness(child);
        public string[] GuardianAdultsForStage(string child, string stage) => Runtime.GuardianAdultsForStage(child, stage);
        public string[] NewGuardianAdultsForStage(string child, string stage) => FamilySystem.NewGuardianAdultsForStage(Runtime.society.Checkpoint, Runtime.towns, child, stage);
        public string? TownForResident(string actor) => Runtime.TownForResident(actor);
        public PlacedBuilding? HouseholdBuildingWithTag(string household, string tag) => Runtime.HouseholdBuildingWithTag(household, tag);
        public HouseResidentCapacityRules.Capacity? HouseResidentCapacity(string household, SocietyAgent proposed) => Runtime.HouseResidentCapacity(household, proposed);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range) => Runtime.IsWithinInteractionRange(origin, destination, range);
        public PracticalCareActionPlan? OfferPracticalCare(string actor, PracticalCareChoice choice) => global::ClankerWorld.Simulation.World.WorldRuntime.FamilyActions.PracticalCare.Offer(Runtime, actor, choice);
        public string NewCareRelationshipId(string actor, string target, long tick) => WorldRuntime.NewCareRelationshipId(actor, target, tick);
        public void ProposeCare(SocietyRelationshipProposal proposal) => Runtime.society.Apply(checkpoint => SocietyRules.ProposeRelationship(checkpoint, proposal));
        public void AcceptCare(string id, int revision, string actor) => Runtime.society.Apply(checkpoint => SocietyRules.AcceptRelationship(checkpoint, id, revision, actor));
        public void RefuseCare(string id, int revision, string actor) => Runtime.society.Apply(checkpoint => SocietyRules.RefuseRelationship(checkpoint, id, revision, actor));
        public void RevokeCare(string id, string actor) => Runtime.society.Apply(checkpoint => SocietyRules.RevokeRelationship(checkpoint, id, actor));
        public SocietyOperationResult AssumePrimaryCare(string adult, string child) => Runtime.society.Apply(checkpoint => SocietyRules.AssumePrimaryCare(checkpoint, adult, child));
        public SocietyOperationResult AssumeInfantCare(string adult, string child) => Runtime.society.Apply(checkpoint => SocietyRules.AssumeInfantCare(checkpoint, adult, child));
        public SocietyOperationResult AcceptDependentGuardianship(string adult, string child, string? destination) => Runtime.society.Apply(checkpoint => SocietyRules.AcceptDependentGuardianship(checkpoint, adult, child, destination));
        public void ReconcileGuardianPlacements() => FamilySystem.ReconcileGuardianPlacements(Runtime.FamilyGuardianWorld);
        public void BeginGuardianPlacement(string child, string adult, string care) => FamilySystem.BeginGuardianPlacement(Runtime.FamilyGuardianWorld, child, adult, care);
        public void SetHousing(string child, HousingState housing) => CommunitySystem.SetHousing(Runtime.CommunityHousingWorld, child, housing);
        public string? HousingBlocker(string child) => Runtime.HousingBlocker(child);
        public FamilyTurnState Turn => Runtime.familyTurn;
        public PlacedBuilding? HouseForHousehold(string household) => Runtime.HouseForHousehold(household);
        public OwnerQueuedInstruction? PendingInstructionFor(string actor) => Runtime.PendingInstructionFor(actor);
        public bool IsConversationBusy(string actor) => Runtime.IsConversationBusy(actor);
        public bool CanFitGuardianHousehold(string household, string adult, string child) => FamilySystem.CanFitGuardianHousehold(Runtime.FamilyCareWorld, household, adult, child);
        public GuardianTravelActionPlan? OfferGuardianTravel(string actor, GuardianTravelChoice choice) => global::ClankerWorld.Simulation.World.WorldRuntime.FamilyActions.GuardianTravel.Offer(Runtime, actor, choice);
        public GuardianTravelActionPlan? PlanGuardianTravel(string actor, GuardianTravelChoice choice) => FamilySystem.PlanGuardianTravel(Runtime.FamilyGuardianWorld, actor, choice);
        public CognitionCandidate? UrgentSurvivalCandidateFor(string actor, AgentState person) => OrderSystem.UrgentSurvivalCandidateFor(Runtime.OrderSurvivalWorld, actor, person, null);
        public void ApplyCandidate(string actor, AgentState person, string candidate, bool reportIdle) => Runtime.ApplyCandidate(actor, person, candidate, reportIdle);
        public bool TryApplyGuardianPlacementCandidate(string actor, string candidate, ActionPlan? plan) => Runtime.TryApplyGuardianPlacementCandidate(actor, candidate, plan);
        public int CaregiverFoodCarryRequirement(string actor, AgentState person) => FamilySystem.CaregiverFoodCarryRequirement(Runtime.FamilyParenthoodWorld, actor, person);
        public void MakeRoomForFood(string actor, AgentState person) => Runtime.MakeRoomForFood(actor, person);
        public void CareForChild(string actor, string child) => Runtime.CareForChild(actor, child);
        public void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range) => Runtime.MoveToward(actor, person, destination, reason, range);
        public FarmFieldState? FarmWorkFor(string actor) => Runtime.farmingSystem.FarmWorkFor(Runtime.farmingWorld, actor);
        public void CancelFarmWork(FarmFieldState field) => Runtime.farmingSystem.CancelFarmWork(Runtime.farmingWorld, field);
        public void CancelEquipmentRepair(string actor) => Runtime.CancelEquipmentRepair(actor);
        public void SetProject(string actor, AgentProject project) => Runtime.SetProject(actor, project);
        public bool IsActionWithheld(string actor) => Runtime.withheldActions?.Contains(actor) == true;
        public SocietyOperationResult PlaceDependentWithGuardian(string adult, string child, string care, int revision, string household) => Runtime.society.Apply(checkpoint => SocietyRules.PlaceDependentWithGuardian(checkpoint, adult, child, care, revision, household));
        public void RestoreHouseholdDeliveries(string actor, string household, string transition) => Runtime.RestoreHouseholdDeliveries(actor, household, transition);
        public bool MoveDependentToGuardianTown(string child, string adult) => Runtime.townSystem.MoveDependentToGuardianTown(Runtime.civicWorld, child, adult);
        public bool CloseKin(string actor, string other) => FamilySystem.CloseKin(Runtime.FamilyPartnershipWorld, actor, other);
        public IEnumerable<SocietyRelationship> Partnerships(string actor) => FamilySystem.Partnerships(Runtime.FamilyPartnershipWorld, actor);
        public bool ReadyForLesson(string actor) => CommunitySystem.ReadyForLesson(Runtime.CommunityLearningWorld, actor);
        public bool NeedsCaregiver(string child) => FamilySystem.NeedsCaregiver(Runtime.FamilyCareWorld, child);
        public IEnumerable<PlacedBuilding> AccessibleShelters(string actor, bool includeStormRefuge) => Runtime.AccessibleShelters(actor, includeStormRefuge);
        public ParenthoodFood ParenthoodFoodReadiness(string actor) => WorldRuntime.ParenthoodFoodReadiness(Runtime.society.Checkpoint, actor, Runtime.towns);
        public IEnumerable<InventoryLot> PreferredFood(string owner, string actor) => Runtime.PreferredFood(owner, actor);
        public int MinimumFoodPickupCarryUnits(string actor, GridPoint origin, bool forDependent) => Runtime.MinimumFoodPickupCarryUnits(actor, origin, forDependent);
        public bool ContinuityPlanDue(string actor, string partner) => Runtime.familySystem.ContinuityPlanDue(Runtime.FamilyContinuityWorld, actor, partner);
        public ContinuityCouple? ContinuityCoupleOf(string actor) => Runtime.familySystem.ContinuityCoupleOf(Runtime.FamilyContinuityWorld, actor);
        public ContinuityCouple? ContinuityCoupleFor(string actor, string partner) => Runtime.familySystem.ContinuityCoupleFor(Runtime.FamilyContinuityWorld, actor, partner);
        public bool ContinuityAllowsPostponement(ContinuityCouple couple) => FamilySystem.ContinuityAllowsPostponement(Runtime.FamilyContinuityWorld, couple);
        public string ParenthoodRequestId(string actor, long tick) => WorldRuntime.ParenthoodRequestId(actor, tick);
        public void CommitBirth(SocietyBirthRequest request) => Runtime.society.Apply(checkpoint => SocietyRules.CommitBirth(checkpoint, request));
        public void AddTownResident(string town, string child, string eventKind) => Runtime.townSystem.AddTownResident(Runtime.civicWorld, town, child, eventKind);
        public void ConsumeCareFood(string actor, string lot) => Runtime.society.Apply(checkpoint => SocietyRules.ConsumeInventory(checkpoint, actor, lot, 1));
        public int FoodNourishment(string kind) => WorldRuntime.FoodNourishment(kind);
        public SurvivalCondition? AfterMeal(AgentState person, InventoryLot food) => Runtime.AfterMeal(person, food);
        public long NextEventId => Runtime.nextEventId;
        public IEnumerable<GoodsMatch> MedicalDoseStock(string actor) => Runtime.MedicalDoseStock(actor);
        public bool HasCarriedOwnItem(string actor, string kind) => Runtime.HasCarriedOwnItem(actor, kind);
        public InventoryLot? SharedPreparationItem(string kind, string actor) => Runtime.SharedPreparationItem(kind, actor);
        public string MedicalDoseId(long tick, long eventId, string caregiver, string patient) => WorldRuntime.MedicalDoseId(tick, eventId, caregiver, patient);
        public void ConsumeMedicalDose(string id, InventoryLot supply, string caregiver, string patient, string kind) => Runtime.RunInventoryCommand(InventorySystem.ConsumeMedicalDose(Runtime.InventoryFamilyWorld, id, supply, caregiver, patient, kind));
        public void CloseMedicalDoseReceipt(Identity reservation, string purpose) => Runtime.RunInventoryCommand(InventorySystem.CloseMedicalDoseReceipt(reservation, purpose));
        public InventoryLot? SharedItem(string kind, string actor, GridPoint destination, int range) => Runtime.SharedItem(kind, actor, destination, range);
        public int HouseholdStockInteractionRange(InventoryLot lot) => WorldRuntime.HouseholdStockInteractionRange(lot);
        public ActionCheck CheckEquipmentPickup(string actor, PlannedEquipmentPickup pickup, string description, ReachPurpose purpose) => Runtime.CheckEquipmentPickup(actor, pickup, description, purpose);
        public bool CanPrepareSharedPickup(string actor, InventoryLot lot, GridPoint destination, int range) => Runtime.CanPrepareSharedPickup(actor, lot, destination, range);
        public void ApplyPlannedEquipmentPickup(string actor, PlannedEquipmentPickup pickup) => Runtime.ApplyPlannedEquipmentPickup(actor, pickup);
        public Task<T> ScheduleWillWork<T>(CognitionDecisionRequest request, Func<Task<T>> work) =>
            Runtime.ScheduleModelWork(ModelWorkKind.Will, request.Observation.AgentId, request.RequestId, work, decision: request);
        public void RecordWillReplyAdmission(string requestId) => Runtime.admittedModelReplies.Add(requestId);
        public void CancelWillWork(CognitionDecisionRequest request) =>
            Runtime.FinishModelWork(request.Observation.AgentId, request.RequestId, ModelWorkStage.Cancelled);
        public IDecisionProvider? CreateProvider(string actor) => Runtime.providerFactory?.Invoke(actor);
        public string? CognitionHouseholdId(string? household) => WorldRuntime.CognitionHouseholdId(household);
        public int DestinationRoom(string warehouse) => Runtime.DestinationRoom(warehouse);
        public void MarkWillStarted(string estate) => Runtime.society.Apply(checkpoint => SocietyRules.MarkWillStarted(checkpoint, estate));
        public SocietyOperationResult ResolveWill(string estate, SocietyWillDirective? directive, string reason, string? finalWords, IReadOnlySet<string>? towns) => Runtime.society.Apply(checkpoint => SocietyRules.ResolveWill(checkpoint, estate, directive, reason, finalWords, towns));
        public IReadOnlyDictionary<string, ResourceState> Resources => Runtime.resources;
        public ReadOnlyCollection<GridPoint> ReachLeg(string actor, GridPoint origin, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) => Runtime.ReachLeg(actor, origin, target, purpose, range, load, swimming);
        public int AvailableLotQuantity(InventoryLot lot) => Runtime.AvailableLotQuantity(lot);
        public bool CanDeliverHouseDelivery(InventoryLot lot) => Runtime.CanDeliverHouseDelivery(lot);
        public GoodsAnswer FindGoods(GoodsRequest request) => Runtime.FindGoods(request);
        public bool OnBorrowedMarketStall(InventoryLot lot) => Runtime.OnBorrowedMarketStall(lot);
        public int FoodHarvestCarryUnits(MapResource resource) => WorldRuntime.FoodHarvestCarryUnits(resource);
        public int FoodHarvestQuantity(MapResource resource) => WorldRuntime.FoodHarvestQuantity(resource);
        public InventoryLot? CarriedHouseDelivery(string actor) => Runtime.CarriedHouseDelivery(actor);
        public InventoryLot? PersonalSpareFood(string actor, string? sourceLotId = null) => Runtime.PersonalSpareFood(actor, sourceLotId: sourceLotId);
        public string ChildSocialMemoryId(string kind, string actor, string target, long tick) => WorldRuntime.ChildSocialMemoryId(kind, actor, target, tick);
        public void IncreaseTrust(string actor, string target, int amount, string reason) => CommunitySystem.IncreaseTrust(Runtime.CommunitySkillWorld, actor, target, amount, reason);
        public void OfferChildChoice(List<CognitionCandidate> candidates, string actor, ChildChoice choice, string description, int priority, string? destinationId = null) => Runtime.OfferChildChoice(candidates, actor, choice, description, priority, destinationId);
        public void RecordChildSocialMemory(string id, string actor, string target, string kind) => Runtime.society.Apply(checkpoint => SocietyRules.RecordSocialMemory(checkpoint,
            new(Identity.Parse(id), actor, target, kind switch { "converse" => "Talked with another person.", "play" => "Played with another person.", _ => "Learned by observing an adult." }, "public", Runtime.WorldTick)));
        public void DeliverChildLoad(ChildActionPlan plan) => Runtime.RunInventoryCommand(InventorySystem.DeliverChildLoad(Runtime.InventoryFamilyWorld, plan));
        public void CollectChildLoad(ChildActionPlan plan) => Runtime.RunInventoryCommand(InventorySystem.CollectChildLoad(Runtime.InventoryFamilyWorld, plan));
        public void GatherChildLoad(ChildActionPlan plan)
        {
            var resource = Runtime.map.Resources.Single(source => source.Id == plan.Choice.Target);
            var state = Runtime.agents[plan.Actor];
            if (plan.Choice.Kind == ChildActionKind.GatherWood) Runtime.GatherProjectMaterial(plan.Actor, state, "wood", resource, deliveryBuildingId: plan.HouseId);
            else if (Runtime.HarvestFood(plan.Actor, state, resource) is { } food)
                Runtime.RunInventoryCommand(InventorySystem.GatherChildLoad(Runtime.InventoryFamilyWorld, food, plan));
        }
        public void ShareChildFood(ChildActionPlan plan) => Runtime.RunInventoryCommand(InventorySystem.ShareChildFood(Runtime.InventoryFamilyWorld, plan));
        public IFamilyCareWorld CareWorld => this;
        public IFamilyParenthoodWorld ParenthoodWorld => this;
        public FoodActionPlan? PlanFoodConsumption(string actor) => Runtime.PlanFoodConsumption(actor);
        public InventoryLot? AvailableHouseholdServing(string actor, GridPoint position, GridPoint returnTo, int returnRange) => Runtime.AvailableHouseholdServing(actor, position, returnTo: returnTo, returnRange: returnRange);
        public SharedFoodActionPlan? PlanSharedFood(string actor, InventoryLot selected) => Runtime.PlanSharedFood(actor, selected);
        public MapResource? AvailableFoodSource(string actor, GridPoint position) => Runtime.AvailableFoodSource(actor, position);
        public FoodResourceActionPlan? PlanFoodResource(string actor, bool harvest, MapResource selected) => Runtime.PlanFoodResource(actor, harvest, selected);
        public ActionCheck CheckFoodPlan(FoodActionPlan plan) => Runtime.CheckFoodPlan(plan);
        public void CompleteChildFoodPickup(string actor, SharedFoodActionPlan plan, GridPoint destination) => Runtime.CompleteChildFoodPickup(actor, plan, destination);
        public void CompleteFoodHarvest(string actor, MapResource resource, string kind, int quantity, int seedQuantity) => Runtime.CompleteFoodHarvest(actor, resource, kind, quantity, seedQuantity);
        public long ContinuityPostponeTicks => Runtime.ContinuityPostponeTicks;
        public bool Partners(string actor, string other) => FamilySystem.Partners(Runtime.FamilyParenthoodWorld, actor, other);
        public void SetParenthood(string actor, ParenthoodPlan plan) => FamilySystem.SetParenthood(Runtime.FamilyParenthoodWorld, actor, plan);
        public void AppendEvent(WorldEventPayload payload) => Runtime.AppendEvent(payload);
    }

    private abstract class AgentRuntimeAdapter(WorldRuntime runtime)
    {
        protected WorldRuntime Runtime { get; } = runtime;

        public SocietyCheckpoint Society => Runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, AgentState> Agents => Runtime.agents;
        public bool NeedsUrgentWarmth(AgentState person) => Runtime.NeedsUrgentWarmth(person);
        public long WorldTick => Runtime.WorldTick;
        public AgentStore PhysicalAgents => Runtime.agentStore;
        public void MarkCheckpointCurrent() => Runtime.checkpointSchemaVersion = StateSchemaVersion;
        public IReadOnlyList<TownRuntimeState> Towns => Runtime.towns;
        public DeclarativeWorldContentState Content => Runtime.worldContent;
        public CognitionSystem Host => Runtime.cognitionSystem;
        public ActionCheck CheckActionPlan(ActionPlan plan) => Runtime.CheckActionPlan(plan);
        public void CancelProviderCall(global::System.Threading.CancellationTokenSource cancellation, bool underRuntimeGate) => Runtime.CancelProviderCall(cancellation, underRuntimeGate);
        public bool ReadyForBriefInteraction(string actor) => Runtime.ReadyForBriefInteraction(actor);
        public void FinishModelWork(string actorId, string requestId, global::ClankerWorld.Simulation.Cognition.ModelWorkStage stage, long? admittedTick = null) => Runtime.FinishModelWork(actorId, requestId, stage, admittedTick);
        public IReadOnlySet<string>? WithheldActions => Runtime.withheldActions;
        public List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateCandidates(string agentId, AgentState state, bool restrictForOrder = true) => Runtime.CreateCandidates(agentId, state, restrictForOrder);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? OrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => Runtime.OrderCandidateFor(instruction, person);
        public bool ShouldInterruptOrder(AgentState person, OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? orderCandidate, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? urgentCandidate) => Runtime.ShouldInterruptOrder(person, instruction, orderCandidate, urgentCandidate);
        public OrderStore Orders => Runtime.orderStore;
        public SocietyWorldCapture Capture() => Runtime.society.Capture();
        public bool HasSurvivalState => Runtime.survivalState is not null;
        public ActorCapabilityState? CapabilitiesFor(string actor) => Runtime.CapabilitiesFor(actor);
        public IReadOnlyList<PlacedBuilding> Buildings => Runtime.worldSimulation.Buildings;
        public ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) => Runtime.Reach(actor, target, purpose, range, load, swimming);
        public int FreeCarryCapacity(string actor) => Runtime.FreeCarryCapacity(actor);
        public WorldMap Map => Runtime.map;
        public bool CanReach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) => Runtime.CanReach(actor, target, purpose, range, load, swimming);
        public GoodsMatch? RecheckGoods(GoodsRequest request, string lot) => Runtime.RecheckGoods(request, lot);
        public GridPoint HouseholdStockPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => Runtime.HouseholdStockPosition(lot);
        public global::System.TimeProvider ModelTimeProvider => Runtime.modelTimeProvider;
        public GridPoint SharedStoragePosition => Runtime.SharedStoragePosition;
        public bool IsChild(string actor) => global::ClankerWorld.Simulation.World.AgentQueries.IsChild(Runtime.agentWorld, actor);
        public string HouseholdFor(string actor) => Runtime.HouseholdFor(actor);
        public void MarkCurrentSchema() => Runtime.checkpointSchemaVersion = StateSchemaVersion;
        public ReadOnlyCollection<AgentMarriage> Marriages => Runtime.marriages;
        public void MarkModelReplyAdmitted(string requestId) => Runtime.admittedModelReplies.Add(requestId);
        public FamilyTurnState GuardianPlacementActions => Runtime.guardianPlacementActions;
        public void RefuseChoice(ActionPlan plan, ActionRefusal refusal) => Runtime.RefuseChoice(plan, refusal);
        public Dictionary<string, string>? AttemptedActions => Runtime.attemptedActions;
        public bool TalkOrderHasConversation(OwnerQueuedInstruction instruction) => Runtime.TalkOrderHasConversation(instruction);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, OrderStatus status, string? reason, bool waitForDecision = false) => Runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string OrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) => Runtime.OrderBlockedReason(instruction, person);
        public bool ApplyNonLandRemedyAction(TownRuntimeState town, string actor, string candidateId, ActionPlan? plan = null) => Runtime.ApplyNonLandRemedyAction(town, actor, candidateId, plan);
        public bool IsInstructionComplete(OwnerQueuedInstruction instruction) => Runtime.IsInstructionComplete(instruction);
        public bool CanContinueEquipmentRepair(string actor) => Runtime.CanContinueEquipmentRepair(actor);
        public bool CanContinueProject(AgentState state, bool waitingForModel = false) => Runtime.CanContinueProject(state, waitingForModel);
        public void RecordModelAttempt(string id, string status, string? acceptedCandidateId = null, string? setupBlocker = null) => Runtime.RecordModelAttempt(id, status, acceptedCandidateId, setupBlocker);
        public bool IsUndeliveredFinishedOrderDecision(global::ClankerWorld.Simulation.Cognition.AgentObservation observation) => Runtime.IsUndeliveredFinishedOrderDecision(observation);
        public bool ShouldDispatchConversationChoice(string agentId) => Runtime.ShouldDispatchConversationChoice(agentId);
        public string? ConversationChoiceContextFor(string agentId) => Runtime.ConversationChoiceContextFor(agentId);
        public bool TryFindRecipeSite(RecipeDefinition recipe, out string siteId, out GridPoint position, string? actorId = null) => Runtime.TryFindRecipeSite(recipe, out siteId, out position, actorId);
    }
}

internal interface IFamilyCareWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    int TicksPerDay { get; }
    long GuardianStageTicks { get; }
    int ResourceInteractionRange { get; }
    int IllnessCareReliefBasisPoints { get; }
    bool IsAdult(string actor);
    bool ReadyForBriefInteraction(string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    bool RecentlyCaredForIllness(string child);
    string[] GuardianAdultsForStage(string child, string stage);
    string[] NewGuardianAdultsForStage(string child, string stage);
    string HouseholdFor(string actor);
    string? TownForResident(string actor);
    PlacedBuilding? HouseholdBuildingWithTag(string household, string tag);
    HouseResidentCapacityRules.Capacity? HouseResidentCapacity(string household, SocietyAgent proposed);
    bool NeedsUrgentFood(AgentState person);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    PracticalCareActionPlan? OfferPracticalCare(string actor, PracticalCareChoice choice);
    string NewCareRelationshipId(string actor, string target, long tick);
    void ProposeCare(SocietyRelationshipProposal proposal);
    void AcceptCare(string id, int revision, string actor);
    void RefuseCare(string id, int revision, string actor);
    void RevokeCare(string id, string actor);
    SocietyOperationResult AssumePrimaryCare(string adult, string child);
    SocietyOperationResult AssumeInfantCare(string adult, string child);
    SocietyOperationResult AcceptDependentGuardianship(string adult, string child, string? destination);
    void ReconcileGuardianPlacements();
    void BeginGuardianPlacement(string child, string adult, string care);
    void SetHousing(string child, HousingState housing);
    string? HousingBlocker(string child);
    void MarkCurrentSchema();
    void AppendEvent(WorldEventPayload payload);
}

internal interface IFamilyChildWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    WorldMap Map { get; }
    IReadOnlyDictionary<string, ResourceState> Resources { get; }
    long WorldTick { get; }
    GridPoint SharedStoragePosition { get; }
    int ResourceInteractionRange { get; }
    bool IsChild(string actor);
    bool IsAdult(string actor);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    PlacedBuilding? HouseForHousehold(string household);
    int FreeCarryCapacity(string actor);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    ReadOnlyCollection<GridPoint> ReachLeg(string actor, GridPoint origin, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    int AvailableLotQuantity(InventoryLot lot);
    bool CanDeliverHouseDelivery(InventoryLot lot);
    GoodsAnswer FindGoods(GoodsRequest request);
    bool OnBorrowedMarketStall(InventoryLot lot);
    GridPoint HouseholdStockPosition(InventoryLot lot);
    int HouseholdStockInteractionRange(InventoryLot lot);
    int DestinationRoom(string house);
    int FoodHarvestCarryUnits(MapResource resource);
    int FoodHarvestQuantity(MapResource resource);
    InventoryLot? CarriedHouseDelivery(string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    InventoryLot? PersonalSpareFood(string actor, string? sourceLotId = null);
    GoodsMatch? RecheckGoods(GoodsRequest request, string lot);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range);
    string HouseholdFor(string actor);
    string ChildSocialMemoryId(string kind, string actor, string target, long tick);
    void IncreaseTrust(string actor, string target, int amount, string reason);
    void OfferChildChoice(List<CognitionCandidate> candidates, string actor, ChildChoice choice, string description, int priority, string? destinationId = null);
    void RecordChildSocialMemory(string id, string actor, string target, string kind);
    void DeliverChildLoad(ChildActionPlan plan);
    void CollectChildLoad(ChildActionPlan plan);
    void GatherChildLoad(ChildActionPlan plan);
    void ShareChildFood(ChildActionPlan plan);
    void AppendEvent(WorldEventPayload payload);
}

internal interface IFamilyContinuityWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    long ContinuityPostponeTicks { get; }
    bool Partners(string actor, string other);
    bool CanHaveChildren(string actor);
    bool EligibleContinuityParents(string actor, string other);
    IEnumerable<SocietyRelationship> Partnerships(string actor);
    int TrustScore(string actor, string other);
    SocietyRelationship ProposePartnership(string actor, string partner);
    void AcceptPartnership(SocietyRelationship proposal);
    void RevokePartnership(string id, string actor);
    void SetParenthood(string actor, ParenthoodPlan plan);
    string HouseholdFor(string actor);
    void MarkCurrentSchema();
    void AppendEvent(WorldEventPayload payload);
}

internal interface IFamilyGuardianWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    FamilyTurnState Turn { get; }
    IReadOnlyList<PlacedBuilding> Buildings { get; }
    long WorldTick { get; }
    int ResourceInteractionRange { get; }
    string? TownForResident(string actor);
    PlacedBuilding? HouseForHousehold(string household);
    bool IsAdult(string actor);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    OwnerQueuedInstruction? PendingInstructionFor(string actor);
    bool IsConversationBusy(string actor);
    bool CanFitGuardianHousehold(string household, string adult, string child);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    ActorCapabilityState? CapabilitiesFor(string actor);
    GuardianTravelActionPlan? OfferGuardianTravel(string actor, GuardianTravelChoice choice);
    GuardianTravelActionPlan? PlanGuardianTravel(string actor, GuardianTravelChoice choice);
    ActionCheck CheckActionPlan(ActionPlan plan);
    CognitionCandidate? UrgentSurvivalCandidateFor(string actor, AgentState person);
    void ApplyCandidate(string actor, AgentState person, string candidate, bool reportIdle);
    bool TryApplyGuardianPlacementCandidate(string actor, string candidate, ActionPlan? plan);
    int CaregiverFoodCarryRequirement(string actor, AgentState person);
    int FreeCarryCapacity(string actor);
    void MakeRoomForFood(string actor, AgentState person);
    void CareForChild(string actor, string child);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range);
    FarmFieldState? FarmWorkFor(string actor);
    void CancelFarmWork(FarmFieldState field);
    void CancelEquipmentRepair(string actor);
    void SetProject(string actor, AgentProject project);
    bool IsActionWithheld(string actor);
    SocietyOperationResult PlaceDependentWithGuardian(string adult, string child, string care, int revision, string household);
    void RestoreHouseholdDeliveries(string actor, string household, string transition);
    bool MoveDependentToGuardianTown(string child, string adult);
    void SetHousing(string child, HousingState housing);
    string? HousingBlocker(string child);
    void MarkCurrentSchema();
    void AppendEvent(WorldEventPayload payload);
}

internal interface IFamilyMedicalWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    int ResourceInteractionRange { get; }
    bool IsAdult(string actor);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    IEnumerable<GoodsMatch> MedicalDoseStock(string actor);
    bool HasCarriedOwnItem(string actor, string kind);
    InventoryLot? SharedPreparationItem(string kind, string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    OwnerQueuedInstruction? PendingInstructionFor(string actor);
    GoodsMatch? RecheckGoods(GoodsRequest request, string lot);
    void MarkCurrentSchema();
    void AppendEvent(WorldEventPayload payload);
}

internal interface IFamilyTreatmentWorld : IFamilyMedicalWorld
{
    long NextEventId { get; }
    string MedicalDoseId(long tick, long eventId, string caregiver, string patient);
    ActionCheck CheckActionPlan(ActionPlan plan);
    void ConsumeMedicalDose(string id, InventoryLot supply, string caregiver, string patient, string kind);
    void CloseMedicalDoseReceipt(Identity reservation, string purpose);
}

internal interface IFamilySupplyWorld : IFamilyMedicalWorld
{
    int FreeCarryCapacity(string actor);
    InventoryLot? SharedItem(string kind, string actor, GridPoint destination, int range);
    GridPoint HouseholdStockPosition(InventoryLot lot);
    int HouseholdStockInteractionRange(InventoryLot lot);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    ActionCheck CheckEquipmentPickup(string actor, PlannedEquipmentPickup pickup, string description, ReachPurpose purpose);
    bool CanPrepareSharedPickup(string actor, InventoryLot lot, GridPoint destination, int range);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range);
    void ApplyPlannedEquipmentPickup(string actor, PlannedEquipmentPickup pickup);
}

internal interface IFamilyParenthoodWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    WorldMap Map { get; }
    long WorldTick { get; }
    int TicksPerDay { get; }
    int ResourceInteractionRange { get; }
    int IllnessCareReliefBasisPoints { get; }
    bool IsAdult(string actor);
    bool CloseKin(string actor, string other);
    IEnumerable<SocietyRelationship> Partnerships(string actor);
    bool ReadyForLesson(string actor);
    bool RecentlyCaredForIllness(string child);
    bool NeedsCaregiver(string child);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    IEnumerable<PlacedBuilding> AccessibleShelters(string actor, bool includeStormRefuge);
    ParenthoodFood ParenthoodFoodReadiness(string actor);
    IReadOnlyList<TownRuntimeState> Towns { get; }
    GoodsMatch? RecheckGoods(GoodsRequest request, string lot);
    bool ReadyForBriefInteraction(string actor);
    bool NeedsUrgentFood(AgentState person);
    IEnumerable<InventoryLot> PreferredFood(string owner, string actor);
    bool CanReach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    int MinimumFoodPickupCarryUnits(string actor, GridPoint origin, bool forDependent);
    string HouseholdFor(string actor);
    bool ContinuityPlanDue(string actor, string partner);
    ContinuityCouple? ContinuityCoupleOf(string actor);
    ContinuityCouple? ContinuityCoupleFor(string actor, string partner);
    bool ContinuityAllowsPostponement(ContinuityCouple couple);
    ActorCapabilityState? CapabilitiesFor(string actor);
    string ParenthoodRequestId(string actor, long tick);
    void CommitBirth(SocietyBirthRequest request);
    string? HousingBlocker(string child);
    void SetHousing(string child, HousingState housing);
    string? TownForResident(string actor);
    void AddTownResident(string town, string child, string eventKind);
    void ConsumeCareFood(string actor, string lot);
    int FoodNourishment(string kind);
    SurvivalCondition? AfterMeal(AgentState person, InventoryLot food);
    void MarkCurrentSchema();
    void AppendEvent(WorldEventPayload payload);
}

internal interface IFamilyPartnershipWorld
{
    AgentStore PhysicalAgents { get; }
    IFamilyContinuityWorld ContinuityWorld { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    int TicksPerDay { get; }
    bool HasSurvivalState { get; }
    bool IsAdult(string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    bool ReadyForBriefInteraction(string actor);
    int TrustScore(string owner, string subject);
    string NewPartnershipId(string actor, string partner, long tick);
    void ProposePartnership(SocietyRelationshipProposal relationship);
    void AcceptPartnership(string id, int revision, string actor);
    void RefusePartnership(string id, int revision, string actor);
    void RevokePartnership(string id, string actor);
    void MaintainMarriages();
    void AppendEvent(WorldEventPayload payload);
}

internal interface IFamilyPracticalCareWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    IFamilyCareWorld CareWorld { get; }
    IFamilyParenthoodWorld ParenthoodWorld { get; }
    long WorldTick { get; }
    int ResourceInteractionRange { get; }
    FoodActionPlan? PlanFoodConsumption(string actor);
    InventoryLot? AvailableHouseholdServing(string actor, GridPoint position, GridPoint returnTo, int returnRange);
    SharedFoodActionPlan? PlanSharedFood(string actor, InventoryLot selected);
    MapResource? AvailableFoodSource(string actor, GridPoint position);
    FoodResourceActionPlan? PlanFoodResource(string actor, bool harvest, MapResource selected);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    ActorCapabilityState? CapabilitiesFor(string actor);
    bool NeedsUrgentFood(AgentState person);
    IEnumerable<InventoryLot> PreferredFood(string owner, string actor);
    ActionCheck CheckFoodPlan(FoodActionPlan plan);
    void CompleteChildFoodPickup(string actor, SharedFoodActionPlan plan, GridPoint destination);
    void CompleteFoodHarvest(string actor, MapResource resource, string kind, int quantity, int seedQuantity);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range);
}

internal interface IFamilyWillWorld
{
    AgentStore PhysicalAgents { get; }
    TimeProvider ModelTimeProvider { get; }
    Task<T> ScheduleWillWork<T>(CognitionDecisionRequest request, Func<Task<T>> work);
    void RecordWillReplyAdmission(string requestId);
    void CancelWillWork(CognitionDecisionRequest request);
    SocietyCheckpoint Society { get; }
    IReadOnlyList<TownRuntimeState> Towns { get; }
    IReadOnlyList<PlacedBuilding> Buildings { get; }
    DeclarativeWorldContentState Content { get; }
    long WorldTick { get; }
    IDecisionProvider? CreateProvider(string actor);
    string? CognitionHouseholdId(string? household);
    int DestinationRoom(string warehouse);
    void MarkWillStarted(string estate);
    SocietyOperationResult ResolveWill(string estate, SocietyWillDirective? directive, string reason, string? finalWords, IReadOnlySet<string>? towns);
    void CancelProviderCall(CancellationTokenSource cancellation, bool underRuntimeGate);
    void AppendEvent(WorldEventPayload payload);
}
