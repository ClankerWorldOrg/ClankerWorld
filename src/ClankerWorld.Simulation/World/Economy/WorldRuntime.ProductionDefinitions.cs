namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<RecipeChoice, RecipeActionPlan> RecipeActionDefinition => ProductionActions.Recipe;

    private static class ProductionActions
    {
        internal static readonly ActionDefinition<RecipeChoice, RecipeActionPlan> Recipe = new(
            DecodeReferenceChoice(RecipeChoice.Decode), static choice => choice.Encode(),
            new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: true, AllowActiveOrder: true),
            static (world, actor, choice, _) => world.PlanRecipeStart(actor, choice),
            static (world, plan, policy) => plan switch
            {
                RecipeProjectActionPlan placed => world.CheckRecipeProject(placed, policy),
                RecipeGoalActionPlan goal => world.CheckRecipeGoal(goal, policy),
                _ => ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the recipe plan is not available"),
            },
            static (world, plan, action, _) =>
            {
                world.BeginProject(action.AgentId, action.State, action.CandidateId, originalPlan: plan);
                return true;
            },
            PlanCapabilities: static plan => plan is RecipeProjectActionPlan placed ? RecipeCapabilities(placed.Recipe) : ProjectGoalCapabilities);
    }
}
