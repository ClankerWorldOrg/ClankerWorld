using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Content;
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
}
