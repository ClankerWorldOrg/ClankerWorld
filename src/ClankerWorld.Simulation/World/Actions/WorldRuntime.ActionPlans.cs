using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    public ActionCheck CheckActionPlan(ActionPlan plan) => plan switch
    {
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
        if (decision.Admission.Accepted && decision.ActionPlan is { } plan &&
            CheckActionPlan(plan).Refusal is { } refusal)
            return decision with { PlanRefusal = refusal };
        return decision;
    }
}
