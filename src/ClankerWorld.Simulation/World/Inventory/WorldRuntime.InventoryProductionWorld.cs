using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static IInventoryProductionWorld InventoryProductionWorld => new InventoryProductionAdapter();
    private sealed class InventoryProductionAdapter : IInventoryProductionWorld
    {
        public InventoryCheckpoint ReserveRecipeInputs(InventoryCheckpoint inventory, RecipeProjectActionPlan plan, string purpose, long expiryTick, out IReadOnlyList<string> reservationIds) => WorldRuntime.ReserveRecipeInputs(inventory, plan, purpose, expiryTick, out reservationIds);
    }
}
