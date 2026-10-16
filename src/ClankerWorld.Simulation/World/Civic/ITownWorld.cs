using ClankerWorld.Simulation.Cognition;
using System.Globalization;
using ClankerWorld.Simulation.Society;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Kernel;
using System.Text.Json;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

internal interface ITownWorld : ICivicHistoryWorld, ICivicLandConsentWorld
{
    bool HasCommunalBoatPort(string townId);
    void AddBoatAccessCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town);
    void AddEquipmentCandidates(List<CognitionCandidate> candidates, string actor, AgentState person);
    void AddHandcartCandidates(List<CognitionCandidate> candidates, string actor,
            AgentState person);
    void AddToolRepairCandidates(List<CognitionCandidate> candidates, string actor);
    void AddTownProjectProposalCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town);
    bool IsAdult(string actor);
    IReadOnlyList<GridPoint> ReachCivicNoticePlace(string actor, GridPoint noticePlace);
    IReadOnlyList<GridPoint> ReachLandSalePickup(string actor, PlannedEquipmentPickup pickup);
    IReadOnlyList<GridPoint> ReachLandSaleReturn(string actor, PlannedEquipmentPickup pickup, GridPoint noticePlace, int quantity);
    IReadOnlyList<GridPoint> ReachLandSaleNoticePlace(string actor, GridPoint noticePlace);
    void RefuseChoice(ActionPlan plan, ActionRefusal refusal);
    void AppendEvent(WorldEventPayload payload, GridPoint? eventPosition = null);
    bool ApplyHandcartCandidate(string actor, AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = int.MaxValue);
    void ApplyInventoryTransition(Func<InventoryCheckpoint, InventoryCheckpoint> transition);
    bool CanContinueEquipmentRepair(string actor);
    bool CanPrepareEquipmentRepair(string actor, AgentState person, InventoryLot lot);
    IEnumerable<AgentState> ChildrenNeedingCare(string actor);
    bool IsChild(string actor);
    EquipmentRepairWork? ContinueEquipmentRepair(string actor);
    int DestinationRoom(string buildingId, InventoryLot? movingRoot = null);
    PlacedBuilding? EquipmentRepairSite(string actor, InventoryLot lot);
    List<GridPoint> FindUnoccupiedRoute(
            string agentId,
            GridPoint origin,
            GridPoint destination,
            int interactionRange,
            bool allowSwimming = true,
            int additionalCarriedUnits = 0);
    int FreeCarryCapacity(string actor);
    bool HasCarriedMaterial(string actor, string itemKind, int quantity);
    PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag);
    string HouseholdFor(string actor);
    IReadOnlyList<HouseholdLandUseRequest> HouseholdLandUseRequests { get; }
    IReadOnlyList<HouseholdLandUseRight> HouseholdLandUseRights { get; }
    IEnumerable<string> IllDependentsNeedingCare(string adult);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange);
    void MaintainTownProjects();
    WorldMap Map { get; }
    void MoveToward(
            string agentId,
            AgentState state,
            GridPoint destination,
            string reason,
            int interactionRange = 0,
            int horseMovementBudget = 100);
    long NextEventId { get; }
    (string TownId, string AgreementId, string TermId)? NonLandRemedySelection { get; set; }
    TownProjectPayload? OfferedTownProjectPlan(TownRuntimeState town, string actor, string definition,
            string coordinates, string? name);
    long PersonalWarehouseAvailableQuantity(string actor, string itemKind);
    (InventoryLot Lot, int Quantity)? PersonalWarehouseSurplus(string actor, string? itemKind = null,
            string? sourceLotId = null);
    PlacedBuilding? Port(string id);
    IEnumerable<InventoryLot> PreferredFood(string owner, string? actor = null);
    void RepairEquipment(string actor, AgentState person, string? requestedLotId = null, string? orderInstructionId = null);
    string? RepairTool(string actor, AgentState state, string lotId,
            IReadOnlyList<string>? protectedToolIds = null);
    int SkilledWorkTicks(string actor, SkillKind skill, int ticks);
    IReadOnlyList<TownLandTitleRecord> TownLandTitles { get; }
    string? TownProjectSiteFailure(TownRuntimeState town, TownProjectPayload plan,
            string? projectId = null, string? actor = null, bool ignorePendingRequests = false);
    int TravelStepCost(string actor, GridPoint from, GridPoint to);
    PlacedBuilding? WarehouseForResident(string actor);
    DeclarativeWorldContentState WorldContent { get; }
    string WorldSeed { get; }
    WorldContentSimulationState WorldSimulation { get; set; }
    IEnumerable<InventoryLot> WornEquipmentItems(string actor);

    IEnumerable<GridPoint> RoadAndBridgeTiles();
    IEnumerable<GridPoint> TownProjectProtectedSites(string? exceptProjectId = null);
    HashSet<GridPoint> PendingTownProjectSiteTiles();
    IEnumerable<GridPoint> MarketSiteTiles();
    bool MayExpandBuilding(string actor, PlacedBuilding building, out string failure);
    IEnumerable<(GridPoint Position, BuildingFootprintRevision Footprint)> ExpansionShapes(PlacedBuilding building);
    bool CanFitExpansion(PlacedBuilding building, GridPoint position, BuildingFootprintRevision footprint, out string failure, string? ownJobId = null, bool requireLandRights = true);
    void AddTownPropertyCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town);
    bool PropertyCanTransfer(TownRuntimeState town, TownLandCase item);
    bool PropertyMayReopen(TownRuntimeState town, TownLandCase item);
    (TownCouncilState Council, TownLandRecords LandRecords) RequestPropertyCase(TownRuntimeState town, string actor, string subject, string choice, string statement, TownCouncilState council, TownGovernmentState government);
    TownPropertySnapshot CaptureProperty(TownRuntimeState town, TownPropertyRequest request, int revision);
    TownLandRecords TransferCaseProperty(TownRuntimeState town, TownLandRecords hearings, string caseId, TownPropertySnapshot prior);
    int CaregiverFoodCarryRequirement(string actor, AgentState parent);
    InventoryLot? SharedItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0);
    int MarketSurplus(string actor, InventoryLot lot);
    int PickupCarryCapacity(string actor, ClankerWorld.Simulation.Kernel.InventoryLot stock, GridPoint destination, int destinationRange = 0);
    GridPoint HouseholdStockPosition(InventoryLot lot);
    bool ReadyForBriefInteraction(string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    int PhysicalUnreservedQuantity(InventoryLot lot);
    IEnumerable<PlacedBuilding> WarehousesForTown(string townId);
    bool VesselFits(InventoryLot lot, int room);
    void GenerateRoadBetweenTowns(TownRuntimeState town, PlacedBuilding firstBuilding);
    bool CanReachByFootOrSwimming(string actor, GridPoint from, GridPoint destination);
    PlacedBuilding? HouseForHousehold(string householdId);
    bool CanFitHouseResident(string householdId, SocietyAgent proposedResident);
    List<GridPoint> GenerateRoadToBuilding(PlacedBuilding building, HashSet<GridPoint>? border = null);
    IEnumerable<GoodsMatch> SpendingGoods(string actor, GoodsUse use, IReadOnlyList<string> owners, string? kind = null);
    IReadOnlyList<FarmFieldState> Fields { get; }
    IReadOnlyList<BridgeState> Bridges { get; }
    IReadOnlySet<GridPoint> RoadTiles { get; set; }
    GroundOccupancy Ground { get; }
    bool HasGeneratedGeography { get; }
    ContentRegistryState ContentState { get; }
    void ApplySocietyTransition(Func<SocietyCheckpoint, SocietyOperationResult> operation);
    void ResumeSociety();
    void AddAgent(string id, AgentState person);
    void SetAgent(string id, AgentState person);
    void RemoveAgent(string id);
}
