using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    public ActionCheck CheckActionPlan(ActionPlan plan) => plan switch
    {
        LandHearingEvidenceActionPlan hearingEvidence => townSystem.CheckLandHearingEvidence(civicWorld, hearingEvidence),
        LandHearingFilingActionPlan hearingFiling => townSystem.CheckLandHearingFiling(civicWorld, hearingFiling),
        LandSaleExchangeActionPlan landSaleExchange => townSystem.CheckLandSaleExchange(civicWorld, landSaleExchange),
        LandSaleCollectionActionPlan landSaleCollection => townSystem.CheckLandSaleCollection(civicWorld, landSaleCollection),
        LandTransferResponseActionPlan transferResponse => townSystem.CheckLandTransferResponse(civicWorld, transferResponse),
        LandTransferProposalActionPlan transferProposal => townSystem.CheckLandTransferProposal(civicWorld, transferProposal),
        HouseholdLandFilingActionPlan landFiling => townSystem.CheckHouseholdLandFiling(civicWorld, landFiling),
        HouseholdLandResponseActionPlan landResponse => TownSystem.CheckHouseholdLandResponse(civicWorld, landResponse),
        TownLandClaimActionPlan claim => TownSystem.CheckTownLandClaim(civicWorld, claim),
        MayoralBallotActionPlan mayoralBallot => TownSystem.CheckMayoralBallot(civicWorld, mayoralBallot),
        GovernmentOfficeActionPlan office => TownSystem.CheckGovernmentOffice(civicWorld, office),
        GovernmentProposalActionPlan governmentProposal => TownSystem.CheckGovernmentProposal(civicWorld, governmentProposal),
        TownPolicyActionPlan policy => TownSystem.CheckTownPolicyPlan(civicWorld, policy),
        TownLawActionPlan law => TownSystem.CheckTownLawPlan(civicWorld, law),
        CivicProposalVoteActionPlan proposalVote => TownSystem.CheckCivicProposalVote(civicWorld, proposalVote),
        CivicNoticeActionPlan civicNotice => TownSystem.CheckCivicNoticePlan(civicWorld, civicNotice),
        TownFoundingActionPlan founding => townSystem.CheckTownFoundingPlan(civicWorld, founding),
        TownAdmissionActionPlan admission => townSystem.CheckTownAdmissionPlan(civicWorld, admission),
        CouncilBallotActionPlan councilBallot => TownSystem.CheckCouncilBallot(civicWorld, councilBallot),
        CouncilConsentActionPlan councilConsent => TownSystem.CheckCouncilConsent(civicWorld, councilConsent),
        CivicVisitActionPlan civicVisit => townSystem.CheckCivicVisit(civicWorld, civicVisit),
        ToolRequestCollectionActionPlan toolCollection => CheckToolRequestCollectionPlan(toolCollection),
        ToolRequestWorkActionPlan toolWork => CheckToolRequestWorkPlan(toolWork),
        ToolRequestVisitActionPlan toolVisit => CheckToolRequestVisitPlan(toolVisit),
        ToolRequestConsentActionPlan toolConsent => CheckToolRequestConsentPlan(toolConsent),
        RelocationActionPlan relocation => CheckRelocationActionPlan(relocation),
        ReplacementCareActionPlan replacementCare => CheckReplacementCarePlan(replacementCare),
        HouseholdResumeActionPlan resume => CheckHouseholdResumePlan(resume),
        DepartureActionPlan departure => CheckDepartureActionPlan(departure),
        LearningActionPlan learning => CheckLearningActionPlan(learning),
        HousingActionPlan housing => CheckHousingActionPlan(housing),
        PartnershipActionPlan partnership => CheckPartnershipActionPlan(partnership),
        ParenthoodActionPlan parenthood => CheckParenthoodActionPlan(parenthood),
        GuardianTravelActionPlan guardianTravel => CheckGuardianTravelPlan(guardianTravel),
        PracticalCareActionPlan practicalCare => CheckPracticalCarePlan(practicalCare),
        DependentCareActionPlan dependentCare => CheckDependentCarePlan(dependentCare),
        ConversationActionPlan conversation => conversationSystem.CheckConversationPlan(conversationWorld, conversation),
        MedicalSupplyActionPlan medicalSupply => CheckMedicalSupplyPlan(medicalSupply),
        MedicalTreatmentActionPlan medicalTreatment => CheckMedicalTreatmentPlan(medicalTreatment),
        MedicalConsentActionPlan medicalConsent => CheckMedicalConsentPlan(medicalConsent),
        BoatTripActionPlan boatTrip => CheckBoatTripPlan(boatTrip),
        BoatWaitingActionPlan boatWaiting => CheckBoatWaitingPlan(boatWaiting),
        HandcartBuildMaterialActionPlan cartBuildMaterial => CheckHandcartBuildMaterialPlan(cartBuildMaterial),
        HandcartRepairInputActionPlan cartRepairInput => CheckHandcartRepairInputPlan(cartRepairInput),
        HandcartRepairActionPlan cartRepair => CheckHandcartRepairPlan(cartRepair),
        HandcartCargoActionPlan cartCargo => CheckHandcartCargoPlan(cartCargo),
        HandcartPullActionPlan cartPull => CheckHandcartPullPlan(cartPull),
        HandcartControlActionPlan cartControl => CheckHandcartControlPlan(cartControl),
        AnimalAgreementActionPlan agreement => animalSystem.CheckAnimalAgreementPlan(animalWorldAdapter, agreement),
        AnimalTravelActionPlan travel => animalSystem.CheckAnimalTravelPlan(animalWorldAdapter, travel),
        AnimalTripActionPlan trip => animalSystem.CheckAnimalTripPlan(animalWorldAdapter, trip),
        AnimalWorkActionPlan work => animalSystem.CheckAnimalWorkPlan(animalWorldAdapter, work),
        AnimalSupplyActionPlan supply => animalSystem.CheckAnimalSupplyPlan(animalWorldAdapter, supply),
        AnimalProductActionPlan animal => animalSystem.CheckAnimalProductPlan(animalWorldAdapter, animal),
        FoodActionPlan food => CheckFoodPlan(food),
        OrnamentActionPlan ornament => CheckOrnamentPlan(ornament),
        FireActionPlan fire => CheckFirePlan(fire),
        WarmthActionPlan warmth => CheckWarmthPlan(warmth),
        EquipmentRepairActionPlan equipmentRepair => CheckEquipmentRepairPlan(equipmentRepair),
        PrivateEquipmentActionPlan equipment => CheckPrivateEquipmentPlan(equipment),
        VesselActionPlan vessel => CheckVesselPlan(vessel),
        FieldActionPlan field => farmingSystem.CheckFieldPlan(farmingWorld, field),
        TreeActionPlan tree => FarmingSystem.CheckTreePlan(farmingWorld, tree),
        RareMiningActionPlan mining => CheckRareMiningPlan(mining),
        ToolRepairActionPlan repair => CheckToolRepairPlan(repair),
        ToolCollectionActionPlan tool => CheckToolCollectionPlan(tool),
        SmithGatherActionPlan gather => CheckSmithGatherPlan(gather),
        SmithDeliveryActionPlan smith => CheckSmithDeliveryPlan(smith),
        StoreStockActionPlan storeStock => CheckStoreStockPlan(storeStock),
        WorkstationSupplyActionPlan workstation => CheckWorkstationSupplyPlan(workstation),
        FarmStockActionPlan farmStock => CheckFarmStockAction(farmStock),
        FoodCargoActionPlan foodCargo => CheckFoodCargoPlan(foodCargo),
        ChildActionPlan child => CheckChildPlan(child),
        TownStockActionPlan town => CheckTownStockPlan(town),
        HouseholdStockActionPlan household => CheckHouseholdStockPlan(household),
        _ => throw new InvalidOperationException("An action plan has no registered check."),
    };

    private void RefuseChoice(ActionPlan plan, ActionRefusal refusal)
    {
        var name = society.Checkpoint.Agents.SingleOrDefault(person => person.Id == plan.Actor)?.Name ?? "Someone";
        if (agents.ContainsKey(plan.Actor)) society.ClearCurrentIntention(plan.Actor, WorldTick);
        AppendEvent(new ChoiceRefusedEventPayload(plan.Actor, name, plan.Activity, refusal.Explanation));
    }

    private Society.SocietyCognitionDispatchResult CheckAcceptedPlan(Society.SocietyCognitionDispatchResult decision)
    {
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
