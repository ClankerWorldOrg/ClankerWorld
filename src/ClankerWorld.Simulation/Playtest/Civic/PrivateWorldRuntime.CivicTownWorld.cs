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
        public void AcceptTownAdmission(string actor, string townId, string proposalId) => runtime.AcceptTownAdmission(actor, townId, proposalId);
        public void AddBoatAccessCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => runtime.AddBoatAccessCandidates(candidates, actor, town);
        public void AddEquipmentCandidates(List<CognitionCandidate> candidates, string actor, PlaytestInhabitantState person) => runtime.AddEquipmentCandidates(candidates, actor, person);
        public void AddHandcartCandidates(List<CognitionCandidate> candidates, string actor,
                PlaytestInhabitantState person) => runtime.AddHandcartCandidates(candidates, actor, person);
        public void AddHouseholdLandCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => runtime.AddHouseholdLandCandidates(candidates, actor, town);
        public void AddToolRepairCandidates(List<CognitionCandidate> candidates, string actor) => runtime.AddToolRepairCandidates(candidates, actor);
        public void AddTownAdmissionCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => runtime.AddTownAdmissionCandidates(candidates, actor, town);
        public void AddTownLandHearingCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => runtime.AddTownLandHearingCandidates(candidates, actor, town);
        public void AddTownLandTransferCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => runtime.AddTownLandTransferCandidates(candidates, actor, town);
        public void AddTownProjectProposalCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => runtime.AddTownProjectProposalCandidates(candidates, actor, town);
        public string AdmissionRequestText(string actor, TownRuntimeState town) => runtime.AdmissionRequestText(actor, town);
        public bool AdultResident(string actor) => runtime.AdultResident(actor);
        public (TownGovernanceState Council, TownLandHearingState LandHearings) AdvanceTownLandHearings(
                TownRuntimeState town, TownGovernanceState council, TownGovernmentState government) => runtime.AdvanceTownLandHearings(town, council, government);
        public (TownGovernanceState Council, TownLandHearingState LandHearings) AdvanceTownLandTransfers(
                TownRuntimeState town, TownGovernanceState council) => runtime.AdvanceTownLandTransfers(town, council);
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public (TownRuntimeState Town, TownGovernanceState Governance) ApplyApprovedTownLandClaims(
                TownRuntimeState town, TownGovernanceState updated) => runtime.ApplyApprovedTownLandClaims(town, updated);
        public bool ApplyHandcartCandidate(string actor, PlaytestInhabitantState person, string candidateId, string? targetCartId = null, int maximumQuantity = int.MaxValue) => runtime.ApplyHandcartCandidate(actor, person, candidateId, targetCartId, maximumQuantity);
        public void ApplyInventoryTransition(Func<InventoryCheckpoint, InventoryCheckpoint> transition) => runtime.ApplyInventoryTransition(transition);
        public (TownGovernanceState Council, TownLandHearingState LandHearings) ApplyTownLandHearingAction(TownRuntimeState town,
                string actor, string action, string subject, string choice, string? proposalText, IReadOnlyList<CognitionLandTile>? landTiles,
                CognitionLandHearingChoice? hearingChoice, TownGovernanceState council, TownGovernmentState government) => runtime.ApplyTownLandHearingAction(town, actor, action, subject, choice, proposalText, landTiles, hearingChoice, council, government);
        public (TownGovernanceState Council, TownLandHearingState LandHearings) ApplyTownLandTransferAction(
                TownRuntimeState town, string actor, string action, string subject, string choice,
                IReadOnlyList<CognitionLandTile>? landTiles, CognitionLandHearingChoice? hearingChoice, TownGovernanceState council) => runtime.ApplyTownLandTransferAction(town, actor, action, subject, choice, landTiles, hearingChoice, council);
        public bool CanContinueEquipmentRepair(string actor) => runtime.CanContinueEquipmentRepair(actor);
        public bool CanPrepareEquipmentRepair(string actor, PlaytestInhabitantState person, InventoryLot lot) => runtime.CanPrepareEquipmentRepair(actor, person, lot);
        public bool CanWalkToCivicBoard(string actor, TownRuntimeState town) => runtime.CanWalkToCivicBoard(actor, town);
        public IEnumerable<PlaytestInhabitantState> ChildrenNeedingCare(string actor) => runtime.ChildrenNeedingCare(actor);
        public bool ChildResident(string actor) => runtime.ChildResident(actor);
        public EquipmentRepairWork? ContinueEquipmentRepair(string actor) => runtime.ContinueEquipmentRepair(actor);
        public TownGovernanceState DecideHouseholdLandUse(TownGovernanceState state, string actor, string requestId, string action) => runtime.DecideHouseholdLandUse(state, actor, requestId, action);
        public int DestinationRoom(string buildingId, InventoryLot? movingRoot = null) => runtime.DestinationRoom(buildingId, movingRoot);
        public PlacedBuilding? EquipmentRepairSite(string actor, InventoryLot lot) => runtime.EquipmentRepairSite(actor, lot);
        public List<GridPoint> FindUnoccupiedRoute(
                string inhabitantId,
                GridPoint origin,
                GridPoint destination,
                int interactionRange,
                bool allowSwimming = true,
                int additionalCarriedUnits = 0) => runtime.FindUnoccupiedRoute(inhabitantId, origin, destination, interactionRange, allowSwimming, additionalCarriedUnits);
        public void FoundTown(string actor, string candidate) => runtime.FoundTown(actor, candidate);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public bool HasCarriedMaterial(string actor, string itemKind, int quantity) => runtime.HasCarriedMaterial(actor, itemKind, quantity);
        public PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag) => runtime.HouseholdBuildingWithTag(householdId, tag);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public IReadOnlyList<HouseholdLandUseRequest> HouseholdLandUseRequests { get => runtime.householdLandUseRequests; }
        public IReadOnlyList<HouseholdLandUseRight> HouseholdLandUseRights { get => runtime.householdLandUseRights; }
        public IEnumerable<string> IllDependentsNeedingCare(string adult) => runtime.IllDependentsNeedingCare(adult);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public bool LandHearingAdult(string actor) => runtime.LandHearingAdult(actor);
        public Dictionary<string, string?> LandHearingHouseholds() => runtime.LandHearingHouseholds();
        public string LandUseTerms(HouseholdLandUseRequest request) => runtime.LandUseTerms(request);
        public void MaintainTownProjects() => runtime.MaintainTownProjects();
        public SeededMap Map { get => runtime.map; }
        public bool MayBeSponsoredForAdmission(string nominee, TownRuntimeState town) => runtime.MayBeSponsoredForAdmission(nominee, town);
        public bool MayRequestOwnAdmission(string actor, TownRuntimeState town) => runtime.MayRequestOwnAdmission(actor, town);
        public bool MayResettleTown(string actor, TownRuntimeState town) => runtime.MayResettleTown(actor, town);
        public bool MayVisitAsNewcomer(string actor, TownRuntimeState town) => runtime.MayVisitAsNewcomer(actor, town);
        public bool MayVisitLandHearing(string actor, TownRuntimeState town) => runtime.MayVisitLandHearing(actor, town);
        public bool MayVisitLandTransfer(string actor, TownRuntimeState town) => runtime.MayVisitLandTransfer(actor, town);
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
        public GridPoint[] RequestableLandNear(TownRuntimeState town, GridPoint from, int count) => runtime.RequestableLandNear(town, from, count);
        public string ResettlementText(string actor, TownRuntimeState town) => runtime.ResettlementText(actor, town);
        public void ResettleTown(string actor, TownRuntimeState town) => runtime.ResettleTown(actor, town);
        public TownGovernanceState ResolveHouseholdLandRequests(TownRuntimeState town, TownGovernanceState state) => runtime.ResolveHouseholdLandRequests(town, state);
        public void SettleTownAdmissions() => runtime.SettleTownAdmissions();
        public void SetTown(TownRuntimeState updated) => runtime.SetTown(updated);
        public int SkilledWorkTicks(string actor, SettlementSkillKind skill, int ticks) => runtime.SkilledWorkTicks(actor, skill, ticks);
        public TownGovernanceState SubmitExpansionLandRequest(TownRuntimeState town, TownGovernanceState state, string actor, string buildingId) => runtime.SubmitExpansionLandRequest(town, state, actor, buildingId);
        public TownGovernanceState SubmitPersonalLandUseRequest(TownRuntimeState town, TownGovernanceState state,
                string actor, IReadOnlyList<CognitionLandTile>? landTiles) => runtime.SubmitPersonalLandUseRequest(town, state, actor, landTiles);
        public TownGovernanceState SubmitTownLandClaim(TownRuntimeState town, TownGovernanceState state,
                string actor, IReadOnlyList<CognitionLandTile>? requested) => runtime.SubmitTownLandClaim(town, state, actor, requested);
        public string? TownForResident(string residentId) => runtime.TownForResident(residentId);
        public TownRuntimeState? TownFoundingLayout(string actor) => runtime.TownFoundingLayout(actor);
        public IReadOnlyList<TownLandTitleRecord> TownLandTitles { get => runtime.townLandTitles; }
        public string? TownProjectSiteFailure(TownRuntimeState town, TownProjectPayload plan,
                string? projectId = null, string? actor = null, bool ignorePendingRequests = false) => runtime.TownProjectSiteFailure(town, plan, projectId, actor, ignorePendingRequests);
        public IReadOnlyList<TownRuntimeState> Towns { get => runtime.towns; }
        public int TravelStepCost(string actor, GridPoint from, GridPoint to) => runtime.TravelStepCost(actor, from, to);
        public PlacedBuilding? WarehouseForResident(string actor) => runtime.WarehouseForResident(actor);
        public DeclarativeWorldContentState WorldContent { get => runtime.worldContent; }
        public string WorldSeed { get => runtime.worldSeed; }
        public WorldContentSimulationState WorldSimulation { get => runtime.worldSimulation; }
        public IEnumerable<InventoryLot> WornEquipmentItems(string actor) => runtime.WornEquipmentItems(actor);
        public WorldSystemsConfig CalendarConfig => runtime.worldSystems.Config;
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
        internal static string LandHearingLawToken(TownLaw law, TownLawVersion version) => PrivateWorldRuntime.LandHearingLawToken(law, version);
        internal static string LandHearingRightText(HouseholdLandUseRight right) => PrivateWorldRuntime.LandHearingRightText(right);
        internal static string LandHearingRulingText(TownLandRuling ruling) => PrivateWorldRuntime.LandHearingRulingText(ruling);
        internal static string LandHearingTitleText(TownLandTitleRecord title) => PrivateWorldRuntime.LandHearingTitleText(title);
        internal static bool NeedsUrgentFood(PlaytestInhabitantState person) => PrivateWorldRuntime.NeedsUrgentFood(person);
        internal static string RepairCartPrefix => PrivateWorldRuntime.RepairCartPrefix;
        internal static string RepairToolPrefix => PrivateWorldRuntime.RepairToolPrefix;
        internal static int ResourceInteractionRange => PrivateWorldRuntime.ResourceInteractionRange;
        internal static bool TownLandHearingElectionBusy(TownRuntimeState town) => PrivateWorldRuntime.TownLandHearingElectionBusy(town);
        internal static int WarehouseLoadQuantity => PrivateWorldRuntime.WarehouseLoadQuantity;
        internal static HashSet<string> WarehouseResourceKinds => PrivateWorldRuntime.WarehouseResourceKinds;
    }
}
