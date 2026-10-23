using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command ReserveProductionInputs(IInventoryProductionWorld commandContext, RecipeProjectActionPlan plan, string purpose, long expiry, IReadOnlyList<string> ids, Action<IReadOnlyList<string>> recordids) => new PreparedCommand(inventory => RecordPreparedOutputs(() => commandContext.ReserveRecipeInputs(inventory, plan, purpose, expiry, out ids), () => { recordids(ids); }));

    internal static Command ReleaseProductionInputs(IReadOnlyList<Identity> ids, string reason) => new PreparedCommand(inventory =>
            {
                foreach (var id in ids)
                    if (inventory.GetReservation(id).State is InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed)
                        inventory = InventoryAuthority.ReleaseReservation(inventory, id, reason);
                return inventory;
            });
}
