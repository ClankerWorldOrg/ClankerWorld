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

    private sealed class CommunityWorldAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICommunityLearningWorld, ICommunitySkillWorld, ICommunityHousingWorld, ICommunityDepartureWorld, ICommunityRelocationWorld, ICommunityGoodsWorld, ICommunityWorkWorld
    {
        public ICommunityHousingWorld HousingWorld => this;
        public long RelocationNoticeTicks => Runtime.RelocationNoticeTicks;
        public HouseResidentCapacityRules.Capacity? HouseResidentCapacity(string household) => Runtime.HouseResidentCapacity(household);
        public void EndHousingRequest(string actor, HousingRequest request, string eventKind, bool remember) => CommunitySystem.EndHousingRequest(Runtime.CommunityHousingWorld, actor, request, eventKind, remember);
        public GoodsKinds EdibleFoodGoods => WorldRuntime.EdibleFoodGoods;
        public bool MayFoundHousehold(string actor) => Runtime.MayFoundHousehold(actor);
        public string SoloHouseholdId(string actor, long tick) => WorldRuntime.SoloHouseholdId(actor, tick);
        public void CreateHousehold(string household, string name, IReadOnlyList<string> members) => Runtime.society.Apply(checkpoint => SocietyRules.CreateHousehold(checkpoint, household, name, members));
        public bool AcceptReplacementCare(string actor, string child) => Runtime.society.Apply(checkpoint => SocietyRules.AcceptReplacementCare(checkpoint, actor, child)).CreatedId is not null;
        public GoodsRequest? PersonalRecoveryRequest(string actor, GoodsUse use, string? kind = null) => Runtime.PersonalRecoveryRequest(actor, use, kind);
        public IEnumerable<InventoryLot> PersonalStorageLots(string actor, string house) => Runtime.PersonalStorageLots(actor, house);
        public IEnumerable<InventoryLot> ReturnableBorrowedGoods(string actor) => Runtime.ReturnableBorrowedGoods(actor);
        public IEnumerable<InventoryLot> BorrowedGoods(string actor) => Runtime.BorrowedGoods(actor);
        public int DestinationRoom(string house) => Runtime.DestinationRoom(house);
        public bool VesselFits(InventoryLot lot, int room) => Runtime.VesselFits(lot, room);
        public int PhysicalUnreservedQuantity(InventoryLot lot) => Runtime.PhysicalUnreservedQuantity(lot);
        public void InterruptOrdinaryFieldWorkForCustody(string actor, string lot) => Runtime.InterruptOrdinaryFieldWorkForCustody(actor, lot);
        public string PersonalMoveId(string actor, long tick, string lot) => WorldRuntime.PersonalMoveId(actor, tick, lot);
        public void MovePersonalGoods(PersonalGoodsActionPlan plan, string moveId) => Runtime.RunInventoryCommand(InventorySystem.MovePersonalGoods(plan, moveId));
        public IReadOnlyList<WorldProductionJob> ProductionJobs => Runtime.worldSimulation.ProductionJobs;
        public IReadOnlyList<BuildingExpansionJob>? Expansions => Runtime.worldSimulation.BuildingExpansions;
        public bool IsActiveProductionOrderJob(string job) => OrderSystem.IsActiveProductionOrderJob(Runtime.ProductionOrdersWorld, job);
        public bool CanFitExpansion(PlacedBuilding building, GridPoint position, BuildingFootprintRevision footprint, string job) => Runtime.CanFitExpansion(building, position, footprint, out _, job);
        public void SetReservationDeadline(IReadOnlyList<Identity> ids, long deadline) => Runtime.RunInventoryCommand(InventorySystem.SetReservationDeadline(ids, deadline));
        public void ResumeProduction(string job, long deadline, string? knife, string actor) => Runtime.productionSystem.ResumeJob(job, deadline, knife, actor);
        public void ResumeExpansion(string job, long deadline, string actor) => Runtime.constructionSystem.Buildings.ResumeExpansion(job, deadline, actor);
        public void ReleaseUnavailablePausedInputs(IReadOnlyList<Identity> ids) => Runtime.RunInventoryCommand(InventorySystem.ReleaseUnavailablePausedInputs(ids));
        public void CancelProduction(IReadOnlySet<string> ids) => Runtime.productionSystem.CancelJobs(ids);
        public void CancelPausedExpansions(IReadOnlySet<string> ids, string reason) => Runtime.constructionSystem.Buildings.CancelPausedExpansions(ids, reason);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public long HousingRefusalCooldownTicks => Runtime.HousingRefusalCooldownTicks;
        public string[] HouseholdAdults(string household) => Runtime.HouseholdAdults(household);
        public PlacedBuilding? HouseForHousehold(string household) => Runtime.HouseForHousehold(household);
        public IEnumerable<SocietyHousehold> AskableHouseholds(string actor) => Runtime.AskableHouseholds(actor);
        public IEnumerable<string> PendingHousingRequestsFor(string member) => Runtime.PendingHousingRequestsFor(member);
        public string? HousingBlocker(string actor) => Runtime.HousingBlocker(actor);
        public void ReconcileRelocation(string household) => CommunitySystem.ReconcileRelocation(Runtime.CommunityRelocationWorld, household);
        public bool MayRelocateFromHousehold(string actor) => CommunitySystem.MayRelocateFromHousehold(Runtime.CommunityRelocationWorld, actor);
        public bool CanFitCareGroup(string household, string actor) => Runtime.CanFitCareGroup(household, actor);
        public bool HoldsRelocationNotice(string actor) => CommunitySystem.HoldsRelocationNotice(Runtime.CommunityRelocationWorld, actor);
        public bool HasDependentInPrimaryCare(string actor) => CommunitySystem.HasDependentInPrimaryCare(Runtime.CommunityRelocationWorld, actor);
        public IReadOnlyList<DepartureAllowance> PlanDepartureAllowance(string actor, string household) => Runtime.PlanDepartureAllowance(actor, household);
        public string DepartureTransferId(string actor, long tick, int count, string lot) => WorldRuntime.DepartureTransferId(actor, tick, count, lot);
        public bool LeaveHousehold(string actor) => Runtime.society.Apply(checkpoint => SocietyRules.LeaveHousehold(checkpoint, actor)).CreatedId is not null;
        public void CommitDepartureAllowance(InventoryCheckpoint inventory) => Runtime.RunInventoryCommand(InventorySystem.CommitDepartureAllowance(inventory));
        public void RestoreDepartureDeliveries(string actor, string household) => Runtime.RestoreHouseholdDeliveries(actor, household, "depart");
        public void ReconcileGuardianPlacements() => FamilySystem.ReconcileGuardianPlacements(Runtime.FamilyGuardianWorld);
        public bool DepartHousehold(string actor, string cause) => CommunitySystem.DepartHousehold(Runtime.CommunityDepartureWorld, actor, cause, null);
        public IReadOnlyList<string> MovingCareGroup(string actor) => global::ClankerWorld.Simulation.Society.SocietyRules.MovingCareGroup(Runtime.society.Checkpoint, actor);
        public void JoinHouseholdCareGroup(string actor, string household) => Runtime.society.Apply(checkpoint => SocietyRules.JoinHouseholdCareGroup(checkpoint, actor, household));
        public bool IsAdult(string actor) => Runtime.IsAdult(actor);
        public bool HasSkill(string actor, SkillKind skill) => CommunitySystem.HasSkill(Runtime.agentStore, actor, skill);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public OwnerQueuedInstruction? PendingInstructionFor(string actor) => Runtime.PendingInstructionFor(actor);
        public bool IsConversationBusy(string actor) => Runtime.IsConversationBusy(actor);
        public bool HasCompetingLessonDecision(string actor) => Runtime.townSystem.HasFoodPolicyDecision(Runtime.civicWorld, actor) || Runtime.HasHousingDecision(actor) ||
            MarketSystem.HasTradeResponse(Runtime.MarketBarterWorld, actor) || FamilySystem.HasFamilyDecision(Runtime.FamilyPartnershipWorld, actor) || FamilySystem.HasParenthoodDecision(Runtime.FamilyParenthoodWorld, actor) || FamilySystem.HasDependentCareDecision(Runtime.FamilyCareWorld, actor);
        public bool HasHousingDecision(string actor) => Runtime.HasHousingDecision(actor);
        public bool ModelDecisionPending(string actor) => Runtime.society.PendingModelAgentIds().Contains(actor);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range) => Runtime.IsWithinInteractionRange(origin, destination, range);
        public void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range) => Runtime.MoveToward(actor, person, destination, reason, range);
        public void RecordAttemptedAction(string actor, string candidate) => Runtime.RecordAttemptedAction(actor, candidate);
        public void PauseProjectForLesson(string actor, AgentProject project) => Runtime.SetProject(actor, project with { Stage = "paused", Blocker = "Taking part in an agreed lesson" });
        public void GainSkill(string actor, SkillKind skill, string teacher) => CommunitySystem.GainSkill(Runtime.CommunitySkillWorld, actor, skill, teacher);
        public void IncreaseTrust(string actor, string other, int amount, string reason) => CommunitySystem.IncreaseTrust(Runtime.CommunitySkillWorld, actor, other, amount, reason);
        public void RecordLessonGratitude(string student, string teacher, SkillKind skill)
        {
            var memoryId = LessonGratitudeMemoryId(student, teacher, skill);
            if (!Runtime.society.Checkpoint.Memories.Any(memory => memory.Id == memoryId))
                Runtime.society.Apply(checkpoint => SocietyRules.RecordSocialMemory(checkpoint, new(Identity.Parse(memoryId), student, teacher,
                    $"Learned {skill.ToString().ToLowerInvariant()} with {checkpoint.GetAgent(teacher).Name}.", "public", Runtime.WorldTick)
                { Kind = SocietyMemoryKind.Skill }));
        }
        public void CancelDepartureOrders(string actor)
        {
            if (Runtime.PendingInstructionFor(actor) is { Task.Action: "produce_item" } productionOrder)
            {
                Runtime.CancelProductionForOrder(productionOrder);
                Runtime.SetOrderStatus(productionOrder, OrderStatus.Blocked, "The agent left the household holding the ordered workstation.");
            }
            if (Runtime.PendingInstructionFor(actor) is { Task.Action: "construct_building" } constructionOrder)
            {
                OrderSystem.CancelConstructionForOrder(Runtime.ConstructionOrdersWorld, constructionOrder);
                Runtime.SetOrderStatus(constructionOrder, OrderStatus.Blocked, "The agent left the household arranging the requested construction.");
            }
            if (Runtime.PendingInstructionFor(actor) is { Task: { Action: "expand_building", TargetBuildingKind: "house" } } expansionOrder)
            {
                OrderSystem.CancelExpansionForOrder(Runtime.ExpansionOrdersWorld, expansionOrder);
                Runtime.SetOrderStatus(expansionOrder, OrderStatus.Blocked, "The agent left the household arranging the requested expansion.");
            }
        }
        public IReadOnlyList<WorldProductionJob> PauseDepartingHouseholdWork(string actor, string household)
        {
            var ownedBuildings = Runtime.worldSimulation.Buildings.Where(building => building.HouseholdId == household)
                .Select(building => building.InstanceId).ToHashSet(StringComparer.Ordinal);
            // Work for the leaver's own goods, such as their handcart, is not household work
            // anyone else may finish: it stops, and its reserved materials stay with the leaver.
            var personalJobs = Runtime.worldSimulation.ProductionJobs.Where(job => job.WorkerId == actor && job.OwnerId == actor &&
                ownedBuildings.Contains(job.BuildingInstanceId) && job.State == WorldProductionJobState.Running).ToArray();
            foreach (var job in personalJobs)
                Runtime.RunInventoryCommand(InventorySystem.ReleaseDepartingFarmInputs(job));
            var personalJobIds = personalJobs.Select(job => job.JobId).ToHashSet(StringComparer.Ordinal);
            Runtime.productionSystem.LeaveHousehold(actor, ownedBuildings, personalJobIds, Runtime.WorldTick);
            Runtime.constructionSystem.Buildings.PauseHouseholdExpansions(actor, household, Runtime.WorldTick);
            var heldReservations = Runtime.worldSimulation.ProductionJobs.Where(job => job.WorkerId == actor &&
                    ownedBuildings.Contains(job.BuildingInstanceId) && job.State == WorldProductionJobState.Paused)
                .SelectMany(job => job.InputReservationIds)
                .Concat((Runtime.worldSimulation.BuildingExpansions ?? []).Where(job => job.WorkerId == actor &&
                    job.OwnerId == household && job.State == WorldProductionJobState.Paused).SelectMany(job => job.InputReservationIds))
                .Distinct().Order().ToArray();
            if (heldReservations.Length != 0)
                Runtime.RunInventoryCommand(InventorySystem.HoldDepartingProjectInputs(heldReservations));
            return personalJobs;
        }
        public void AppendEvent(WorldEventPayload payload) => Runtime.AppendEvent(payload);
    }
}

