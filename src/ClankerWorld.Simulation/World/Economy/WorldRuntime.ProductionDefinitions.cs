namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<RecipeChoice, RecipeProjectActionPlan> RecipeActionDefinition => ProductionActions.Recipe;

    private static class ProductionActions
    {
        internal static readonly ActionDefinition<RecipeChoice, RecipeProjectActionPlan> Recipe = new(
            DecodeReferenceChoice(RecipeChoice.Decode), static choice => choice.Encode(),
            new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: true, AllowActiveOrder: true),
            static (world, actor, choice, policy) => world.worldContent.Recipes.FirstOrDefault(recipe => recipe.CanonicalId == choice.DefinitionId) is { } recipe &&
                world.TryFindRecipeSite(recipe, out var site, out _, actor)
                ? world.PlanRecipeProject(actor, recipe, world.worldSimulation.Buildings.Single(building => building.InstanceId == site)) : null,
            static (world, plan, policy) => world.CheckRecipeProject(plan, policy),
            static (world, plan, action, _) =>
            {
                world.BeginProject(action.AgentId, action.State, action.CandidateId, originalPlan: plan);
                return true;
            },
            PlanCapabilities: static plan => RecipeCapabilities(plan.Recipe),
            NoPlanFallback: static (world, action) =>
            {
                world.BeginProject(action.AgentId, action.State, action.CandidateId, originalPlan: action.Plan);
                return true;
            });
    }
}
