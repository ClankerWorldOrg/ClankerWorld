using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Server.Observation;

public sealed partial class OwnerWorldObservationStore
{
    private static OwnerWorldProductionRecipe ProjectProductionRecipe(RecipeDefinition recipe) => new(
        recipe.CanonicalId, recipe.DisplayName,
        recipe.Inputs.Select(input => new OwnerWorldMaterialQuantity(input.ResourceId, input.Amount)).ToArray(),
        recipe.Outputs.Select(output => new OwnerWorldMaterialQuantity(output.ResourceId, output.Amount)).ToArray());
}
