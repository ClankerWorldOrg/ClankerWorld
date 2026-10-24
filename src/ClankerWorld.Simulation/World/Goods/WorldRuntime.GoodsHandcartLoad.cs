using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IEnumerable<InventoryLot> HandcartLoadStock(string actor)
    {
        var owners = HouseholdFor(actor) is { } home ? new[] { actor, home } : [actor];
        var kinds = owners.SelectMany(owner => InventoryIndex.For(society.Checkpoint.Inventory).OwnedBy(owner))
            .Select(lot => lot.ItemKind).Distinct(StringComparer.Ordinal).ToArray();
        return kinds.Length == 0 ? [] : FindGoods(new(GoodsUse.ConsumeAt, actor, new(owners), new(kinds))).Matches
            .Where(match => TransportSystem.CanLoadCartLot(HandcartWorld, actor, agents[actor], match.Lot)).Select(match => match.Lot);
    }
}
