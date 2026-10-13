using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static GoodsReason? HandcartControlExclusion(string actor, InventoryLot lot) =>
        lot.OwnerId != actor || lot.ItemKind != InventoryContainerRules.Handcart ? GoodsReason.Owner :
        lot.ContainerLotId is not null || lot.GroundPosition is null || lot.CarrierId is not null ? GoodsReason.Place : null;

    private IEnumerable<InventoryLot> HandcartControlStock(string actor) => FindGoods(new(GoodsUse.HandcartControl,
        actor, GoodsOwners.One(actor), GoodsKinds.One(InventoryContainerRules.Handcart))).Matches.Select(match => match.Lot);
}
