using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static readonly GoodsKinds PreparedMealGoods = new(FoodRules.PreparedKinds);
    private static readonly GoodsKinds EdibleFoodGoods = new(FoodRules.EdibleKinds);

    private long HoldingsTotal(IEnumerable<string> owners, GoodsKinds kinds)
    {
        var ids = owners.Distinct(StringComparer.Ordinal).ToArray();
        return ids.Length == 0 ? 0 : FindGoods(new(GoodsUse.Holdings, null, new(ids), kinds)).Total;
    }
}