internal interface ICommunityDepartureWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    GridPoint SharedStoragePosition { get; }
    GoodsKinds EdibleFoodGoods { get; }
    ActorCapabilityState? CapabilitiesFor(string actor);
    string HouseholdFor(string actor);
    bool MayFoundHousehold(string actor);
    IEnumerable<SocietyHousehold> AskableHouseholds(string actor);
    GoodsMatch? RecheckGoods(GoodsRequest request, string lot);
    string SoloHouseholdId(string actor, long tick);
    string? HousingBlocker(string actor);
    void CreateHousehold(string household, string name, IReadOnlyList<string> members);
    bool AcceptReplacementCare(string actor, string child);
    bool IsAdult(string actor);
    bool HasDependentInPrimaryCare(string actor);
    bool HoldsRelocationNotice(string actor);
    bool MayRelocateFromHousehold(string actor);
    IReadOnlyList<string> MovingCareGroup(string actor);
    IReadOnlyList<DepartureAllowance> PlanDepartureAllowance(string actor, string household);
    string DepartureTransferId(string actor, long tick, int count, string lot);
    bool LeaveHousehold(string actor);
    void CommitDepartureAllowance(InventoryCheckpoint inventory);
    void CancelDepartureOrders(string actor);
    IReadOnlyList<WorldProductionJob> PauseDepartingHouseholdWork(string actor, string household);
    void RestoreDepartureDeliveries(string actor, string household);
    void ReconcileGuardianPlacements();
    void MarkCurrentSchema();
    void AppendEvent(WorldEventPayload payload);
}

