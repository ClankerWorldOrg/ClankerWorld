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

internal interface ICivicTownWorld : ICivicHistoryWorld
{
    void AcceptTownAdmission(string actor, string townId, string proposalId);
    void AddBoatAccessCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town);
    void AddEquipmentCandidates(List<CognitionCandidate> candidates, string actor, PlaytestInhabitantState person);
    void AddHandcartCandidates(List<CognitionCandidate> candidates, string actor,
            PlaytestInhabitantState person);
    void AddHouseholdLandCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town);
    void AddToolRepairCandidates(List<CognitionCandidate> candidates, string actor);
    void AddTownAdmissionCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town);
    void AddTownLandHearingCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town);
    void AddTownLandTransferCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town);
    void AddTownProjectProposalCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town);
    string AdmissionRequestText(string actor, TownRuntimeState town);
    bool AdultResident(string actor);
    (TownGovernanceState Council, TownLandHearingState LandHearings) AdvanceTownLandHearings(
            TownRuntimeState town, TownGovernanceState council, TownGovernmentState government);
    (TownGovernanceState Council, TownLandHearingState LandHearings) AdvanceTownLandTransfers(
            TownRuntimeState town, TownGovernanceState council);
    void AppendEvent(string kind, string detail, GridPoint? eventPosition = null);
    (TownRuntimeState Town, TownGovernanceState Governance) ApplyApprovedTownLandClaims(
            TownRuntimeState town, TownGovernanceState updated);
    bool ApplyHandcartCandidate(string actor, PlaytestInhabitantState person, string candidateId, string? targetCartId = null, int maximumQuantity = int.MaxValue);
    void ApplyInventoryTransition(Func<InventoryCheckpoint, InventoryCheckpoint> transition);
    (TownGovernanceState Council, TownLandHearingState LandHearings) ApplyTownLandHearingAction(TownRuntimeState town,
            string actor, string action, string subject, string choice, string? proposalText, IReadOnlyList<CognitionLandTile>? landTiles,
            CognitionLandHearingChoice? hearingChoice, TownGovernanceState council, TownGovernmentState government);
    (TownGovernanceState Council, TownLandHearingState LandHearings) ApplyTownLandTransferAction(
            TownRuntimeState town, string actor, string action, string subject, string choice,
            IReadOnlyList<CognitionLandTile>? landTiles, CognitionLandHearingChoice? hearingChoice, TownGovernanceState council);
    bool CanContinueEquipmentRepair(string actor);
    bool CanPrepareEquipmentRepair(string actor, PlaytestInhabitantState person, InventoryLot lot);
    bool CanWalkToCivicBoard(string actor, TownRuntimeState town);
    IEnumerable<PlaytestInhabitantState> ChildrenNeedingCare(string actor);
    bool ChildResident(string actor);
    EquipmentRepairWork? ContinueEquipmentRepair(string actor);
    TownGovernanceState DecideHouseholdLandUse(TownGovernanceState state, string actor, string requestId, string action);
    int DestinationRoom(string buildingId, InventoryLot? movingRoot = null);
    PlacedBuilding? EquipmentRepairSite(string actor, InventoryLot lot);
    List<GridPoint> FindUnoccupiedRoute(
            string inhabitantId,
            GridPoint origin,
            GridPoint destination,
            int interactionRange,
            bool allowSwimming = true,
            int additionalCarriedUnits = 0);
    void FoundTown(string actor, string candidate);
    int FreeCarryCapacity(string actor);
    bool HasCarriedMaterial(string actor, string itemKind, int quantity);
    PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag);
    string HouseholdFor(string actor);
    IReadOnlyList<HouseholdLandUseRequest> HouseholdLandUseRequests { get; }
    IReadOnlyList<HouseholdLandUseRight> HouseholdLandUseRights { get; }
    IEnumerable<string> IllDependentsNeedingCare(string adult);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange);
    bool LandHearingAdult(string actor);
    Dictionary<string, string?> LandHearingHouseholds();
    string LandUseTerms(HouseholdLandUseRequest request);
    void MaintainTownProjects();
    SeededMap Map { get; }
    bool MayBeSponsoredForAdmission(string nominee, TownRuntimeState town);
    bool MayRequestOwnAdmission(string actor, TownRuntimeState town);
    bool MayResettleTown(string actor, TownRuntimeState town);
    bool MayVisitAsNewcomer(string actor, TownRuntimeState town);
    bool MayVisitLandHearing(string actor, TownRuntimeState town);
    bool MayVisitLandTransfer(string actor, TownRuntimeState town);
    void MoveToward(
            string inhabitantId,
            PlaytestInhabitantState state,
            GridPoint destination,
            string reason,
            int interactionRange = 0,
            int horseMovementBudget = 100);
    long NextEventId { get; }
    (string TownId, string AgreementId, string TermId)? NonviolentRemedySelection { get; set; }
    TownProjectPayload? OfferedTownProjectPlan(TownRuntimeState town, string actor, string definition,
            string coordinates, string? name);
    long PersonalWarehouseAvailableQuantity(string actor, string itemKind);
    (InventoryLot Lot, int Quantity)? PersonalWarehouseSurplus(string actor, string? itemKind = null,
            string? sourceLotId = null);
    PlacedBuilding? Port(string id);
    IEnumerable<InventoryLot> PreferredFood(string owner, string? actor = null);
    void RepairEquipment(string actor, PlaytestInhabitantState person, string? requestedLotId = null, string? orderInstructionId = null);
    string? RepairTool(string actor, PlaytestInhabitantState state, string lotId,
            IReadOnlyList<string>? protectedToolIds = null);
    GridPoint[] RequestableLandNear(TownRuntimeState town, GridPoint from, int count);
    string ResettlementText(string actor, TownRuntimeState town);
    void ResettleTown(string actor, TownRuntimeState town);
    TownGovernanceState ResolveHouseholdLandRequests(TownRuntimeState town, TownGovernanceState state);
    void SettleTownAdmissions();
    void SetTown(TownRuntimeState updated);
    int SkilledWorkTicks(string actor, SettlementSkillKind skill, int ticks);
    TownGovernanceState SubmitExpansionLandRequest(TownRuntimeState town, TownGovernanceState state, string actor, string buildingId);
    TownGovernanceState SubmitPersonalLandUseRequest(TownRuntimeState town, TownGovernanceState state,
            string actor, IReadOnlyList<CognitionLandTile>? landTiles);
    TownGovernanceState SubmitTownLandClaim(TownRuntimeState town, TownGovernanceState state,
            string actor, IReadOnlyList<CognitionLandTile>? requested);
    string? TownForResident(string residentId);
    TownRuntimeState? TownFoundingLayout(string actor);
    IReadOnlyList<TownLandTitleRecord> TownLandTitles { get; }
    string? TownProjectSiteFailure(TownRuntimeState town, TownProjectPayload plan,
            string? projectId = null, string? actor = null, bool ignorePendingRequests = false);
    int TravelStepCost(string actor, GridPoint from, GridPoint to);
    PlacedBuilding? WarehouseForResident(string actor);
    DeclarativeWorldContentState WorldContent { get; }
    string WorldSeed { get; }
    WorldContentSimulationState WorldSimulation { get; }
    IEnumerable<InventoryLot> WornEquipmentItems(string actor);
}
