using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Server.Observation;

public sealed partial class OwnerWorldObservationStore
{
    private static ProductionRecipeView ProjectProductionRecipe(RecipeDefinition recipe) => new(
        recipe.CanonicalId, recipe.DisplayName,
        recipe.Inputs.Select(input => new MaterialQuantityView(input.ResourceId, input.Amount)).ToArray(),
        recipe.Outputs.Select(output => new MaterialQuantityView(output.ResourceId, output.Amount)).ToArray());
}