internal interface ICommunityGoodsWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    int ResourceInteractionRange { get; }
    string HouseholdFor(string actor);
    GoodsRequest? PersonalRecoveryRequest(string actor, GoodsUse use, string? kind = null);
    GoodsMatch? RecheckGoods(GoodsRequest request, string lot);
    GridPoint HouseholdStockPosition(InventoryLot lot);
    PlacedBuilding? HouseForHousehold(string household);
    IEnumerable<InventoryLot> PersonalStorageLots(string actor, string house);
    IEnumerable<InventoryLot> ReturnableBorrowedGoods(string actor);
    IEnumerable<InventoryLot> BorrowedGoods(string actor);
    int DestinationRoom(string house);
    bool VesselFits(InventoryLot lot, int room);
    int PhysicalUnreservedQuantity(InventoryLot lot);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    ActorCapabilityState? CapabilitiesFor(string actor);
    bool ReadyForBriefInteraction(string actor);
    int FreeCarryCapacity(string actor);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    void InterruptOrdinaryFieldWorkForCustody(string actor, string lot);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range);
    string PersonalMoveId(string actor, long tick, string lot);
    void MovePersonalGoods(PersonalGoodsActionPlan plan, string moveId);
    void AppendEvent(WorldEventPayload payload);
}

internal interface ICommunityHousingWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    long HousingRefusalCooldownTicks { get; }
    bool IsAdult(string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    string[] HouseholdAdults(string household);
    PlacedBuilding? HouseForHousehold(string household);
    IEnumerable<SocietyHousehold> AskableHouseholds(string actor);
    IEnumerable<string> PendingHousingRequestsFor(string member);
    string? HousingBlocker(string actor);
    void ReconcileRelocation(string household);
    bool MayRelocateFromHousehold(string actor);
    bool CanFitCareGroup(string household, string actor);
    bool HoldsRelocationNotice(string actor);
    bool DepartHousehold(string actor, string cause);
    IReadOnlyList<string> MovingCareGroup(string actor);
    void JoinHouseholdCareGroup(string actor, string household);
    void MarkCurrentSchema();
    void AppendEvent(WorldEventPayload payload);
}

