using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private sealed partial class CivicWorldAdapter
    {
        public AgentStore PhysicalAgents => runtime.agentStore;
        public bool HasCommunalBoatPort(string townId) => TransportSystem.HasCommunalBoatPort(runtime.BoatWorld, townId);
        public void AddBoatAccessCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => runtime.AddBoatAccessCandidates(candidates, actor, town);
        public void AddEquipmentCandidates(List<CognitionCandidate> candidates, string actor, AgentState person) => runtime.AddEquipmentCandidates(candidates, actor, person);
        public void AddHandcartCandidates(List<CognitionCandidate> candidates, string actor,
                AgentState person) => runtime.AddHandcartCandidates(candidates, actor, person);
        public void AddToolRepairCandidates(List<CognitionCandidate> candidates, string actor) => runtime.AddToolRepairCandidates(candidates, actor);
        public void AddTownProjectProposalCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => runtime.AddTownProjectProposalCandidates(candidates, actor, town);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public IReadOnlyList<GridPoint> ReachNonLandService(string actor, GridPoint destination) =>
            runtime.Reach(actor, destination, ReachPurpose.NonLandService, 0, 0, SwimmingPolicy.WhenCapable);
        public ActionPlan? PlanNonLandRepair(string actor, string targetId)
        {
            var target = InventoryIndex.For(runtime.society.Checkpoint.Inventory).Find(targetId);
            if (target is null) return null;
            return target.ItemKind == InventoryContainerRules.Handcart ? TransportSystem.PlanHandcartRepair(runtime.HandcartWorld, actor, new(targetId)) :
                ToolProgressionRules.Find(target.ItemKind) is not null ? runtime.PlanToolRepair(actor, targetId, target) : runtime.PlanEquipmentRepair(actor, target);
        }
        public bool NonLandRepairProgressIsCurrent(string actor, EquipmentRepairWork progress) =>
            progress.MaterialReservationIds.All(id => runtime.society.Checkpoint.Inventory.Reservations.Any(reservation =>
                reservation.Id == id && reservation.State == InventoryReservationState.Reserved && reservation.ExpiryTick >= runtime.WorldTick)) && runtime.CanContinueEquipmentRepair(actor);
        public ActionCheck CheckNativeActionPlan(ActionPlan plan) => runtime.CheckActionPlan(plan);
        public void ApplyNonLandRepair(ActionPlan plan)
        {
            switch (plan)
            {
                case HandcartRepairActionPlan cart: runtime.ApplyHandcartRepairPlan(cart); break;
                case ToolRepairActionPlan tool: runtime.ApplyToolRepairPlan(tool); break;
                case EquipmentRepairActionPlan equipment: runtime.ApplyEquipmentRepairPlan(equipment); break;
                default: throw new InvalidOperationException("A remedy has an unsupported native repair plan.");
            }
        }
        public void AppendEvent(WorldEventPayload payload, GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public void RunInventoryCommand(InventorySystem.Command command) => runtime.RunInventoryCommand(command);
        public bool CanContinueEquipmentRepair(string actor) => runtime.CanContinueEquipmentRepair(actor);
        public bool CanPrepareEquipmentRepair(string actor, AgentState person, InventoryLot lot) => runtime.CanPrepareEquipmentRepair(actor, person, lot);
        public IEnumerable<AgentState> ChildrenNeedingCare(string actor) => FamilySystem.ChildrenNeedingCare(runtime.FamilyParenthoodWorld, actor);
        public bool IsChild(string actor) => runtime.IsChild(actor);
        public EquipmentRepairWork? ContinueEquipmentRepair(string actor) => runtime.ContinueEquipmentRepair(actor);
        public int DestinationRoom(string buildingId, InventoryLot? movingRoot = null) => runtime.DestinationRoom(buildingId, movingRoot);
        public PlacedBuilding? EquipmentRepairSite(string actor, InventoryLot lot) => runtime.EquipmentRepairSite(actor, lot);
        public IReadOnlyList<GridPoint> ReachLeg(
                string agentId,
                GridPoint origin,
                GridPoint destination,
                ReachPurpose purpose,
                int interactionRange,
                int additionalCarriedUnits,
                SwimmingPolicy swimming) => runtime.ReachLeg(agentId, origin, destination, purpose, interactionRange, additionalCarriedUnits, swimming);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public bool HasCarriedMaterial(string actor, string itemKind, int quantity) => runtime.HasCarriedMaterial(actor, itemKind, quantity);
        public PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag) => runtime.HouseholdBuildingWithTag(householdId, tag);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public IReadOnlyList<HouseholdLandUseRequest> HouseholdLandUseRequests { get => runtime.householdLandUseRequests; }
        public IReadOnlyList<HouseholdLandUseRight> HouseholdLandUseRights { get => runtime.householdLandUseRights; }
        public IEnumerable<string> IllDependentsNeedingCare(string adult) => FamilySystem.IllDependentsNeedingCare(runtime.FamilyCareWorld, adult);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public void MaintainTownProjects() => runtime.MaintainTownProjects();
        public WorldMap Map { get => runtime.map; }
        public void MoveToward(
                string agentId,
                AgentState state,
                GridPoint destination,
                string reason,
                int interactionRange = 0,
                int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public long NextEventId { get => runtime.nextEventId; }
        public (string TownId, string AgreementId, string TermId)? NonLandRemedySelection { get => runtime.nonLandRemedySelection; set => runtime.nonLandRemedySelection = value; }
        public TownProjectPayload? OfferedTownProjectPlan(TownRuntimeState town, string actor, string definition,
                string coordinates, string? name) => runtime.OfferedTownProjectPlan(town, actor, definition, coordinates, name);
        public long PersonalWarehouseAvailableQuantity(string actor, string itemKind) => runtime.PersonalWarehouseAvailableQuantity(actor, itemKind);
        public (InventoryLot Lot, int Quantity)? PersonalWarehouseSurplus(string actor, string? itemKind = null,
                string? sourceLotId = null) => runtime.PersonalWarehouseSurplus(actor, itemKind, sourceLotId);
        public PlacedBuilding? Port(string id) => runtime.Port(id);
        public IEnumerable<InventoryLot> PreferredFood(string owner, string? actor = null) => runtime.PreferredFood(owner, actor);
        public int SkilledWorkTicks(string actor, SkillKind skill, int ticks) => runtime.SkilledWorkTicks(actor, skill, ticks);
        public IReadOnlyList<TownLandTitleRecord> TownLandTitles { get => runtime.townLandTitles; }
        public bool TownProjectDemandIsCurrent(TownRuntimeState town, TownProjectPayload plan) => WorldRuntime.TownProjectDemandIsCurrent(town, plan);
        public string? TownProjectSiteFailure(TownRuntimeState town, TownProjectPayload plan,
                string? projectId = null, string? actor = null, bool ignorePendingRequests = false) => runtime.TownProjectSiteFailure(town, plan, projectId, actor, ignorePendingRequests);
        public IReadOnlyList<TownRuntimeState> Towns { get => runtime.towns; }
        public int TravelStepCost(string actor, GridPoint from, GridPoint to) => TransportSystem.TravelStepCost(runtime.HandcartWorld, actor, from, to);
        public PlacedBuilding? WarehouseForResident(string actor) => runtime.WarehouseForResident(actor);
        public DeclarativeWorldContentState WorldContent { get => runtime.worldContent; }
        public string WorldSeed { get => runtime.worldSeed; }
        public WorldContentSimulationState WorldSimulation => runtime.worldSimulation;
        public void ReplaceFirstTownBuildings(IReadOnlyList<PlacedBuilding> placements) => runtime.constructionSystem.Buildings.ReplaceFirstTownLayout(placements);
        public void AssignFoundingBuildings(IReadOnlySet<string> assigned, string townId) => runtime.constructionSystem.Buildings.AssignFoundingTown(assigned, townId);
        public IEnumerable<InventoryLot> WornEquipmentItems(string actor) => runtime.WornEquipmentItems(actor);
        public WorldSystemsConfig CalendarConfig => runtime.worldSystems.Config;
        public string[] HouseholdAdults(string householdId) => runtime.HouseholdAdults(householdId);
        public IEnumerable<GridPoint> RoadAndBridgeTiles() => runtime.RoadAndBridgeTiles();
        public IEnumerable<GridPoint> TownProjectProtectedSites(string? exceptProjectId = null) => runtime.TownProjectProtectedSites(exceptProjectId);
        public HashSet<GridPoint> PendingTownProjectSiteTiles() => runtime.PendingTownProjectSiteTiles();
        public IEnumerable<GridPoint> MarketSiteTiles() => runtime.MarketSiteTiles();
        public bool MayExpandBuilding(string actor, PlacedBuilding building, out string failure) => runtime.MayExpandBuilding(actor, building, out failure);
        public IEnumerable<(GridPoint Position, BuildingFootprintRevision Footprint)> ExpansionShapes(PlacedBuilding building) => runtime.ExpansionShapes(building);
        public bool CanFitExpansion(PlacedBuilding building, GridPoint position, BuildingFootprintRevision footprint, out string failure, string? ownJobId = null, bool requireLandRights = true) => runtime.CanFitExpansion(building, position, footprint, out failure, ownJobId, requireLandRights);
        public void AddTownPropertyCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => runtime.AddTownPropertyCandidates(candidates, actor, town);
        public bool PropertyCanTransfer(TownRuntimeState town, TownLandCase item) => runtime.PropertyCanTransfer(town, item);
        public bool PropertyMayReopen(TownRuntimeState town, TownLandCase item) => runtime.PropertyMayReopen(town, item);
        public (TownCouncilState Council, TownLandRecords LandRecords) RequestPropertyCase(TownRuntimeState town, string actor, string subject, string choice, string statement, TownCouncilState council, TownGovernmentState government, LandHearingPropertyActionPlan? plan = null) => runtime.RequestPropertyCase(town, actor, subject, choice, statement, council, government, plan);
        public TownPropertySnapshot CaptureProperty(TownRuntimeState town, TownPropertyRequest request, int revision) => runtime.CaptureProperty(town, request, revision);
        public TownPropertySnapshot? TryCaptureProperty(TownRuntimeState town, TownPropertyRequest request, int revision, out string? failure) => runtime.TryCaptureProperty(town, request, revision, out failure);
        public TownLandRecords TransferCaseProperty(TownRuntimeState town, TownLandRecords hearings, string caseId, TownPropertySnapshot prior) => runtime.TransferCaseProperty(town, hearings, caseId, prior);
        public int CaregiverFoodCarryRequirement(string actor, AgentState parent) => FamilySystem.CaregiverFoodCarryRequirement(runtime.FamilyParenthoodWorld, actor, parent);
        public InventoryLot? SharedItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0) => runtime.SharedItem(kind, actor, returnTo, returnRange);
        public int MarketSurplus(string actor, InventoryLot lot) => MarketSystem.MarketSurplus(runtime.MarketStallWorld, actor, lot, null);
        public int PickupCarryCapacity(string actor, ClankerWorld.Simulation.Kernel.InventoryLot stock, GridPoint destination, int destinationRange = 0) => runtime.PickupCarryCapacity(actor, stock, destination, destinationRange);
        public GridPoint HouseholdStockPosition(InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public ActorCapabilityState? CapabilitiesFor(string actor) => runtime.CapabilitiesFor(actor);
        public IReadOnlyList<GridPoint> ReachCivicNoticePlace(string actor, GridPoint noticePlace) =>
            runtime.Reach(actor, noticePlace, ReachPurpose.CivicVisit, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable);
        public IReadOnlyList<GridPoint> ReachLandSalePickup(string actor, PlannedEquipmentPickup pickup) =>
            runtime.Reach(actor, pickup.Position, ReachPurpose.LandSalePayment, pickup.InteractionRange, 0, SwimmingPolicy.WhenCapable);
        public IReadOnlyList<GridPoint> ReachLandSaleNoticePlace(string actor, GridPoint noticePlace)
        {
            for (var dy = -ResourceInteractionRange; dy <= ResourceInteractionRange; dy++)
                for (var dx = -ResourceInteractionRange; dx <= ResourceInteractionRange; dx++)
                {
                    var tile = runtime.map.WrapColumn(new GridPoint(noticePlace.X + dx, noticePlace.Y + dy));
                    if (runtime.map.Contains(tile) && runtime.IsWithinInteractionRange(tile, noticePlace, ResourceInteractionRange) &&
                        runtime.Reach(actor, tile, ReachPurpose.LandSalePayment, 0, 0, SwimmingPolicy.WhenCapable, ReachObstacles.Terrain) is { Count: > 0 } route)
                        return route;
                }
            return [];
        }
        public IReadOnlyList<GridPoint> ReachLandSaleReturn(string actor, PlannedEquipmentPickup pickup, GridPoint noticePlace, int quantity)
        {
            if (runtime.PickupPosition(actor, pickup.Position, pickup.InteractionRange) is not { } origin) return [];
            var foot = runtime.ReachLeg(actor, origin, noticePlace, ReachPurpose.LandSalePayment, ResourceInteractionRange, 0, SwimmingPolicy.Never);
            return foot.Count > 0 ? foot : runtime.ReachLeg(actor, origin, noticePlace, ReachPurpose.LandSalePayment,
                ResourceInteractionRange, quantity, SwimmingPolicy.WhenCapable);
        }
        public void RefuseChoice(ActionPlan plan, ActionRefusal refusal) => runtime.RefuseChoice(plan, refusal);
        public int PhysicalUnreservedQuantity(InventoryLot lot) => runtime.PhysicalUnreservedQuantity(lot);
        public bool VesselFits(InventoryLot lot, int room) => runtime.VesselFits(lot, room);
        public void GenerateRoadBetweenTowns(TownRuntimeState town, PlacedBuilding firstBuilding) => runtime.GenerateRoadBetweenTowns(town, firstBuilding);
        public bool CanReachTerrain(string actor, GridPoint target, ReachPurpose purpose, int load, SwimmingPolicy swimming) => runtime.CanReachTerrain(actor, target, purpose, load, swimming);
        public PlacedBuilding? HouseForHousehold(string householdId) => runtime.HouseForHousehold(householdId);
        public bool CanFitHouseResident(string householdId, SocietyAgent proposedResident) => runtime.CanFitHouseResident(householdId, proposedResident);
        public List<GridPoint> GenerateRoadToBuilding(PlacedBuilding building, HashSet<GridPoint>? border = null) => TransportSystem.GenerateRoadToBuilding(runtime.RoadWorld, building, border);
        public IEnumerable<GoodsMatch> SpendingGoods(string actor, GoodsUse use, IReadOnlyList<string> owners, string? kind = null) => runtime.SpendingGoods(actor, use, owners, kind);
        public IReadOnlyList<FarmFieldState> Fields => runtime.fields;
        public IReadOnlyList<BridgeState> Bridges => runtime.bridges;
        public IReadOnlySet<GridPoint> RoadTiles { get => runtime.roadTiles; set => runtime.transportSystem.RestoreRoads(value); }
        public GroundOccupancy Ground => runtime.Ground;
        public bool HasGeneratedGeography => runtime.geographyOptions is not null;
        public ContentRegistryState ContentState => runtime.contentRegistry.ExportState();
        public void ApplySocietyTransition(Func<SocietyCheckpoint, SocietyOperationResult> operation) => runtime.society.Apply(operation);
        public void ResumeSociety() => runtime.society.Resume();
    }
    internal static class CivicSharedRules
    {
        internal static string ConductSeenId(string conduct, string observer) => WorldRuntime.ConductSeenId(conduct, observer);
        internal static string ConductInspectionId(string conduct, string file, string actor) => WorldRuntime.ConductInspectionId(conduct, file, actor);
        internal static string ConductRelayId(string conduct, long tick, string source, string recipient) => WorldRuntime.ConductRelayId(conduct, tick, source, recipient);
        internal static string NativeReturnId(long tick, string actor, long eventId) => WorldRuntime.NativeReturnId(tick, actor, eventId);
        internal static string WarehouseStockId(long tick, string actor) => WorldRuntime.WarehouseStockId(tick, actor);
        internal static string RemedyEffectId(string receipt) => WorldRuntime.RemedyEffectId(receipt);
        internal static string NonLandRemedyChoiceId(string town, string agreement, string term) => WorldRuntime.NonLandRemedyChoiceId(town, agreement, term);
        internal static string NonLandRelayChoiceId(string town, string act, string listener) => WorldRuntime.NonLandRelayChoiceId(town, act, listener);
        internal static TownProjectPayload BoatProjectPlan(PlacedBuilding port, string name) => WorldRuntime.BoatProjectPlan(port, name);
        internal static bool IsEdibleFood(string kind) => WorldRuntime.IsEdibleFood(kind);
        internal static bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        internal static string RepairCartPrefix => global::ClankerWorld.Simulation.World.HandcartRepairChoice.Prefix;
        internal static string RepairToolPrefix => WorldRuntime.RepairToolPrefix;
        internal static int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        internal static int WarehouseLoadQuantity => WorldRuntime.WarehouseLoadQuantity;
        internal static HashSet<string> WarehouseResourceKinds => WorldRuntime.WarehouseResourceKinds;
        internal static int HouseholdStockInteractionRange(InventoryLot lot) => WorldRuntime.HouseholdStockInteractionRange(lot);
        internal static string TownSalvageOperationId(long tick, string actor, string candidate) => WorldRuntime.TownSalvageOperationId(tick, actor, candidate);
        internal const string HouseholdId = WorldRuntime.HouseholdId;
        internal const string SecondHouseholdId = WorldRuntime.SecondHouseholdId;
        internal const string FoodLotId = WorldRuntime.FoodLotId;
        internal const string TownSalvagePrefix = WorldRuntime.TownSalvagePrefix;
        internal const string AdmissionApproved = WorldRuntime.AdmissionApproved;
        internal const string AdmissionAdmitted = WorldRuntime.AdmissionAdmitted;
        internal const string AdmissionLapsed = WorldRuntime.AdmissionLapsed;
        internal static IReadOnlyList<string> AdmissionLapseReasons => WorldRuntime.AdmissionLapseReasons;
        internal static IEnumerable<GridPoint> ExpansionTiles(BuildingExpansionJob job) => WorldRuntime.ExpansionTiles(job);
        internal static bool HasActiveContainerReservation(InventoryCheckpoint inventory, string containerId) => WorldRuntime.HasActiveContainerReservation(inventory, containerId);
    }
}
