using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IProductionWorld ProductionWorld => new ProductionWorldAdapter(this);

    private sealed class ProductionWorldAdapter(WorldRuntime runtime) : IProductionWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public IReadOnlyList<PlacedBuilding> Buildings => runtime.worldSimulation.Buildings;
        public void ReleaseProductionInputs(IReadOnlyList<string> ids, string reason) => runtime.ApplyInventoryTransition(inventory =>
        {
            foreach (var id in ids)
                if (inventory.GetReservation(id).State is InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed)
                    inventory = InventoryAuthority.ReleaseReservation(inventory, id, reason);
            return inventory;
        });
        public void CompleteRecipeInventory(WorldProductionJob job, RecipeDefinition recipe, ToolWorkPlan? knife, long tick) =>
            runtime.CompleteRecipeInventory(job, recipe, knife, tick);
        public void CreditProducedWork(string worker, RecipeDefinition recipe) =>
            runtime.CreditCompletedWork(worker, "crafting", runtime.SkillForRecipe(recipe));
        public void LearnProducedRecipe(WorldProductionJob job, RecipeDefinition recipe, long tick) => runtime.LearnProducedRecipe(job, recipe, tick);
        public void CreditProductionOrderJob(WorldProductionJob job, RecipeDefinition recipe) => runtime.CreditProductionOrderJob(job, recipe);
        public void AppendEvent(string kind, string detail, GridPoint? position = null) => runtime.AppendEvent(kind, detail, position);
    }
}
