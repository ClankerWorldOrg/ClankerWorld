namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy ProjectMaterialCapabilities =>
        new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private static class ProjectMaterialActions
    {
        internal static readonly ActionDefinition<BuildingMaterialChoice, BuildingMaterialActionPlan> Building = new(
            DecodeReferenceChoice(BuildingMaterialChoice.Decode), static choice => choice.Encode(), ProjectMaterialCapabilities,
            static (world, actor, choice, _) => world.HouseholdFor(actor) is { } household && world.PlanBuildingMaterial(actor, household) is { } plan &&
                plan.Choice.ItemKind == choice.ItemKind ? plan : null,
            static (world, plan, policy) => world.CheckBuildingMaterial(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedBuildingMaterial(plan); return true; });

        internal static readonly ActionDefinition<ProjectAssistanceChoice, ProjectAssistanceActionPlan> Assistance = new(
            DecodeReferenceChoice(ProjectAssistanceChoice.Decode), static choice => choice.Encode(), ProjectMaterialCapabilities,
            static (world, actor, choice, _) => world.PlanRequestedAssistance(actor, choice.ItemKind),
            static (world, plan, policy) => world.CheckProjectAssistance(plan, policy),
            static (world, plan, action, _) =>
            {
                world.ApplyCheckedProjectAssistance(plan, action.State, plan.Choice.ItemKind);
                return true;
            });
    }

    private ProjectAssistanceActionPlan? PlanRequestedAssistance(string actor, string itemKind)
    {
        foreach (var candidate in ProjectRequests(actor).Where(request => request.Input.ResourceId == itemKind))
            if (PlanProjectAssistance(actor, candidate) is { } plan && CheckActionPlan(plan).Allowed) return plan;
        return null;
    }
}
