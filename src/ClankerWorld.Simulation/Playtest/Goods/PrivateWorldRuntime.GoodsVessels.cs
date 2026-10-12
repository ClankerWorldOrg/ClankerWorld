using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private GoodsRequest? CartCargoRequest(string actor, string cartId)
    {
        var kinds = InventoryIndex.For(society.Checkpoint.Inventory).ContentsOf(cartId)
            .Select(lot => lot.ItemKind).Distinct(StringComparer.Ordinal).ToArray();
        return kinds.Length == 0 ? null : new(GoodsUse.Cargo, actor, GoodsOwners.One(actor), new(kinds), ContainerId: cartId);
    }

    private IEnumerable<InventoryLot> CartCargoLots(string actor, string cartId) =>
        CartCargoRequest(actor, cartId) is { } request ? FindGoods(request).Matches.Select(match => match.Lot) : [];

    private InventoryLot? RecheckCartCargo(string actor, string cartId, string lotId) =>
        CartCargoRequest(actor, cartId) is { } request ? RecheckGoods(request, lotId)?.Lot : null;
}
