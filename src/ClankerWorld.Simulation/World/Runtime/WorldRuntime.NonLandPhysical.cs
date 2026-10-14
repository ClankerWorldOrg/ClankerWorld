using System.Security.Cryptography;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string NonLandRemedyPrefix = TownSystem.NonLandRemedyPrefix;
    private const string NonLandRelayPrefix = TownSystem.NonLandRelayPrefix;
    private const int UnfiledConductLimit = TownSystem.UnfiledConductLimit;
    private (string TownId, string AgreementId, string TermId)? nonLandRemedySelection;
    private static string NonLandDigest<T>(T value) => TownSystem.NonLandDigest<T>(value);
    private static string ConductLawDigest(TownLawVersion version) => TownSystem.ConductLawDigest(version);
    // This runs at the native successful action, never by searching the public event log.
    // A relevant law makes the act reportable, not illegal; only an agent may make that allegation.
    private void RecordNonLandConduct(string actor, string kind, GridPoint position, string? targetId, string? itemKind, int quantity, string nativeActId) => townSystem.RecordNonLandConduct(civicWorld, actor, kind, position, targetId, itemKind, quantity, nativeActId);
    private static TownNonLandHearingState PruneNonLandConduct(TownRuntimeState town, TownNonLandHearingState state) => TownSystem.PruneNonLandConduct(town, state);
    private static bool HasPriorLawNotice(TownRuntimeState town, TownViolationAllegation allegation, string subject) => TownSystem.HasPriorLawNotice(town, allegation, subject);
    private static bool KnowsConductLaw(TownRuntimeState town, string actor, TownConductLawSnapshot snapshot) => TownSystem.KnowsConductLaw(town, actor, snapshot);
    private bool CanObserveNonLandAct(string observer, string actor, GridPoint position) => TownSystem.CanObserveNonLandAct(civicWorld, observer, actor, position);
    private PlacedBuilding? NonLandPrivateSite(GridPoint position) => TownSystem.NonLandPrivateSite(civicWorld, position);
    private static string DescribeNonLandConduct(TownConductRecord record) => TownSystem.DescribeNonLandConduct(record);
    private (TownViolationAllegation Allegation, TownCaseEvidence Evidence)[] NonLandKnownReports(TownRuntimeState town, string actor) => TownSystem.NonLandKnownReports(civicWorld, town, actor);
    private TownCaseEvidence[] NonLandKnownEvidence(TownRuntimeState town, TownNonLandCase file, string actor) => TownSystem.NonLandKnownEvidence(civicWorld, town, file, actor);
    private TownCaseEvidence NonLandEvidence(TownConductRecord record, TownConductAcquisition acquired, string actor, int revision) => TownSystem.NonLandEvidence(civicWorld, record, acquired, actor, revision);
    private TownNonLandHearingState AcquireNonLandInspectedSources(TownRuntimeState town, TownNonLandCase file, string actor) => TownSystem.AcquireNonLandInspectedSources(civicWorld, town, file, actor);
    private bool RelayNonLandKnownReport(TownRuntimeState town, string source, string recipient, string incidentId, string statement) => townSystem.RelayNonLandKnownReport(civicWorld, town, source, recipient, incidentId, statement);
    private bool NonLandRemedyTermKnownTo(TownRuntimeState town, TownRemedyTerm term, string actor) => townSystem.NonLandRemedyTermKnownTo(civicWorld, town, term, actor);
    private bool NonLandWorkAllowed(string actor) => TownSystem.NonLandWorkAllowed(civicWorld, actor);
    private bool NonLandEquipmentRepairPaused(string actor) => TownSystem.NonLandEquipmentRepairPaused(civicWorld, actor);
    private InventoryLot? NonLandReturnLot(TownRemedyTerm term) => TownSystem.NonLandReturnLot(civicWorld, term);
    private bool NonLandRemedyFeasible(TownRuntimeState town, TownRemedyTerm term) => TownSystem.NonLandRemedyFeasible(civicWorld, town, term);
    private bool NonLandRemedyDurationFeasible(TownRuntimeState town, TownRemedyTerm[] terms, long ticks) => TownSystem.NonLandRemedyDurationFeasible(civicWorld, town, terms, ticks);
    private bool NonLandRemedyCapacityFeasible(IEnumerable<TownRemedyTerm> terms) => TownSystem.NonLandRemedyCapacityFeasible(civicWorld, terms);
    private static string NonLandRemedyCandidateId(TownRuntimeState town, TownRestorativeAgreement agreement, TownRemedyTerm term) => TownSystem.NonLandRemedyCandidateId(town, agreement, term);
    private static int NonLandRemaining(TownNonLandHearingState state, TownRestorativeAgreement agreement, TownRemedyTerm term) => TownSystem.NonLandRemaining(state, agreement, term);
    private void AddNonLandRemedyCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => TownSystem.AddNonLandRemedyCandidates(civicWorld, candidates, actor, town);
    private bool ApplyNonLandRemedyAction(TownRuntimeState town, string actor, string candidateId, ActionPlan? plan = null) => townSystem.ApplyNonLandRemedyAction(civicWorld, town, actor, candidateId, plan);
    private static string NonLandRelayCandidateId(TownRuntimeState town, TownConductRecord record, string listener) => TownSystem.NonLandRelayCandidateId(town, record, listener);
    private void DeliverNonLandTownGoods(string actor, TownRemedyTerm term, int remaining) => townSystem.DeliverNonLandTownGoods(civicWorld, actor, term, remaining);
    private void RecordNonLandRepairCompletion(string actor, InventoryLot originalTarget, string repairedLotId, string nativeReceiptId, GridPoint position, IReadOnlyList<string> reservationIds) => townSystem.RecordNonLandRepairCompletion(civicWorld, actor, originalTarget, repairedLotId, nativeReceiptId, position, reservationIds);
    private void RecordNonLandGoodsCompletion(string actor, InventoryLot sourceLot, string recipientId, int quantity, string? storageBuildingId, string nativeReceiptId, string kind, long? carriedAvailableBefore = null) => townSystem.RecordNonLandGoodsCompletion(civicWorld, actor, sourceLot, recipientId, quantity, storageBuildingId, nativeReceiptId, kind, carriedAvailableBefore);
    private void RecordNonLandNativeReceipt(TownNativeRemedyReceipt receipt) => townSystem.RecordNonLandNativeReceipt(civicWorld, receipt);
    private static IReadOnlyList<ContentQuantity> NonLandRepairMaterials(string itemKind) => TownSystem.NonLandRepairMaterials(itemKind);
}
