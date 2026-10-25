using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static ActorCapabilityPolicy CivicVisitCapabilities =>
        new(ActionLifeStages.NonInfants, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true);
    internal static CivicVisitActionPlan? PlanCivicVisit(ITownWorld world, string actor, CivicVisitChoice choice) =>
        world.Agents.ContainsKey(actor) && world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is { } town &&
        CivicBoard(world, town) is { } board
            ? new(actor, choice, board, world.ReachCivicNoticePlace(actor, board)) : null;

    internal ActionCheck CheckCivicVisit(ITownWorld world, CivicVisitActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var actor = world.CapabilitiesFor(plan.Actor);
        var visitPolicy = policy ?? CivicVisitCapabilities;
        var capability = ActorCapabilities.Check(actor, plan.CandidateId, visitPolicy with
        { AllowUrgentFood = visitPolicy.AllowUrgentFood && actor is not null && LifeStageRules.IsAdult(actor.Age) });
        if (!capability.Allowed) return capability;
        var town = world.Towns.SingleOrDefault(item => item.Id == plan.Choice.TownId);
        return town?.Council is not null && CivicBoard(world, town) == plan.NoticePlace &&
            !world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, plan.NoticePlace, WorldRuntime.CivicSharedRules.ResourceInteractionRange) &&
            (TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) || MayVisitAsNewcomer(world, plan.Actor, town) ||
                MayVisitLandHearing(world, plan.Actor, town) || MayVisitLandTransfer(world, plan.Actor, town) || MayVisitNonLandCase(world, plan.Actor, town))
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected notice place or reason for visiting has changed");
    }

    internal void AddTownCivicCandidates(ITownWorld world, List<CognitionCandidate> candidates, string actor,
        IReadOnlyList<string>? buildingsWithoutSites = null)
    {
        var first = candidates.Count;
        AddTownCivicCandidateOptions(world, candidates, actor, buildingsWithoutSites);
        AttachCivicVisitPlans(world, candidates, actor, first);
    }

    private void AttachCivicVisitPlans(ITownWorld world, List<CognitionCandidate> candidates, string actor, int first)
    {
        for (var index = candidates.Count - 1; index >= first; index--)
        {
            var candidate = candidates[index];
            var plan = PlanCivicCandidate(world, actor, candidate.Id, candidate.ActionPlan, out var recognized);
            if (!recognized) continue;
            if (CheckCivicCandidatePlan(world, plan)?.Allowed == true) candidates[index] = candidate with { ActionPlan = plan };
            else candidates.RemoveAt(index);
        }
    }

    private ActionPlan? PlanCivicCandidate(ITownWorld world, string actor, string candidate,
        ActionPlan? originalPlan, out bool recognized)
    {
        recognized = true;
        if (TownProjectProposalChoice.Decode(candidate) is { } projectChoice)
            return originalPlan as TownProjectProposalActionPlan ?? PlanTownProjectProposal(world, actor, projectChoice);
        if (NonLandAgreementChoice.Decode(candidate) is { } nonLandAgreementChoice)
            return originalPlan as NonLandAgreementActionPlan ?? PlanNonLandAgreement(world, actor, nonLandAgreementChoice);
        if (NonLandAdjudicationChoice.Decode(candidate) is { } nonLandAdjudicationChoice)
            return originalPlan as NonLandAdjudicationActionPlan ?? PlanNonLandAdjudication(world, actor, nonLandAdjudicationChoice);
        if (NonLandJudgeChoice.Decode(candidate) is { } nonLandJudgeChoice)
            return originalPlan as NonLandJudgeActionPlan ?? PlanNonLandJudge(world, actor, nonLandJudgeChoice);
        if (NonLandEvidenceChoice.Decode(candidate) is { } nonLandEvidenceChoice)
            return originalPlan as NonLandEvidenceActionPlan ?? PlanNonLandEvidence(world, actor, nonLandEvidenceChoice);
        if (NonLandFilingChoice.Decode(candidate) is { } nonLandFilingChoice)
            return originalPlan as NonLandFilingActionPlan ?? PlanNonLandFiling(world, actor, nonLandFilingChoice);
        if (LandHearingPropertyChoice.Decode(candidate) is { } hearingPropertyChoice)
            return originalPlan as LandHearingPropertyActionPlan ?? PlanLandHearingProperty(world, actor, hearingPropertyChoice);
        if (LandHearingReopeningChoice.Decode(candidate) is { } hearingReopeningChoice)
            return originalPlan as LandHearingReopeningActionPlan ?? PlanLandHearingReopening(world, actor, hearingReopeningChoice);
        if (LandHearingRulingChoice.Decode(candidate) is { } hearingRulingChoice)
            return originalPlan as LandHearingRulingActionPlan ?? PlanLandHearingRuling(world, actor, hearingRulingChoice);
        if (LandHearingJudgeChoice.Decode(candidate) is { } hearingJudgeChoice)
            return originalPlan as LandHearingJudgeActionPlan ?? PlanLandHearingJudge(world, actor, hearingJudgeChoice);
        if (LandHearingEvidenceChoice.Decode(candidate) is { } hearingEvidenceChoice)
            return originalPlan as LandHearingEvidenceActionPlan ?? PlanLandHearingEvidence(world, actor, hearingEvidenceChoice);
        if (LandHearingFilingChoice.Decode(candidate) is { } hearingFilingChoice)
            return originalPlan as LandHearingFilingActionPlan ?? PlanLandHearingFiling(world, actor, hearingFilingChoice);
        if (LandSaleExchangeChoice.Decode(candidate) is { } exchangeChoice)
            return originalPlan as LandSaleExchangeActionPlan ?? PlanLandSaleExchange(world, actor, exchangeChoice);
        if (LandSaleCollectionChoice.Decode(candidate) is { } collectionChoice)
            return originalPlan as LandSaleCollectionActionPlan ?? PlanLandSaleCollection(world, actor, collectionChoice);
        if (LandTransferResponseChoice.Decode(candidate) is { } transferResponseChoice)
            return originalPlan as LandTransferResponseActionPlan ?? PlanLandTransferResponse(world, actor, transferResponseChoice);
        if (LandTransferProposalChoice.Decode(candidate) is { } transferChoice)
            return originalPlan as LandTransferProposalActionPlan ?? PlanLandTransferProposal(world, actor, transferChoice);
        if (HouseholdLandFilingChoice.Decode(candidate) is { } filingChoice)
            return originalPlan as HouseholdLandFilingActionPlan ?? PlanHouseholdLandFiling(world, actor, filingChoice);
        if (HouseholdLandResponseChoice.Decode(candidate) is { } landResponseChoice)
            return originalPlan as HouseholdLandResponseActionPlan ?? PlanHouseholdLandResponse(world, actor, landResponseChoice);
        if (TownLandClaimChoice.Decode(candidate) is { } claimChoice)
            return originalPlan as TownLandClaimActionPlan ?? PlanTownLandClaim(world, actor, claimChoice);
        if (MayoralBallotChoice.Decode(candidate) is { } mayorChoice)
            return originalPlan as MayoralBallotActionPlan ?? PlanMayoralBallot(world, actor, mayorChoice);
        if (GovernmentOfficeChoice.Decode(candidate) is { } officeChoice)
            return originalPlan as GovernmentOfficeActionPlan ?? PlanGovernmentOffice(world, actor, officeChoice);
        if (GovernmentProposalChoice.Decode(candidate) is { } governmentChoice)
            return originalPlan as GovernmentProposalActionPlan ?? PlanGovernmentProposal(world, actor, governmentChoice);
        if (TownPolicyChoice.Decode(candidate) is { } policy)
            return originalPlan as TownPolicyActionPlan ?? PlanTownPolicy(world, actor, policy);
        if (TownLawChoice.Decode(candidate) is { } law)
            return originalPlan as TownLawActionPlan ?? PlanTownLaw(world, actor, law);
        if (CivicProposalVoteChoice.Decode(candidate) is { } response)
            return originalPlan as CivicProposalVoteActionPlan ?? PlanCivicProposalVote(world, actor, response);
        if (CivicNoticeChoice.Decode(candidate) is { } notice)
            return originalPlan as CivicNoticeActionPlan ?? PlanCivicNotice(world, actor, notice);
        if (TownFoundingChoice.Decode(candidate) is { } founding)
            return originalPlan as TownFoundingActionPlan ?? PlanTownFounding(world, actor, founding);
        if (TownAdmissionChoice.Decode(candidate) is { } admission)
            return originalPlan as TownAdmissionActionPlan ?? PlanTownAdmission(world, actor, admission);
        if (CouncilBallotChoice.Decode(candidate) is { } ballot)
            return originalPlan as CouncilBallotActionPlan ?? PlanCouncilBallot(world, actor, ballot);
        if (CouncilConsentChoice.Decode(candidate) is { } consent)
            return originalPlan as CouncilConsentActionPlan ?? PlanCouncilConsent(world, actor, consent);
        if (CivicVisitChoice.Decode(candidate) is { } choice)
            return originalPlan as CivicVisitActionPlan ?? PlanCivicVisit(world, actor, choice);
        recognized = false;
        return null;
    }

    private ActionCheck? CheckCivicCandidatePlan(ITownWorld world, ActionPlan? plan) => plan switch
    {
        TownProjectProposalActionPlan typed => CheckTownProjectProposal(world, typed),
        NonLandAgreementActionPlan typed => CheckNonLandAgreement(world, typed),
        NonLandAdjudicationActionPlan typed => CheckNonLandAdjudication(world, typed),
        NonLandJudgeActionPlan typed => CheckNonLandJudge(world, typed),
        NonLandEvidenceActionPlan typed => CheckNonLandEvidence(world, typed),
        NonLandFilingActionPlan typed => CheckNonLandFiling(world, typed),
        LandHearingPropertyActionPlan typed => CheckLandHearingProperty(world, typed),
        LandHearingReopeningActionPlan typed => CheckLandHearingReopening(world, typed),
        LandHearingRulingActionPlan typed => CheckLandHearingRuling(world, typed),
        LandHearingJudgeActionPlan typed => CheckLandHearingJudge(world, typed),
        LandHearingEvidenceActionPlan typed => CheckLandHearingEvidence(world, typed),
        LandHearingFilingActionPlan typed => CheckLandHearingFiling(world, typed),
        LandSaleExchangeActionPlan typed => CheckLandSaleExchange(world, typed),
        LandSaleCollectionActionPlan typed => CheckLandSaleCollection(world, typed),
        LandTransferResponseActionPlan typed => CheckLandTransferResponse(world, typed),
        LandTransferProposalActionPlan typed => CheckLandTransferProposal(world, typed),
        HouseholdLandFilingActionPlan typed => CheckHouseholdLandFiling(world, typed),
        HouseholdLandResponseActionPlan typed => CheckHouseholdLandResponse(world, typed),
        TownLandClaimActionPlan typed => CheckTownLandClaim(world, typed),
        MayoralBallotActionPlan typed => CheckMayoralBallot(world, typed),
        GovernmentOfficeActionPlan typed => CheckGovernmentOffice(world, typed),
        GovernmentProposalActionPlan typed => CheckGovernmentProposal(world, typed),
        TownPolicyActionPlan typed => CheckTownPolicyPlan(world, typed),
        TownLawActionPlan typed => CheckTownLawPlan(world, typed),
        CivicProposalVoteActionPlan typed => CheckCivicProposalVote(world, typed),
        CivicNoticeActionPlan typed => CheckCivicNoticePlan(world, typed),
        TownFoundingActionPlan typed => CheckTownFoundingPlan(world, typed),
        TownAdmissionActionPlan typed => CheckTownAdmissionPlan(world, typed),
        CouncilBallotActionPlan typed => CheckCouncilBallot(world, typed),
        CouncilConsentActionPlan typed => CheckCouncilConsent(world, typed),
        CivicVisitActionPlan typed => CheckCivicVisit(world, typed),
        _ => null,
    };
}
