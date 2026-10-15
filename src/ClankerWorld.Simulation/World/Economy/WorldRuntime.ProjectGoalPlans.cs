namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy ProjectGoalCapabilities =>
        new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private RecipeActionPlan? PlanRecipeStart(string actor, RecipeChoice choice)
    {
        if (worldContent.Recipes.FirstOrDefault(recipe => recipe.CanonicalId == choice.DefinitionId) is not { } recipe) return null;
        return TryFindRecipeSite(recipe, out var site, out _, actor)
            ? PlanRecipeProject(actor, recipe, worldSimulation.Buildings.Single(building => building.InstanceId == site))
            : new RecipeGoalActionPlan(actor, recipe);
    }

    private ConstructionProjectActionPlan? PlanConstructionStart(string actor, ConstructionChoice choice)
    {
        if (worldContent.Buildings.FirstOrDefault(definition => definition.CanonicalId == choice.DefinitionId) is not { } definition) return null;
        return choice.Site is { } site ? PlanConstruction(actor, definition, site) : new ConstructionGoalActionPlan(actor, definition);
    }

    private ActionCheck CheckRecipeGoal(RecipeGoalActionPlan plan, ActorCapabilityPolicy policy)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy);
        if (!capability.Allowed) return capability;
        return worldContent.Recipes.Any(recipe => recipe.CanonicalId == plan.Recipe.CanonicalId) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected recipe goal is no longer available");
    }

    private ActionCheck CheckConstructionGoal(ConstructionGoalActionPlan plan, ActorCapabilityPolicy policy)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy);
        if (!capability.Allowed) return capability;
        return worldContent.Buildings.Any(definition => definition.CanonicalId == plan.Definition.CanonicalId) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected building goal is no longer available");
    }
}
