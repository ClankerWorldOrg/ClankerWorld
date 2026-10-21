using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static TownSystem.CivicHistoryIndex CivicHistory(TownRuntimeState town) => TownSystem.CivicHistory(town);
    private string[] TownAdults(TownRuntimeState town) => TownSystem.TownAdults(civicWorld, town);
    private int CivicDay => TownSystem.CivicDay(civicWorld);

    /// <summary>
    /// The calendar day a tick falls on, counted from 1 as agents are told it.
    /// A world that starts in the morning reaches its next day before a full
    /// day has elapsed, so this is not the tick divided by a day's length.
    /// </summary>
    internal long CivicDayNumber(long tick) => TownSystem.CivicDayNumber(civicWorld, tick);
    private string? CivicNote(string actor) => TownSystem.CivicNote(civicWorld, actor);
    private void AdvanceTownCouncil() => townSystem.AdvanceTownCouncil(civicWorld);
    private bool NearCivicBoard(string actor, TownRuntimeState town) => TownSystem.NearCivicBoard(civicWorld, actor, town);
    private static string CivicAgentToken(string id) => TownSystem.CivicAgentToken(id);
    private string CivicAction(string town, string kind, string subject = "", string choice = "") => TownSystem.CivicAction(civicWorld, town, kind, subject, choice);
    private static string CivicRoundToken(TownElection election) => TownSystem.CivicRoundToken(election);
    private void AddTownCivicCandidates(List<CognitionCandidate> candidates, string actor, IReadOnlyList<string>? buildingsWithoutSites = null) => townSystem.AddTownCivicCandidates(civicWorld, candidates, actor, buildingsWithoutSites);
    private void ContinueTownCivicVisit(string actor, string candidate) => townSystem.ContinueTownCivicVisit(civicWorld, actor, candidate);
    private void ApplyTownCivicCandidate(string actor, string candidate, string? proposalText = null, IReadOnlyList<string>? ballot = null, IReadOnlyList<CognitionLandTile>? landTiles = null, CognitionLandHearingChoice? hearingChoice = null, CognitionNonLandChoice? nonLandChoice = null) => ApplyPlannedTownCivicCandidate(actor, candidate, proposalText, ballot, landTiles, hearingChoice, nonLandChoice);
    private void ApplyPlannedTownCivicCandidate(string actor, string candidate, string? proposalText = null, IReadOnlyList<string>? ballot = null, IReadOnlyList<CognitionLandTile>? landTiles = null, CognitionLandHearingChoice? hearingChoice = null, CognitionNonLandChoice? nonLandChoice = null, ActionPlan? originalPlan = null)
    {
        var definition = candidate.StartsWith("civic|", StringComparison.Ordinal) ? ActionRegistrations.FirstOrDefault(row => row.Phase == ActionPhase.FreshAdmission &&
            row.Definition?.Matches(candidate) == true)?.Definition : null;
        if (definition is null)
        {
            townSystem.ApplyTownCivicCandidate(civicWorld, actor, candidate, proposalText, ballot, landTiles, hearingChoice, nonLandChoice, originalPlan);
            return;
        }
        originalPlan ??= definition.PlanCandidate(this, actor, candidate);
        if (originalPlan is CouncilBallotActionPlan selected)
            originalPlan = TownSystem.BindCouncilBallot(civicWorld, selected, ballot);
        if (originalPlan is TownLawActionPlan law)
            originalPlan = TownSystem.BindTownLawText(law, proposalText);
        if (originalPlan is LandTransferProposalActionPlan transfer)
            originalPlan = TownSystem.BindLandTransferProposal(transfer, landTiles, hearingChoice);
        if (originalPlan is HouseholdLandFilingActionPlan filing)
            originalPlan = TownSystem.BindHouseholdLandFiling(filing, landTiles);
        if (originalPlan is TownLandClaimActionPlan claim)
            originalPlan = TownSystem.BindTownLandClaim(claim, landTiles);
        if (originalPlan is LandHearingPropertyActionPlan hearingProperty)
            originalPlan = TownSystem.BindLandHearingProperty(hearingProperty, hearingChoice, proposalText);
        if (originalPlan is LandHearingReopeningActionPlan hearingReopening)
            originalPlan = TownSystem.BindLandHearingReopening(hearingReopening, hearingChoice);
        if (originalPlan is LandHearingRulingActionPlan hearingRuling)
            originalPlan = TownSystem.BindLandHearingRuling(hearingRuling, hearingChoice, proposalText);
        if (originalPlan is LandHearingEvidenceActionPlan hearingEvidence)
            originalPlan = TownSystem.BindLandHearingEvidence(hearingEvidence, hearingChoice, proposalText);
        if (originalPlan is LandHearingFilingActionPlan hearingFiling)
            originalPlan = TownSystem.BindLandHearingFiling(hearingFiling, landTiles, hearingChoice, proposalText);
        if (originalPlan is NonLandAgreementActionPlan agreement)
            originalPlan = TownSystem.BindNonLandAgreement(agreement, nonLandChoice, TownSystem.CivicDay(civicWorld));
        if (originalPlan is NonLandAdjudicationActionPlan adjudication)
            originalPlan = TownSystem.BindNonLandAdjudication(adjudication, nonLandChoice);
        if (originalPlan is NonLandEvidenceActionPlan evidence)
            originalPlan = TownSystem.BindNonLandEvidence(evidence, nonLandChoice);
        if (originalPlan is NonLandFilingActionPlan nonLandFiling)
            originalPlan = TownSystem.BindNonLandFiling(nonLandFiling, nonLandChoice);
        if (originalPlan is TownProjectProposalActionPlan projectProposal)
            originalPlan = TownSystem.BindTownProjectName(projectProposal, proposalText);
        if (originalPlan is not null)
            TryRegisteredAction(ActionPhase.FreshAdmission, new(actor, agents[actor], candidate, false, originalPlan));
    }
}
