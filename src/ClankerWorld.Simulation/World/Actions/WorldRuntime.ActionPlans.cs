using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    public ActionCheck CheckActionPlan(ActionPlan plan) => plan switch
    {
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
        AppendEvent("choice_refused", $"{name} didn't {plan.Activity}: {refusal.Explanation}.");
    }

    private Society.SocietyCognitionDispatchResult CheckAcceptedPlan(Society.SocietyCognitionDispatchResult decision)
    {
        if (decision.Admission.Accepted && decision.ActionPlan is { } plan &&
            CheckActionPlan(plan).Refusal is { } refusal)
            return decision with { PlanRefusal = refusal };
        return decision;
    }
}
