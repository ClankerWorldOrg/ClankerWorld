using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    internal sealed record PersonalCollectionEffect(string MoveId, string ItemKind, int Quantity);

    private PersonalCollectionEffect? CollectPersonalGoods(string actor, string lotId, int maximumQuantity = int.MaxValue)
    {
        var plan = PlanPersonalGoods(actor, new(PersonalGoodsKind.Collect, lotId), maximumQuantity, PersonalGoodsMode.Native);
        return ApplyPersonalGoods(plan) is { } effect ? new(effect.MoveId, effect.ItemKind, effect.Quantity) : null;
    }
}
