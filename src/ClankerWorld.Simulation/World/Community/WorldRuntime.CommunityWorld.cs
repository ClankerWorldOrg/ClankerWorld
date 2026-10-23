using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICommunityLearningWorld CommunityLearningWorld => new CommunityWorldAdapter(this);
    private ICommunitySkillWorld CommunitySkillWorld => new CommunityWorldAdapter(this);
    private ICommunityHousingWorld CommunityHousingWorld => new CommunityWorldAdapter(this);
    private ICommunityWorkWorld CommunityWorkWorld => new CommunityWorldAdapter(this);
    private ICommunityGoodsWorld CommunityGoodsWorld => new CommunityWorldAdapter(this);
    private ICommunityRelocationWorld CommunityRelocationWorld => new CommunityWorldAdapter(this);
    private ICommunityDepartureWorld CommunityDepartureWorld => new CommunityWorldAdapter(this);

    private sealed class CommunityWorldAdapter(WorldRuntime runtime) : ICommunityLearningWorld, ICommunitySkillWorld, ICommunityHousingWorld, ICommunityDepartureWorld, ICommunityRelocationWorld, ICommunityGoodsWorld, ICommunityWorkWorld
    {
        public ICommunityHousingWorld HousingWorld => this;
        public long RelocationNoticeTicks => runtime.RelocationNoticeTicks;
        public HouseResidentCapacityRules.Capacity? HouseResidentCapacity(string household) => runtime.HouseResidentCapacity(household);
        public void EndHousingRequest(string actor, HousingRequest request, string eventKind, bool remember) => runtime.EndHousingRequest(actor, request, eventKind, remember);
        public GoodsKinds EdibleFoodGoods => WorldRuntime.EdibleFoodGoods;
        public bool MayFoundHousehold(string actor) => runtime.MayFoundHousehold(actor);
        public GoodsMatch? RecheckGoods(GoodsRequest request, string lot) => runtime.RecheckGoods(request, lot);
        public string SoloHouseholdId(string actor, long tick) => WorldRuntime.SoloHouseholdId(actor, tick);
        public void CreateHousehold(string household, string name, IReadOnlyList<string> members) => runtime.society.Apply(checkpoint => SocietyRules.CreateHousehold(checkpoint, household, name, members));
        public bool AcceptReplacementCare(string actor, string child) => runtime.society.Apply(checkpoint => SocietyRules.AcceptReplacementCare(checkpoint, actor, child)).CreatedId is not null;
        public GoodsRequest? PersonalRecoveryRequest(string actor, GoodsUse use, string? kind = null) => runtime.PersonalRecoveryRequest(actor, use, kind);
        public GridPoint HouseholdStockPosition(InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public IEnumerable<InventoryLot> PersonalStorageLots(string actor, string house) => runtime.PersonalStorageLots(actor, house);
        public IEnumerable<InventoryLot> ReturnableBorrowedGoods(string actor) => runtime.ReturnableBorrowedGoods(actor);
        public IEnumerable<InventoryLot> BorrowedGoods(string actor) => runtime.BorrowedGoods(actor);
        public int DestinationRoom(string house) => runtime.DestinationRoom(house);
        public bool VesselFits(InventoryLot lot, int room) => runtime.VesselFits(lot, room);
        public int PhysicalUnreservedQuantity(InventoryLot lot) => runtime.PhysicalUnreservedQuantity(lot);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public void InterruptOrdinaryFieldWorkForCustody(string actor, string lot) => runtime.InterruptOrdinaryFieldWorkForCustody(actor, lot);
        public string PersonalMoveId(string actor, long tick, string lot) => WorldRuntime.PersonalMoveId(actor, tick, lot);
        public void MovePersonalGoods(PersonalGoodsActionPlan plan, string moveId) => runtime.RunInventoryCommand(InventorySystem.MovePersonalGoods(plan, moveId));
        public IReadOnlyList<PlacedBuilding> Buildings => runtime.worldSimulation.Buildings;
        public IReadOnlyList<WorldProductionJob> ProductionJobs => runtime.worldSimulation.ProductionJobs;
        public IReadOnlyList<BuildingExpansionJob>? Expansions => runtime.worldSimulation.BuildingExpansions;
        public bool IsActiveProductionOrderJob(string job) => runtime.IsActiveProductionOrderJob(job);
        public bool CanFitExpansion(PlacedBuilding building, GridPoint position, BuildingFootprintRevision footprint, string job) => runtime.CanFitExpansion(building, position, footprint, out _, job);
        public void SetReservationDeadline(IReadOnlyList<Identity> ids, long deadline) => runtime.RunInventoryCommand(InventorySystem.SetReservationDeadline(ids, deadline));
        public void ResumeProduction(string job, long deadline, string? knife, string actor) => runtime.productionSystem.ResumeJob(job, deadline, knife, actor);
        public void ResumeExpansion(string job, long deadline, string actor) => runtime.constructionSystem.Buildings.ResumeExpansion(job, deadline, actor);
        public void ReleaseUnavailablePausedInputs(IReadOnlyList<Identity> ids) => runtime.RunInventoryCommand(InventorySystem.ReleaseUnavailablePausedInputs(ids));
        public void CancelProduction(IReadOnlySet<string> ids) => runtime.productionSystem.CancelJobs(ids);
        public void CancelPausedExpansions(IReadOnlySet<string> ids, string reason) => runtime.constructionSystem.Buildings.CancelPausedExpansions(ids, reason);
        public AgentStore PhysicalAgents => runtime.agentStore;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public long WorldTick => runtime.WorldTick;
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public GridPoint SharedStoragePosition => runtime.SharedStoragePosition;
        public bool HasSurvivalState => runtime.survivalState is not null;
        public long HousingRefusalCooldownTicks => runtime.HousingRefusalCooldownTicks;
        public string[] HouseholdAdults(string household) => runtime.HouseholdAdults(household);
        public PlacedBuilding? HouseForHousehold(string household) => runtime.HouseForHousehold(household);
        public IEnumerable<SocietyHousehold> AskableHouseholds(string actor) => runtime.AskableHouseholds(actor);
        public IEnumerable<string> PendingHousingRequestsFor(string member) => runtime.PendingHousingRequestsFor(member);
        public string? HousingBlocker(string actor) => runtime.HousingBlocker(actor);
        public void ReconcileRelocation(string household) => runtime.ReconcileRelocation(household);
        public bool MayRelocateFromHousehold(string actor) => runtime.MayRelocateFromHousehold(actor);
        public bool CanFitCareGroup(string household, string actor) => runtime.CanFitCareGroup(household, actor);
        public bool HoldsRelocationNotice(string actor) => runtime.HoldsRelocationNotice(actor);
        public bool HasDependentInPrimaryCare(string actor) => runtime.HasDependentInPrimaryCare(actor);
        public IReadOnlyList<DepartureAllowance> PlanDepartureAllowance(string actor, string household) => runtime.PlanDepartureAllowance(actor, household);
        public string DepartureTransferId(string actor, long tick, int count, string lot) => WorldRuntime.DepartureTransferId(actor, tick, count, lot);
        public bool LeaveHousehold(string actor) => runtime.society.Apply(checkpoint => SocietyRules.LeaveHousehold(checkpoint, actor)).CreatedId is not null;
        public void CommitDepartureAllowance(InventoryCheckpoint inventory) => runtime.RunInventoryCommand(InventorySystem.CommitDepartureAllowance(inventory));
        public void RestoreDepartureDeliveries(string actor, string household) => runtime.RestoreHouseholdDeliveries(actor, household, "depart");
        public void ReconcileGuardianPlacements() => runtime.ReconcileGuardianPlacements();
        public bool DepartHousehold(string actor, string cause) => runtime.DepartHousehold(actor, cause);
        public IReadOnlyList<string> MovingCareGroup(string actor) => runtime.MovingCareGroup(actor);
        public void JoinHouseholdCareGroup(string actor, string household) => runtime.society.Apply(checkpoint => SocietyRules.JoinHouseholdCareGroup(checkpoint, actor, household));
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool HasSkill(string actor, SkillKind skill) => runtime.HasSkill(actor, skill);
        public ActorCapabilityState? CapabilitiesFor(string actor) => runtime.CapabilitiesFor(actor);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);
        public OwnerQueuedInstruction? PendingInstructionFor(string actor) => runtime.PendingInstructionFor(actor);
        public bool IsConversationBusy(string actor) => runtime.IsConversationBusy(actor);
        public bool HasCompetingLessonDecision(string actor) => runtime.HasFoodPolicyDecision(actor) || runtime.HasHousingDecision(actor) ||
            MarketSystem.HasTradeResponse(runtime.MarketBarterWorld, actor) || runtime.HasFamilyDecision(actor) || runtime.HasParenthoodDecision(actor) || runtime.HasDependentCareDecision(actor);
        public bool HasHousingDecision(string actor) => runtime.HasHousingDecision(actor);
        public bool ModelDecisionPending(string actor) => runtime.society.PendingModelAgentIds().Contains(actor);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range) => runtime.IsWithinInteractionRange(origin, destination, range);
        public bool CanReach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) => runtime.CanReach(actor, target, purpose, range, load, swimming);
        public ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) => runtime.Reach(actor, target, purpose, range, load, swimming);
        public ActionCheck CheckActionPlan(ActionPlan plan) => runtime.CheckActionPlan(plan);
        public void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range) => runtime.MoveToward(actor, person, destination, reason, range);
        public void RecordAttemptedAction(string actor, string candidate) => runtime.RecordAttemptedAction(actor, candidate);
        public void PauseProjectForLesson(string actor, AgentProject project) => runtime.SetProject(actor, project with { Stage = "paused", Blocker = "Taking part in an agreed lesson" });
        public void GainSkill(string actor, SkillKind skill, string teacher) => runtime.GainSkill(actor, skill, teacher);
        public void IncreaseTrust(string actor, string other, int amount, string reason) => runtime.IncreaseTrust(actor, other, amount, reason);
        public void RecordLessonGratitude(string student, string teacher, SkillKind skill)
        {
            var memoryId = LessonGratitudeMemoryId(student, teacher, skill);
            if (!runtime.society.Checkpoint.Memories.Any(memory => memory.Id == memoryId))
                runtime.society.Apply(checkpoint => SocietyRules.RecordSocialMemory(checkpoint, new(Identity.Parse(memoryId), student, teacher,
                    $"Learned {skill.ToString().ToLowerInvariant()} with {checkpoint.GetAgent(teacher).Name}.", "public", runtime.WorldTick)
                { Kind = SocietyMemoryKind.Skill }));
        }
        public void CancelDepartureOrders(string actor)
        {
            if (runtime.PendingInstructionFor(actor) is { Task.Action: "produce_item" } productionOrder)
            {
                runtime.CancelProductionForOrder(productionOrder);
                runtime.SetOrderStatus(productionOrder, OrderStatus.Blocked, "The agent left the household holding the ordered workstation.");
            }
            if (runtime.PendingInstructionFor(actor) is { Task.Action: "construct_building" } constructionOrder)
            {
                OrderSystem.CancelConstructionForOrder(runtime.ConstructionOrdersWorld, constructionOrder);
                runtime.SetOrderStatus(constructionOrder, OrderStatus.Blocked, "The agent left the household arranging the requested construction.");
            }
            if (runtime.PendingInstructionFor(actor) is { Task: { Action: "expand_building", TargetBuildingKind: "house" } } expansionOrder)
            {
                OrderSystem.CancelExpansionForOrder(runtime.ExpansionOrdersWorld, expansionOrder);
                runtime.SetOrderStatus(expansionOrder, OrderStatus.Blocked, "The agent left the household arranging the requested expansion.");
            }
        }
        public IReadOnlyList<WorldProductionJob> PauseDepartingHouseholdWork(string actor, string household)
        {
            var ownedBuildings = runtime.worldSimulation.Buildings.Where(building => building.HouseholdId == household)
                .Select(building => building.InstanceId).ToHashSet(StringComparer.Ordinal);
            // Work for the leaver's own goods, such as their handcart, is not household work
            // anyone else may finish: it stops, and its reserved materials stay with the leaver.
            var personalJobs = runtime.worldSimulation.ProductionJobs.Where(job => job.WorkerId == actor && job.OwnerId == actor &&
                ownedBuildings.Contains(job.BuildingInstanceId) && job.State == WorldProductionJobState.Running).ToArray();
            foreach (var job in personalJobs)
                runtime.RunInventoryCommand(InventorySystem.ReleaseDepartingFarmInputs(job));
            var personalJobIds = personalJobs.Select(job => job.JobId).ToHashSet(StringComparer.Ordinal);
            runtime.productionSystem.LeaveHousehold(actor, ownedBuildings, personalJobIds, runtime.WorldTick);
            runtime.constructionSystem.Buildings.PauseHouseholdExpansions(actor, household, runtime.WorldTick);
            var heldReservations = runtime.worldSimulation.ProductionJobs.Where(job => job.WorkerId == actor &&
                    ownedBuildings.Contains(job.BuildingInstanceId) && job.State == WorldProductionJobState.Paused)
                .SelectMany(job => job.InputReservationIds)
                .Concat((runtime.worldSimulation.BuildingExpansions ?? []).Where(job => job.WorkerId == actor &&
                    job.OwnerId == household && job.State == WorldProductionJobState.Paused).SelectMany(job => job.InputReservationIds))
                .Distinct().Order().ToArray();
            if (heldReservations.Length != 0)
                runtime.RunInventoryCommand(InventorySystem.HoldDepartingProjectInputs(heldReservations));
            return personalJobs;
        }
        public void MarkCurrentSchema() => runtime.checkpointSchemaVersion = StateSchemaVersion;
        public void AppendEvent(WorldEventPayload payload) => runtime.AppendEvent(payload);
    }
}