internal interface ICommunityLearningWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    int ResourceInteractionRange { get; }
    GridPoint SharedStoragePosition { get; }
    bool HasSurvivalState { get; }
    bool IsAdult(string actor);
    bool HasSkill(string actor, SkillKind skill);
    ActorCapabilityState? CapabilitiesFor(string actor);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    OwnerQueuedInstruction? PendingInstructionFor(string actor);
    bool IsConversationBusy(string actor);
    bool HasCompetingLessonDecision(string actor);
    bool HasHousingDecision(string actor);
    bool ModelDecisionPending(string actor);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    bool CanReach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    ActionCheck CheckActionPlan(ActionPlan plan);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range);
    void RecordAttemptedAction(string actor, string candidate);
    void PauseProjectForLesson(string actor, AgentProject project);
    void GainSkill(string actor, SkillKind skill, string teacher);
    void IncreaseTrust(string actor, string other, int amount, string reason);
    void RecordLessonGratitude(string student, string teacher, SkillKind skill);
    void MarkCurrentSchema();
    void AppendEvent(WorldEventPayload payload);
}

internal interface ICommunityRelocationWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    DeclarativeWorldContentState Content { get; }
    ICommunityHousingWorld HousingWorld { get; }
    long WorldTick { get; }
    long RelocationNoticeTicks { get; }
    string HouseholdFor(string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    bool IsAdult(string actor);
    PlacedBuilding? HouseForHousehold(string household);
    HouseResidentCapacityRules.Capacity? HouseResidentCapacity(string household);
    bool DepartHousehold(string actor, string cause);
    void EndHousingRequest(string actor, HousingRequest request, string eventKind, bool remember);
    void AppendEvent(WorldEventPayload payload);
}

