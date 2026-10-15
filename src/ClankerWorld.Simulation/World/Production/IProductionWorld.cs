using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IProductionWorld
{
    SocietyCheckpoint Society { get; }
    DeclarativeWorldContentState Content { get; }
    IReadOnlyList<PlacedBuilding> Buildings { get; }
    void ReleaseProductionInputs(IReadOnlyList<string> ids, string reason);
    void CompleteRecipeInventory(WorldProductionJob job, RecipeDefinition recipe, ToolWorkPlan? knife, long tick);
    void CreditProducedWork(string worker, RecipeDefinition recipe);
    void LearnProducedRecipe(WorldProductionJob job, RecipeDefinition recipe, long tick);
    void CreditProductionOrderJob(WorldProductionJob job, RecipeDefinition recipe);
    void AppendEvent(string kind, string detail, GridPoint? position = null);
}
