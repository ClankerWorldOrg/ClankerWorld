using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command ReserveProductionInputs(IInventoryProductionWorld commandContext, RecipeProjectActionPlan plan, string purpose, long expiry, IReadOnlyList<string> ids, Action<IReadOnlyList<string>> recordIds) => new PreparedCommand(inventory => RecordPreparedOutputs(() => commandContext.ReserveRecipeInputs(inventory, plan, purpose, expiry, out ids), () =>
{
    recordIds(ids);
}));

    internal static Command ReleaseProductionInputs(IReadOnlyList<string> ids, string reason) => new PreparedCommand(inventory =>
{
    foreach (var id in ids)
        if (inventory.GetReservation(id).State is InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed)
            inventory = InventoryAuthority.ReleaseReservation(inventory, id, reason);
    return inventory;
});
}
