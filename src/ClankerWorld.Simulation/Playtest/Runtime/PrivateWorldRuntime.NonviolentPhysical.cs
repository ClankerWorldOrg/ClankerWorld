using System.Security.Cryptography;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private const string NonviolentRemedyPrefix = CivicSystem.NonviolentRemedyPrefix;
    private const string NonviolentRelayPrefix = CivicSystem.NonviolentRelayPrefix;
    private const int UnfiledConductLimit = CivicSystem.UnfiledConductLimit;
    private (string TownId, string AgreementId, string TermId)? nonviolentRemedySelection;
    private static string NonviolentDigest<T>(T value) => CivicSystem.NonviolentDigest<T>(value);
    private static string ConductLawDigest(TownLawVersion version) => CivicSystem.ConductLawDigest(version);
    // This runs at the native successful action, never by searching the public event log.
    // A relevant law makes the act reportable, not illegal; only an agent may make that allegation.
    private void RecordNonviolentConduct(string actor, string kind, GridPoint position, string? targetId, string? itemKind, int quantity, string nativeActId) => civicSystem.RecordNonviolentConduct(civicWorld, actor, kind, position, targetId, itemKind, quantity, nativeActId);
    private static TownNonviolentState PruneNonviolentConduct(TownRuntimeState town, TownNonviolentState state) => CivicSystem.PruneNonviolentConduct(town, state);
    private static bool HasPriorLawNotice(TownRuntimeState town, TownViolationAllegation allegation, string subject) => CivicSystem.HasPriorLawNotice(town, allegation, subject);
    private static bool KnowsConductLaw(TownRuntimeState town, string actor, TownConductLawSnapshot snapshot) => CivicSystem.KnowsConductLaw(town, actor, snapshot);
    private bool CanObserveNonviolentAct(string observer, string actor, GridPoint position) => CivicSystem.CanObserveNonviolentAct(civicWorld, observer, actor, position);
    private PlacedBuilding? NonviolentPrivateSite(GridPoint position) => CivicSystem.NonviolentPrivateSite(civicWorld, position);
    private static string DescribeNonviolentConduct(TownConductRecord record) => CivicSystem.DescribeNonviolentConduct(record);
    private (TownViolationAllegation Allegation, TownCaseEvidence Evidence)[] NonviolentKnownReports(TownRuntimeState town, string actor) => CivicSystem.NonviolentKnownReports(civicWorld, town, actor);
    private TownCaseEvidence[] NonviolentKnownEvidence(TownRuntimeState town, TownViolationCase file, string actor) => CivicSystem.NonviolentKnownEvidence(civicWorld, town, file, actor);
    private TownCaseEvidence NonviolentEvidence(TownConductRecord record, TownConductAcquisition acquired, string actor, int revision) => CivicSystem.NonviolentEvidence(civicWorld, record, acquired, actor, revision);
    private TownNonviolentState AcquireNonviolentInspectedSources(TownRuntimeState town, TownViolationCase file, string actor) => CivicSystem.AcquireNonviolentInspectedSources(civicWorld, town, file, actor);
    private bool RelayNonviolentKnownReport(TownRuntimeState town, string source, string recipient, string incidentId, string statement) => civicSystem.RelayNonviolentKnownReport(civicWorld, town, source, recipient, incidentId, statement);
    private bool NonviolentRemedyTermKnownTo(TownRuntimeState town, TownRemedyTerm term, string actor) => CivicSystem.NonviolentRemedyTermKnownTo(civicWorld, town, term, actor);
    private bool NonviolentWorkAllowed(string actor) => CivicSystem.NonviolentWorkAllowed(civicWorld, actor);
    private bool NonviolentEquipmentRepairPaused(string actor) => CivicSystem.NonviolentEquipmentRepairPaused(civicWorld, actor);
    private InventoryLot? NonviolentReturnLot(TownRemedyTerm term) => CivicSystem.NonviolentReturnLot(civicWorld, term);
    private bool NonviolentRemedyFeasible(TownRuntimeState town, TownRemedyTerm term) => CivicSystem.NonviolentRemedyFeasible(civicWorld, town, term);
    private bool NonviolentRemedyDurationFeasible(TownRuntimeState town, TownRemedyTerm[] terms, long ticks) => CivicSystem.NonviolentRemedyDurationFeasible(civicWorld, town, terms, ticks);
    private bool NonviolentRemedyCapacityFeasible(IEnumerable<TownRemedyTerm> terms) => CivicSystem.NonviolentRemedyCapacityFeasible(civicWorld, terms);
    private static string NonviolentRemedyCandidateId(TownRuntimeState town, TownRestorativeAgreement agreement, TownRemedyTerm term) => CivicSystem.NonviolentRemedyCandidateId(town, agreement, term);
    private static int NonviolentRemaining(TownNonviolentState state, TownRestorativeAgreement agreement, TownRemedyTerm term) => CivicSystem.NonviolentRemaining(state, agreement, term);
    private void AddNonviolentRemedyCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => CivicSystem.AddNonviolentRemedyCandidates(civicWorld, candidates, actor, town);
    private bool ApplyNonviolentRemedyAction(TownRuntimeState town, string actor, string candidateId) => civicSystem.ApplyNonviolentRemedyAction(civicWorld, town, actor, candidateId);
    private static string NonviolentRelayCandidateId(TownRuntimeState town, TownConductRecord record, string listener) => CivicSystem.NonviolentRelayCandidateId(town, record, listener);
    private void DeliverNonviolentTownGoods(string actor, TownRemedyTerm term, int remaining) => civicSystem.DeliverNonviolentTownGoods(civicWorld, actor, term, remaining);
    private void RecordNonviolentRepairCompletion(string actor, InventoryLot originalTarget, string repairedLotId, string nativeReceiptId, GridPoint position, IReadOnlyList<string> reservationIds) => civicSystem.RecordNonviolentRepairCompletion(civicWorld, actor, originalTarget, repairedLotId, nativeReceiptId, position, reservationIds);
    private void RecordNonviolentGoodsCompletion(string actor, InventoryLot sourceLot, string recipientId, int quantity, string? storageBuildingId, string nativeReceiptId, string kind, long? carriedAvailableBefore = null) => civicSystem.RecordNonviolentGoodsCompletion(civicWorld, actor, sourceLot, recipientId, quantity, storageBuildingId, nativeReceiptId, kind, carriedAvailableBefore);
    private void RecordNonviolentNativeReceipt(TownNativeRemedyReceipt receipt) => civicSystem.RecordNonviolentNativeReceipt(civicWorld, receipt);
    private static IReadOnlyList<ContentQuantity> NonviolentRepairMaterials(string itemKind) => CivicSystem.NonviolentRepairMaterials(itemKind);
}
