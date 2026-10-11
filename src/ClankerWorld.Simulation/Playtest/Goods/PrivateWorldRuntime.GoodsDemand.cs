namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private static readonly GoodsKinds PreparedMealGoods = new([
        "simple_meal", "porridge", "berry_porridge", "fruit_porridge", "bread", "stew",
        "restaurant_meal", "cooked_eggs", "milk_porridge", "rich_meal",
    ]);
    private static readonly GoodsKinds EdibleFoodGoods = new([
        .. PreparedMealGoods.Ids, "food", "fruit", "berries", "wild_greens", "cultivated_greens", "milk",
    ]);

    private long HoldingsTotal(IEnumerable<string> owners, GoodsKinds kinds)
    {
        var ids = owners.Distinct(StringComparer.Ordinal).ToArray();
        return ids.Length == 0 ? 0 : FindGoods(new(GoodsUse.Holdings, null, new(ids), kinds)).Total;
    }
}
