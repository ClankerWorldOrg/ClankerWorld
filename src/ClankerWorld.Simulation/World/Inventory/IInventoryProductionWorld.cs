using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

internal interface IInventoryProductionWorld
{
    InventoryCheckpoint ReserveRecipeInputs(InventoryCheckpoint inventory, RecipeProjectActionPlan plan, string purpose, long expiryTick, out IReadOnlyList<string> reservationIds);
}
