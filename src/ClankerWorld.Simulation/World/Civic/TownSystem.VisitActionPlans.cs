using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static CivicVisitActionPlan? PlanCivicVisit(ITownWorld world, string actor, CivicVisitChoice choice) =>
        world.Agents.ContainsKey(actor) && world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is { } town &&
        CivicBoard(world, town) is { } board
            ? new(actor, choice, board, world.ReachCivicNoticePlace(actor, board)) : null;

    internal ActionCheck CheckCivicVisit(ITownWorld world, CivicVisitActionPlan plan)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true));
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
        for (var index = candidates.Count - 1; index >= first; index--)
        {
            var candidate = candidates[index];
            if (LandHearingFilingChoice.Decode(candidate.Id) is { } hearingFilingChoice)
            {
                if ((candidate.ActionPlan as LandHearingFilingActionPlan ?? PlanLandHearingFiling(world, actor, hearingFilingChoice)) is { } hearingFiling &&
                    CheckLandHearingFiling(world, hearingFiling).Allowed)
                    candidates[index] = candidate with { ActionPlan = hearingFiling };
                else candidates.RemoveAt(index);
                continue;
            }
            if (LandSaleExchangeChoice.Decode(candidate.Id) is { } exchangeChoice)
            {
                if ((candidate.ActionPlan as LandSaleExchangeActionPlan ?? PlanLandSaleExchange(world, actor, exchangeChoice)) is { } exchange &&
                    CheckLandSaleExchange(world, exchange).Allowed)
                    candidates[index] = candidate with { ActionPlan = exchange };
                else candidates.RemoveAt(index);
                continue;
            }
            if (LandSaleCollectionChoice.Decode(candidate.Id) is { } collectionChoice)
            {
                if ((candidate.ActionPlan as LandSaleCollectionActionPlan ?? PlanLandSaleCollection(world, actor, collectionChoice)) is { } collection &&
                    CheckLandSaleCollection(world, collection).Allowed)
                    candidates[index] = candidate with { ActionPlan = collection };
                else candidates.RemoveAt(index);
                continue;
            }
            if (LandTransferResponseChoice.Decode(candidate.Id) is { } transferResponseChoice)
            {
                if ((candidate.ActionPlan as LandTransferResponseActionPlan ?? PlanLandTransferResponse(world, actor, transferResponseChoice)) is { } transferResponse &&
                    CheckLandTransferResponse(world, transferResponse).Allowed)
                    candidates[index] = candidate with { ActionPlan = transferResponse };
                else candidates.RemoveAt(index);
                continue;
            }
            if (LandTransferProposalChoice.Decode(candidate.Id) is { } transferChoice)
            {
                if ((candidate.ActionPlan as LandTransferProposalActionPlan ?? PlanLandTransferProposal(world, actor, transferChoice)) is { } transfer &&
                    CheckLandTransferProposal(world, transfer).Allowed)
                    candidates[index] = candidate with { ActionPlan = transfer };
                else candidates.RemoveAt(index);
                continue;
            }
            if (HouseholdLandFilingChoice.Decode(candidate.Id) is { } filingChoice)
            {
                if ((candidate.ActionPlan as HouseholdLandFilingActionPlan ?? PlanHouseholdLandFiling(world, actor, filingChoice)) is { } filing &&
                    CheckHouseholdLandFiling(world, filing).Allowed)
                    candidates[index] = candidate with { ActionPlan = filing };
                else candidates.RemoveAt(index);
                continue;
            }
            if (HouseholdLandResponseChoice.Decode(candidate.Id) is { } landResponseChoice)
            {
                if ((candidate.ActionPlan as HouseholdLandResponseActionPlan ?? PlanHouseholdLandResponse(world, actor, landResponseChoice)) is { } landResponse &&
                    CheckHouseholdLandResponse(world, landResponse).Allowed)
                    candidates[index] = candidate with { ActionPlan = landResponse };
                else candidates.RemoveAt(index);
                continue;
            }
            if (TownLandClaimChoice.Decode(candidate.Id) is { } claimChoice)
            {
                if ((candidate.ActionPlan as TownLandClaimActionPlan ?? PlanTownLandClaim(world, actor, claimChoice)) is { } claim &&
                    CheckTownLandClaim(world, claim).Allowed)
                    candidates[index] = candidate with { ActionPlan = claim };
                else candidates.RemoveAt(index);
                continue;
            }
            if (MayoralBallotChoice.Decode(candidate.Id) is { } mayorChoice)
            {
                if ((candidate.ActionPlan as MayoralBallotActionPlan ?? PlanMayoralBallot(world, actor, mayorChoice)) is { } mayoral &&
                    CheckMayoralBallot(world, mayoral).Allowed)
                    candidates[index] = candidate with { ActionPlan = mayoral };
                else candidates.RemoveAt(index);
                continue;
            }
            if (GovernmentOfficeChoice.Decode(candidate.Id) is { } officeChoice)
            {
                if ((candidate.ActionPlan as GovernmentOfficeActionPlan ?? PlanGovernmentOffice(world, actor, officeChoice)) is { } office &&
                    CheckGovernmentOffice(world, office).Allowed)
                    candidates[index] = candidate with { ActionPlan = office };
                else candidates.RemoveAt(index);
                continue;
            }
            if (GovernmentProposalChoice.Decode(candidate.Id) is { } governmentChoice)
            {
                if ((candidate.ActionPlan as GovernmentProposalActionPlan ?? PlanGovernmentProposal(world, actor, governmentChoice)) is { } selectedGovernment &&
                    CheckGovernmentProposal(world, selectedGovernment).Allowed)
                    candidates[index] = candidate with { ActionPlan = selectedGovernment };
                else candidates.RemoveAt(index);
                continue;
            }
            if (TownPolicyChoice.Decode(candidate.Id) is { } policy)
            {
                if ((candidate.ActionPlan as TownPolicyActionPlan ?? PlanTownPolicy(world, actor, policy)) is { } selected &&
                    CheckTownPolicyPlan(world, selected).Allowed)
                    candidates[index] = candidate with { ActionPlan = selected };
                else candidates.RemoveAt(index);
                continue;
            }
            if (TownLawChoice.Decode(candidate.Id) is { } law)
            {
                if ((candidate.ActionPlan as TownLawActionPlan ?? PlanTownLaw(world, actor, law)) is { } changing &&
                    CheckTownLawPlan(world, changing).Allowed)
                    candidates[index] = candidate with { ActionPlan = changing };
                else candidates.RemoveAt(index);
                continue;
            }
            if (CivicProposalVoteChoice.Decode(candidate.Id) is { } response)
            {
                if ((candidate.ActionPlan as CivicProposalVoteActionPlan ?? PlanCivicProposalVote(world, actor, response)) is { } answering &&
                    CheckCivicProposalVote(world, answering).Allowed)
                    candidates[index] = candidate with { ActionPlan = answering };
                else candidates.RemoveAt(index);
                continue;
            }
            if (CivicNoticeChoice.Decode(candidate.Id) is { } notice)
            {
                if ((candidate.ActionPlan as CivicNoticeActionPlan ?? PlanCivicNotice(world, actor, notice)) is { } sharing &&
                    CheckCivicNoticePlan(world, sharing).Allowed)
                    candidates[index] = candidate with { ActionPlan = sharing };
                else candidates.RemoveAt(index);
                continue;
            }
            if (TownFoundingChoice.Decode(candidate.Id) is { } founding)
            {
                if ((candidate.ActionPlan as TownFoundingActionPlan ?? PlanTownFounding(world, actor, founding)) is { } settlement &&
                    CheckTownFoundingPlan(world, settlement).Allowed)
                    candidates[index] = candidate with { ActionPlan = settlement };
                else candidates.RemoveAt(index);
                continue;
            }
            if (TownAdmissionChoice.Decode(candidate.Id) is { } admission)
            {
                if ((candidate.ActionPlan as TownAdmissionActionPlan ?? PlanTownAdmission(world, actor, admission)) is { } joining &&
                    CheckTownAdmissionPlan(world, joining).Allowed)
                    candidates[index] = candidate with { ActionPlan = joining };
                else candidates.RemoveAt(index);
                continue;
            }
            if (CouncilBallotChoice.Decode(candidate.Id) is { } ballot)
            {
                if ((candidate.ActionPlan as CouncilBallotActionPlan ?? PlanCouncilBallot(world, actor, ballot)) is { } voting &&
                    CheckCouncilBallot(world, voting).Allowed)
                    candidates[index] = candidate with { ActionPlan = voting };
                else candidates.RemoveAt(index);
                continue;
            }
            if (CouncilConsentChoice.Decode(candidate.Id) is { } consent)
            {
                if ((candidate.ActionPlan as CouncilConsentActionPlan ?? PlanCouncilConsent(world, actor, consent)) is { } agreement &&
                    CheckCouncilConsent(world, agreement).Allowed)
                    candidates[index] = candidate with { ActionPlan = agreement };
                else candidates.RemoveAt(index);
                continue;
            }
            if (CivicVisitChoice.Decode(candidate.Id) is not { } choice) continue;
            if (PlanCivicVisit(world, actor, choice) is { } plan && CheckCivicVisit(world, plan).Allowed)
                candidates[index] = candidate with { ActionPlan = plan };
            else candidates.RemoveAt(index);
        }
    }
}
