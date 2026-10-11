using ClankerWorld.Simulation.Cognition;
using System.Globalization;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Kernel;
using System.Text.Json;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private sealed partial class CivicWorldAdapter
    {
        public void AddBoatAccessCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => runtime.AddBoatAccessCandidates(candidates, actor, town);
        public void AddEquipmentCandidates(List<CognitionCandidate> candidates, string actor, PlaytestInhabitantState person) => runtime.AddEquipmentCandidates(candidates, actor, person);
        public void AddHandcartCandidates(List<CognitionCandidate> candidates, string actor,
                PlaytestInhabitantState person) => runtime.AddHandcartCandidates(candidates, actor, person);
        public void AddToolRepairCandidates(List<CognitionCandidate> candidates, string actor) => runtime.AddToolRepairCandidates(candidates, actor);
        public void AddTownProjectProposalCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => runtime.AddTownProjectProposalCandidates(candidates, actor, town);
        public bool AdultResident(string actor) => runtime.AdultResident(actor);
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public bool ApplyHandcartCandidate(string actor, PlaytestInhabitantState person, string candidateId, string? targetCartId = null, int maximumQuantity = int.MaxValue) => runtime.ApplyHandcartCandidate(actor, person, candidateId, targetCartId, maximumQuantity);
        public void ApplyInventoryTransition(Func<InventoryCheckpoint, InventoryCheckpoint> transition) => runtime.ApplyInventoryTransition(transition);
        public bool CanContinueEquipmentRepair(string actor) => runtime.CanContinueEquipmentRepair(actor);
        public bool CanPrepareEquipmentRepair(string actor, PlaytestInhabitantState person, InventoryLot lot) => runtime.CanPrepareEquipmentRepair(actor, person, lot);
        public IEnumerable<PlaytestInhabitantState> ChildrenNeedingCare(string actor) => runtime.ChildrenNeedingCare(actor);
        public bool ChildResident(string actor) => runtime.ChildResident(actor);
        public EquipmentRepairWork? ContinueEquipmentRepair(string actor) => runtime.ContinueEquipmentRepair(actor);
        public int DestinationRoom(string buildingId, InventoryLot? movingRoot = null) => runtime.DestinationRoom(buildingId, movingRoot);
        public PlacedBuilding? EquipmentRepairSite(string actor, InventoryLot lot) => runtime.EquipmentRepairSite(actor, lot);
        public List<GridPoint> FindUnoccupiedRoute(
                string inhabitantId,
                GridPoint origin,
                GridPoint destination,
                int interactionRange,
                bool allowSwimming = true,
                int additionalCarriedUnits = 0) => runtime.FindUnoccupiedRoute(inhabitantId, origin, destination, interactionRange, allowSwimming, additionalCarriedUnits);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public bool HasCarriedMaterial(string actor, string itemKind, int quantity) => runtime.HasCarriedMaterial(actor, itemKind, quantity);
        public PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag) => runtime.HouseholdBuildingWithTag(householdId, tag);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public IReadOnlyList<HouseholdLandUseRequest> HouseholdLandUseRequests { get => runtime.householdLandUseRequests; }
        public IReadOnlyList<HouseholdLandUseRight> HouseholdLandUseRights { get => runtime.householdLandUseRights; }
        public IEnumerable<string> IllDependentsNeedingCare(string adult) => runtime.IllDependentsNeedingCare(adult);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public void MaintainTownProjects() => runtime.MaintainTownProjects();
        public SeededMap Map { get => runtime.map; }
        public void MoveToward(
                string inhabitantId,
                PlaytestInhabitantState state,
                GridPoint destination,
                string reason,
                int interactionRange = 0,
                int horseMovementBudget = 100) => runtime.MoveToward(inhabitantId, state, destination, reason, interactionRange, horseMovementBudget);
        public long NextEventId { get => runtime.nextEventId; }
        public (string TownId, string AgreementId, string TermId)? NonviolentRemedySelection { get => runtime.nonviolentRemedySelection; set => runtime.nonviolentRemedySelection = value; }
        public TownProjectPayload? OfferedTownProjectPlan(TownRuntimeState town, string actor, string definition,
                string coordinates, string? name) => runtime.OfferedTownProjectPlan(town, actor, definition, coordinates, name);
        public long PersonalWarehouseAvailableQuantity(string actor, string itemKind) => runtime.PersonalWarehouseAvailableQuantity(actor, itemKind);
        public (InventoryLot Lot, int Quantity)? PersonalWarehouseSurplus(string actor, string? itemKind = null,
                string? sourceLotId = null) => runtime.PersonalWarehouseSurplus(actor, itemKind, sourceLotId);
        public PlacedBuilding? Port(string id) => runtime.Port(id);
        public IEnumerable<InventoryLot> PreferredFood(string owner, string? actor = null) => runtime.PreferredFood(owner, actor);
        public void RepairEquipment(string actor, PlaytestInhabitantState person, string? requestedLotId = null, string? orderInstructionId = null) => runtime.RepairEquipment(actor, person, requestedLotId, orderInstructionId);
        public string? RepairTool(string actor, PlaytestInhabitantState state, string lotId,
                IReadOnlyList<string>? protectedToolIds = null) => runtime.RepairTool(actor, state, lotId, protectedToolIds);
        public int SkilledWorkTicks(string actor, SettlementSkillKind skill, int ticks) => runtime.SkilledWorkTicks(actor, skill, ticks);
        public IReadOnlyList<TownLandTitleRecord> TownLandTitles { get => runtime.townLandTitles; }
        public string? TownProjectSiteFailure(TownRuntimeState town, TownProjectPayload plan,
                string? projectId = null, string? actor = null, bool ignorePendingRequests = false) => runtime.TownProjectSiteFailure(town, plan, projectId, actor, ignorePendingRequests);
        public IReadOnlyList<TownRuntimeState> Towns { get => runtime.towns; }
        public int TravelStepCost(string actor, GridPoint from, GridPoint to) => runtime.TravelStepCost(actor, from, to);
        public PlacedBuilding? WarehouseForResident(string actor) => runtime.WarehouseForResident(actor);
        public DeclarativeWorldContentState WorldContent { get => runtime.worldContent; }
        public string WorldSeed { get => runtime.worldSeed; }
        public WorldContentSimulationState WorldSimulation { get => runtime.worldSimulation; set => runtime.worldSimulation = value; }
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
        public (TownGovernanceState Council, TownLandHearingState LandHearings) RequestPropertyCase(TownRuntimeState town, string actor, string subject, string choice, string statement, TownGovernanceState council, TownGovernmentState government) => runtime.RequestPropertyCase(town, actor, subject, choice, statement, council, government);
        public TownPropertySnapshot CaptureProperty(TownRuntimeState town, TownPropertyRequest request, int revision) => runtime.CaptureProperty(town, request, revision);
        public TownLandHearingState TransferCaseProperty(TownRuntimeState town, TownLandHearingState hearings, string caseId, TownPropertySnapshot prior) => runtime.TransferCaseProperty(town, hearings, caseId, prior);
        public int CaregiverFoodCarryRequirement(string actor, PlaytestInhabitantState parent) => runtime.CaregiverFoodCarryRequirement(actor, parent);
        public InventoryLot? SharedItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0) => runtime.SharedItem(kind, actor, returnTo, returnRange);
        public int MarketSurplus(string actor, InventoryLot lot) => runtime.MarketSurplus(actor, lot);
        public int PickupCarryCapacity(string actor, ClankerWorld.Simulation.Kernel.InventoryLot stock, GridPoint destination, int destinationRange = 0) => runtime.PickupCarryCapacity(actor, stock, destination, destinationRange);
        public GridPoint HouseholdStockPosition(InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public int PhysicalUnreservedQuantity(InventoryLot lot) => runtime.PhysicalUnreservedQuantity(lot);
        public IEnumerable<PlacedBuilding> WarehousesForTown(string townId) => runtime.WarehousesForTown(townId);
        public bool VesselFits(InventoryLot lot, int room) => runtime.VesselFits(lot, room);
        public void GenerateRoadBetweenTowns(TownRuntimeState town, PlacedBuilding firstBuilding) => runtime.GenerateRoadBetweenTowns(town, firstBuilding);
        public bool CanReachByFootOrSwimming(string actor, GridPoint from, GridPoint destination) => runtime.CanReachByFootOrSwimming(actor, from, destination);
        public PlacedBuilding? HouseForHousehold(string householdId) => runtime.HouseForHousehold(householdId);
        public bool CanFitHouseResident(string householdId, SocietyInhabitant proposedResident) => runtime.CanFitHouseResident(householdId, proposedResident);
        public List<GridPoint> GenerateRoadToBuilding(PlacedBuilding building, HashSet<GridPoint>? border = null) => runtime.GenerateRoadToBuilding(building, border);
        public IEnumerable<GoodsMatch> SpendingGoods(string actor, GoodsUse use, IReadOnlyList<string> owners, string? kind = null) => runtime.SpendingGoods(actor, use, owners, kind);
        public IReadOnlyList<FarmFieldState> Fields => runtime.fields;
        public IReadOnlyList<BridgeState> Bridges => runtime.bridges;
        public IReadOnlySet<GridPoint> RoadTiles { get => runtime.roadTiles; set => runtime.roadTiles = value.ToHashSet(); }
        public GroundOccupancy Ground => runtime.Ground;
        public bool HasGeneratedGeography => runtime.geographyOptions is not null;
        public ContentRegistryState ContentState => runtime.contentRegistry.ExportState();
        public void ApplySocietyTransition(Func<SocietyCheckpoint, SocietyOperationResult> operation) => runtime.society.Apply(operation);
        public void ResumeSociety() => runtime.society.Resume();
        public void AddInhabitant(string id, PlaytestInhabitantState person) => runtime.inhabitants.Add(id, person);
        public void SetInhabitant(string id, PlaytestInhabitantState person) => runtime.inhabitants[id] = person;
        public void RemoveInhabitant(string id) => runtime.inhabitants.Remove(id);
    }
    internal static class CivicSharedRules
    {
        internal static string ConductSeenId(string conduct, string observer) => PrivateWorldRuntime.ConductSeenId(conduct, observer);
        internal static string ConductInspectionId(string conduct, string file, string actor) => PrivateWorldRuntime.ConductInspectionId(conduct, file, actor);
        internal static string ConductRelayId(string conduct, long tick, string source, string recipient) => PrivateWorldRuntime.ConductRelayId(conduct, tick, source, recipient);
        internal static string NativeReturnId(long tick, string actor, long eventId) => PrivateWorldRuntime.NativeReturnId(tick, actor, eventId);
        internal static string WarehouseStockId(long tick, string actor) => PrivateWorldRuntime.WarehouseStockId(tick, actor);
        internal static string RemedyEffectId(string receipt) => PrivateWorldRuntime.RemedyEffectId(receipt);
        internal static string NonviolentRemedyChoiceId(string town, string agreement, string term) => PrivateWorldRuntime.NonviolentRemedyChoiceId(town, agreement, term);
        internal static string NonviolentRelayChoiceId(string town, string act, string listener) => PrivateWorldRuntime.NonviolentRelayChoiceId(town, act, listener);
        internal static TownProjectPayload BoatProjectPlan(PlacedBuilding port, string name) => PrivateWorldRuntime.BoatProjectPlan(port, name);
        internal static bool IsEdibleFood(string kind) => PrivateWorldRuntime.IsEdibleFood(kind);
        internal static bool NeedsUrgentFood(PlaytestInhabitantState person) => PrivateWorldRuntime.NeedsUrgentFood(person);
        internal static string RepairCartPrefix => PrivateWorldRuntime.RepairCartPrefix;
        internal static string RepairToolPrefix => PrivateWorldRuntime.RepairToolPrefix;
        internal static int ResourceInteractionRange => PrivateWorldRuntime.ResourceInteractionRange;
        internal static int WarehouseLoadQuantity => PrivateWorldRuntime.WarehouseLoadQuantity;
        internal static HashSet<string> WarehouseResourceKinds => PrivateWorldRuntime.WarehouseResourceKinds;
        internal static int HouseholdStockInteractionRange(InventoryLot lot) => PrivateWorldRuntime.HouseholdStockInteractionRange(lot);
        internal static string TownSalvageOperationId(long tick, string actor, string candidate) => PrivateWorldRuntime.TownSalvageOperationId(tick, actor, candidate);
        internal const string HouseholdId = PrivateWorldRuntime.HouseholdId;
        internal const string SecondHouseholdId = PrivateWorldRuntime.SecondHouseholdId;
        internal const string FoodLotId = PrivateWorldRuntime.FoodLotId;
        internal const string TownSalvagePrefix = PrivateWorldRuntime.TownSalvagePrefix;
        internal const string AdmissionApproved = PrivateWorldRuntime.AdmissionApproved;
        internal const string AdmissionAdmitted = PrivateWorldRuntime.AdmissionAdmitted;
        internal const string AdmissionLapsed = PrivateWorldRuntime.AdmissionLapsed;
        internal static IReadOnlyList<string> AdmissionLapseReasons => PrivateWorldRuntime.AdmissionLapseReasons;
        internal static IEnumerable<GridPoint> ExpansionTiles(BuildingExpansionJob job) => PrivateWorldRuntime.ExpansionTiles(job);
        internal static bool HasActiveContainerReservation(InventoryCheckpoint inventory, string containerId) => PrivateWorldRuntime.HasActiveContainerReservation(inventory, containerId);
        internal static int AvailableLotQuantity(InventoryCheckpoint inventory, InventoryLot lot) => PrivateWorldRuntime.AvailableLotQuantity(inventory, lot);
    }
}
