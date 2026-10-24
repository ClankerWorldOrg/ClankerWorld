using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command ReserveExpansionInputs(IInventoryConstructionWorld commandContext, IReadOnlyList<ExpansionMaterialInput> inputs, string actor, string id, long expiry, IReadOnlyList<string> ids, Action<IReadOnlyList<string>> recordids) => new PreparedCommand(inventory => RecordPreparedOutputs(() => commandContext.ReserveExpansionInputs(inventory, inputs, actor, id, expiry, out ids), () => { recordids(ids); }));

    internal static Command ResolveExpansionInputs(IReadOnlyList<Identity> ids, bool completed) => new PreparedCommand(inventory =>
            {
                foreach (var id in ids)
                    if (completed) inventory = InventoryAuthority.ConsumeReservation(inventory, id);
                    else if (inventory.GetReservation(id).State is InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed)
                        inventory = InventoryAuthority.ReleaseReservation(inventory, id, "expansion_cancelled");
                return inventory;
            });

    internal static Command ConsumeConstructionInputs(IInventoryConstructionWorld commandContext, IReadOnlyList<ConstructionMaterialInput> inputs, string purpose, string owner) => new PreparedCommand(inventory => commandContext.ConsumeConstructionInputs(inventory, inputs, purpose, owner));
}
