
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    public ActionCheck CheckActionPlan(ActionPlan plan) => DefinitionFor(plan) is { } definition
        ? definition.Check(this, plan) : throw new InvalidOperationException("An action plan has no registered check.");

    private void RefuseChoice(ActionPlan plan, ActionRefusal refusal)
    {
        var name = society.Checkpoint.Agents.SingleOrDefault(person => person.Id == plan.Actor)?.Name ?? "Someone";
        if (agents.ContainsKey(plan.Actor)) society.ClearCurrentIntention(plan.Actor, WorldTick);
        AppendEvent(new ChoiceRefusedEventPayload(plan.Actor, name, plan.Activity, refusal.Explanation));
    }

    private Society.SocietyCognitionDispatchResult CheckAcceptedPlan(Society.SocietyCognitionDispatchResult decision)
    {
        if (decision.ActionPlan is TownProjectProposalActionPlan projectProposal)
            decision = decision with { ActionPlan = TownSystem.BindTownProjectName(projectProposal, decision.Admission.CivicProposal) };
        if (decision.ActionPlan is NonLandAgreementActionPlan nonLandAgreement)
            decision = decision with { ActionPlan = TownSystem.BindNonLandAgreement(nonLandAgreement, decision.Admission.CivicNonLandHearing, TownSystem.CivicDay(civicWorld)) };
        if (decision.ActionPlan is NonLandAdjudicationActionPlan nonLandAdjudication)
            decision = decision with { ActionPlan = TownSystem.BindNonLandAdjudication(nonLandAdjudication, decision.Admission.CivicNonLandHearing) };
        if (decision.ActionPlan is NonLandEvidenceActionPlan nonLandEvidence)
            decision = decision with { ActionPlan = TownSystem.BindNonLandEvidence(nonLandEvidence, decision.Admission.CivicNonLandHearing) };
        if (decision.ActionPlan is NonLandFilingActionPlan nonLandFiling)
            decision = decision with { ActionPlan = TownSystem.BindNonLandFiling(nonLandFiling, decision.Admission.CivicNonLandHearing) };
        if (decision.ActionPlan is LandHearingPropertyActionPlan hearingProperty)
            decision = decision with { ActionPlan = TownSystem.BindLandHearingProperty(hearingProperty, decision.Admission.CivicLandHearing, decision.Admission.CivicProposal) };
        if (decision.ActionPlan is LandHearingReopeningActionPlan hearingReopening)
            decision = decision with { ActionPlan = TownSystem.BindLandHearingReopening(hearingReopening, decision.Admission.CivicLandHearing) };
        if (decision.ActionPlan is LandHearingRulingActionPlan hearingRuling)
            decision = decision with { ActionPlan = TownSystem.BindLandHearingRuling(hearingRuling, decision.Admission.CivicLandHearing, decision.Admission.CivicProposal) };
        if (decision.ActionPlan is LandHearingEvidenceActionPlan hearingEvidence)
            decision = decision with { ActionPlan = TownSystem.BindLandHearingEvidence(hearingEvidence, decision.Admission.CivicLandHearing, decision.Admission.CivicProposal) };
        if (decision.ActionPlan is LandHearingFilingActionPlan hearingFiling)
            decision = decision with
            {
                ActionPlan = TownSystem.BindLandHearingFiling(hearingFiling, decision.Admission.CivicLandTiles,
                decision.Admission.CivicLandHearing, decision.Admission.CivicProposal)
            };
        if (decision.ActionPlan is LandTransferProposalActionPlan transfer)
            decision = decision with { ActionPlan = TownSystem.BindLandTransferProposal(transfer, decision.Admission.CivicLandTiles, decision.Admission.CivicLandHearing) };
        if (decision.ActionPlan is HouseholdLandFilingActionPlan filing)
            decision = decision with { ActionPlan = TownSystem.BindHouseholdLandFiling(filing, decision.Admission.CivicLandTiles) };
        if (decision.ActionPlan is TownLandClaimActionPlan claim)
            decision = decision with { ActionPlan = TownSystem.BindTownLandClaim(claim, decision.Admission.CivicLandTiles) };
        if (decision.ActionPlan is TownLawActionPlan law)
            decision = decision with { ActionPlan = TownSystem.BindTownLawText(law, decision.Admission.CivicProposal) };
        if (decision.ActionPlan is CouncilBallotActionPlan ballot)
            decision = decision with { ActionPlan = TownSystem.BindCouncilBallot(civicWorld, ballot, decision.Admission.CivicBallot) };
        if (decision.Admission.Accepted && decision.ActionPlan is { } plan &&
            CheckActionPlan(plan).Refusal is { } refusal)
            return decision with { PlanRefusal = refusal };
        return decision;
    }
}
