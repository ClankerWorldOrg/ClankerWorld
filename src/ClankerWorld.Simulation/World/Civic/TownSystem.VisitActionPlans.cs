using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static ActorCapabilityPolicy CivicMembershipCapabilities =>
        new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true);
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
            ActionPlan? plan;
            if (TownProjectProposalChoice.Decode(candidate.Id) is { } projectChoice)
                plan = candidate.ActionPlan as TownProjectProposalActionPlan ?? PlanTownProjectProposal(world, actor, projectChoice);
            else if (NonLandAgreementChoice.Decode(candidate.Id) is { } nonLandAgreementChoice)
                plan = candidate.ActionPlan as NonLandAgreementActionPlan ?? PlanNonLandAgreement(world, actor, nonLandAgreementChoice);
            else if (NonLandAdjudicationChoice.Decode(candidate.Id) is { } nonLandAdjudicationChoice)
                plan = candidate.ActionPlan as NonLandAdjudicationActionPlan ?? PlanNonLandAdjudication(world, actor, nonLandAdjudicationChoice);
            else if (NonLandJudgeChoice.Decode(candidate.Id) is { } nonLandJudgeChoice)
                plan = candidate.ActionPlan as NonLandJudgeActionPlan ?? PlanNonLandJudge(world, actor, nonLandJudgeChoice);
            else if (NonLandEvidenceChoice.Decode(candidate.Id) is { } nonLandEvidenceChoice)
                plan = candidate.ActionPlan as NonLandEvidenceActionPlan ?? PlanNonLandEvidence(world, actor, nonLandEvidenceChoice);
            else if (NonLandFilingChoice.Decode(candidate.Id) is { } nonLandFilingChoice)
                plan = candidate.ActionPlan as NonLandFilingActionPlan ?? PlanNonLandFiling(world, actor, nonLandFilingChoice);
            else if (LandHearingPropertyChoice.Decode(candidate.Id) is { } hearingPropertyChoice)
                plan = candidate.ActionPlan as LandHearingPropertyActionPlan ?? PlanLandHearingProperty(world, actor, hearingPropertyChoice);
            else if (LandHearingReopeningChoice.Decode(candidate.Id) is { } hearingReopeningChoice)
                plan = candidate.ActionPlan as LandHearingReopeningActionPlan ?? PlanLandHearingReopening(world, actor, hearingReopeningChoice);
            else if (LandHearingRulingChoice.Decode(candidate.Id) is { } hearingRulingChoice)
                plan = candidate.ActionPlan as LandHearingRulingActionPlan ?? PlanLandHearingRuling(world, actor, hearingRulingChoice);
            else if (LandHearingJudgeChoice.Decode(candidate.Id) is { } hearingJudgeChoice)
                plan = candidate.ActionPlan as LandHearingJudgeActionPlan ?? PlanLandHearingJudge(world, actor, hearingJudgeChoice);
            else if (LandHearingEvidenceChoice.Decode(candidate.Id) is { } hearingEvidenceChoice)
                plan = candidate.ActionPlan as LandHearingEvidenceActionPlan ?? PlanLandHearingEvidence(world, actor, hearingEvidenceChoice);
            else if (LandHearingFilingChoice.Decode(candidate.Id) is { } hearingFilingChoice)
                plan = candidate.ActionPlan as LandHearingFilingActionPlan ?? PlanLandHearingFiling(world, actor, hearingFilingChoice);
            else if (LandSaleExchangeChoice.Decode(candidate.Id) is { } exchangeChoice)
                plan = candidate.ActionPlan as LandSaleExchangeActionPlan ?? PlanLandSaleExchange(world, actor, exchangeChoice);
            else if (LandSaleCollectionChoice.Decode(candidate.Id) is { } collectionChoice)
                plan = candidate.ActionPlan as LandSaleCollectionActionPlan ?? PlanLandSaleCollection(world, actor, collectionChoice);
            else if (LandTransferResponseChoice.Decode(candidate.Id) is { } transferResponseChoice)
                plan = candidate.ActionPlan as LandTransferResponseActionPlan ?? PlanLandTransferResponse(world, actor, transferResponseChoice);
            else if (LandTransferProposalChoice.Decode(candidate.Id) is { } transferChoice)
                plan = candidate.ActionPlan as LandTransferProposalActionPlan ?? PlanLandTransferProposal(world, actor, transferChoice);
            else if (HouseholdLandFilingChoice.Decode(candidate.Id) is { } filingChoice)
                plan = candidate.ActionPlan as HouseholdLandFilingActionPlan ?? PlanHouseholdLandFiling(world, actor, filingChoice);
            else if (HouseholdLandResponseChoice.Decode(candidate.Id) is { } landResponseChoice)
                plan = candidate.ActionPlan as HouseholdLandResponseActionPlan ?? PlanHouseholdLandResponse(world, actor, landResponseChoice);
            else if (TownLandClaimChoice.Decode(candidate.Id) is { } claimChoice)
                plan = candidate.ActionPlan as TownLandClaimActionPlan ?? PlanTownLandClaim(world, actor, claimChoice);
            else if (MayoralBallotChoice.Decode(candidate.Id) is { } mayorChoice)
                plan = candidate.ActionPlan as MayoralBallotActionPlan ?? PlanMayoralBallot(world, actor, mayorChoice);
            else if (GovernmentOfficeChoice.Decode(candidate.Id) is { } officeChoice)
                plan = candidate.ActionPlan as GovernmentOfficeActionPlan ?? PlanGovernmentOffice(world, actor, officeChoice);
            else if (GovernmentProposalChoice.Decode(candidate.Id) is { } governmentChoice)
                plan = candidate.ActionPlan as GovernmentProposalActionPlan ?? PlanGovernmentProposal(world, actor, governmentChoice);
            else if (TownPolicyChoice.Decode(candidate.Id) is { } policy)
                plan = candidate.ActionPlan as TownPolicyActionPlan ?? PlanTownPolicy(world, actor, policy);
            else if (TownLawChoice.Decode(candidate.Id) is { } law)
                plan = candidate.ActionPlan as TownLawActionPlan ?? PlanTownLaw(world, actor, law);
            else if (CivicProposalVoteChoice.Decode(candidate.Id) is { } response)
                plan = candidate.ActionPlan as CivicProposalVoteActionPlan ?? PlanCivicProposalVote(world, actor, response);
            else if (CivicNoticeChoice.Decode(candidate.Id) is { } notice)
                plan = candidate.ActionPlan as CivicNoticeActionPlan ?? PlanCivicNotice(world, actor, notice);
            else if (TownFoundingChoice.Decode(candidate.Id) is { } founding)
                plan = candidate.ActionPlan as TownFoundingActionPlan ?? PlanTownFounding(world, actor, founding);
            else if (TownAdmissionChoice.Decode(candidate.Id) is { } admission)
                plan = candidate.ActionPlan as TownAdmissionActionPlan ?? PlanTownAdmission(world, actor, admission);
            else if (CouncilBallotChoice.Decode(candidate.Id) is { } ballot)
                plan = candidate.ActionPlan as CouncilBallotActionPlan ?? PlanCouncilBallot(world, actor, ballot);
            else if (CouncilConsentChoice.Decode(candidate.Id) is { } consent)
                plan = candidate.ActionPlan as CouncilConsentActionPlan ?? PlanCouncilConsent(world, actor, consent);
            else if (CivicVisitChoice.Decode(candidate.Id) is { } choice)
                plan = candidate.ActionPlan as CivicVisitActionPlan ?? PlanCivicVisit(world, actor, choice);
            else continue;

            var allowed = plan switch
            {
                TownProjectProposalActionPlan typed => CheckTownProjectProposal(world, typed).Allowed,
                NonLandAgreementActionPlan typed => CheckNonLandAgreement(world, typed).Allowed,
                NonLandAdjudicationActionPlan typed => CheckNonLandAdjudication(world, typed).Allowed,
                NonLandJudgeActionPlan typed => CheckNonLandJudge(world, typed).Allowed,
                NonLandEvidenceActionPlan typed => CheckNonLandEvidence(world, typed).Allowed,
                NonLandFilingActionPlan typed => CheckNonLandFiling(world, typed).Allowed,
                LandHearingPropertyActionPlan typed => CheckLandHearingProperty(world, typed).Allowed,
                LandHearingReopeningActionPlan typed => CheckLandHearingReopening(world, typed).Allowed,
                LandHearingRulingActionPlan typed => CheckLandHearingRuling(world, typed).Allowed,
                LandHearingJudgeActionPlan typed => CheckLandHearingJudge(world, typed).Allowed,
                LandHearingEvidenceActionPlan typed => CheckLandHearingEvidence(world, typed).Allowed,
                LandHearingFilingActionPlan typed => CheckLandHearingFiling(world, typed).Allowed,
                LandSaleExchangeActionPlan typed => CheckLandSaleExchange(world, typed).Allowed,
                LandSaleCollectionActionPlan typed => CheckLandSaleCollection(world, typed).Allowed,
                LandTransferResponseActionPlan typed => CheckLandTransferResponse(world, typed).Allowed,
                LandTransferProposalActionPlan typed => CheckLandTransferProposal(world, typed).Allowed,
                HouseholdLandFilingActionPlan typed => CheckHouseholdLandFiling(world, typed).Allowed,
                HouseholdLandResponseActionPlan typed => CheckHouseholdLandResponse(world, typed).Allowed,
                TownLandClaimActionPlan typed => CheckTownLandClaim(world, typed).Allowed,
                MayoralBallotActionPlan typed => CheckMayoralBallot(world, typed).Allowed,
                GovernmentOfficeActionPlan typed => CheckGovernmentOffice(world, typed).Allowed,
                GovernmentProposalActionPlan typed => CheckGovernmentProposal(world, typed).Allowed,
                TownPolicyActionPlan typed => CheckTownPolicyPlan(world, typed).Allowed,
                TownLawActionPlan typed => CheckTownLawPlan(world, typed).Allowed,
                CivicProposalVoteActionPlan typed => CheckCivicProposalVote(world, typed).Allowed,
                CivicNoticeActionPlan typed => CheckCivicNoticePlan(world, typed).Allowed,
                TownFoundingActionPlan typed => CheckTownFoundingPlan(world, typed).Allowed,
                TownAdmissionActionPlan typed => CheckTownAdmissionPlan(world, typed).Allowed,
                CouncilBallotActionPlan typed => CheckCouncilBallot(world, typed).Allowed,
                CouncilConsentActionPlan typed => CheckCouncilConsent(world, typed).Allowed,
                CivicVisitActionPlan typed => CheckCivicVisit(world, typed).Allowed,
                _ => false,
            };
            if (allowed) candidates[index] = candidate with { ActionPlan = plan };
            else candidates.RemoveAt(index);
        }
    }
}