internal interface ICommunitySkillWorld
{
    SocietyCheckpoint Society { get; }
    AgentStore PhysicalAgents { get; }
    DeclarativeWorldContentState Content { get; }
    long WorldTick { get; }
    void MarkCurrentSchema();
    void AppendEvent(WorldEventPayload payload);
}

internal interface ICommunityWorkWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    IReadOnlyList<PlacedBuilding> Buildings { get; }
    IReadOnlyList<WorldProductionJob> ProductionJobs { get; }
    IReadOnlyList<BuildingExpansionJob>? Expansions { get; }
    string HouseholdFor(string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    bool IsActiveProductionOrderJob(string job);
    bool CanFitExpansion(PlacedBuilding building, GridPoint position, BuildingFootprintRevision footprint, string job);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range = 0);
    void SetReservationDeadline(IReadOnlyList<Identity> ids, long deadline);
    void ResumeProduction(string job, long deadline, string? knife, string actor);
    void ResumeExpansion(string job, long deadline, string actor);
    void ReleaseUnavailablePausedInputs(IReadOnlyList<Identity> ids);
    void CancelProduction(IReadOnlySet<string> ids);
    void CancelPausedExpansions(IReadOnlySet<string> ids, string reason);
    void AppendEvent(WorldEventPayload payload);
}
