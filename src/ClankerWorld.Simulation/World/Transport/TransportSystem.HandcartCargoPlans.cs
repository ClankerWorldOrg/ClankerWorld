using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    internal static HandcartCargoActionPlan? PlanHandcartCargo(IHandcartWorld world, string actor, HandcartCargoChoice choice,
        string? targetCartId = null, int maximumQuantity = int.MaxValue)
    {
        var inventory = world.Inventory;
        var stock = InventoryIndex.For(inventory).Find(choice.StockId);
        var cartId = targetCartId ?? (choice.Kind == HandcartCargoKind.Load
            ? NearbyOwnedHandcart(world, actor, world.Agents[actor].Position)?.Id : stock?.ContainerLotId);
        if (cartId is null || world.RecheckGoods(new(GoodsUse.HandcartControl, actor, GoodsOwners.One(actor),
                GoodsKinds.One(InventoryContainerRules.Handcart)), cartId) is not { Lot: var cart } || cart.GroundPosition is not { } point || stock is null)
            return null;
        var match = choice.Kind == HandcartCargoKind.Load
            ? world.RecheckGoods(new(GoodsUse.ConsumeAt, actor, GoodsOwners.One(stock.OwnerId), GoodsKinds.One(stock.ItemKind)), stock.Id)
            : world.RecheckGoods(new(GoodsUse.Cargo, actor, GoodsOwners.One(actor), GoodsKinds.One(stock.ItemKind), ContainerId: cartId), stock.Id);
        if (match is null) return null;
        var displayQuantity = choice.Kind == HandcartCargoKind.Load
            ? Math.Min(InventoryContainerRules.HandcartCapacity - InventoryIndex.For(inventory).ContentsQuantity(cartId), world.AvailableLotQuantity(stock))
            : stock.Quantity;
        var available = choice.Kind == HandcartCargoKind.Load && stock.OwnerId != actor && world.IsEdibleFood(stock.ItemKind)
            ? Math.Min(displayQuantity, world.SharedFoodCollectionAllowance(actor)) : displayQuantity;
        var quantity = Math.Min(maximumQuantity, choice.Kind == HandcartCargoKind.Unload ? Math.Min(available, world.FreeCarryCapacity(actor)) : available);
        return new(actor, choice, cartId, new(point.X, point.Y), GoodsPlanFamily.Capture(inventory, cart),
            world.Transport.Hitches.FirstOrDefault(item => item.PullerId == actor), stock.OwnerId, stock.ItemKind,
            GoodsPlanFamily.Capture(inventory, stock), match.Place, quantity, displayQuantity);
    }

    internal static ActionCheck CheckHandcartCargoPlan(IHandcartWorld world, HandcartCargoActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? world.TransportCapabilities);
        if (!capability.Allowed) return capability;
        var selection = CheckHandcartSelection(world, plan.Actor, plan.CartId, plan.Position, plan.CartFamily, plan.Hitch);
        if (!selection.Allowed) return selection;
        if (world.Agents[plan.Actor].Position != plan.Position)
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "they are no longer beside the selected handcart");
        var inventory = world.Inventory;
        var cart = inventory.GetLot(plan.CartId);
        if (!InventoryRules.CanMoveFamily(InventoryIndex.For(inventory), cart))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected cart cargo is promised now");
        var load = plan.Choice.Kind == HandcartCargoKind.Load;
        var match = world.RecheckGoods(new(load ? GoodsUse.ConsumeAt : GoodsUse.Cargo, plan.Actor, GoodsOwners.One(plan.StockOwner),
            GoodsKinds.One(plan.ItemKind), ContainerId: load ? null : plan.CartId), plan.Choice.StockId);
        if (match is null || match.Quantity < plan.Quantity || match.Place != plan.SourcePlace ||
            !GoodsPlanFamily.Matches(inventory, match.Lot, plan.StockFamily) ||
            load && (!CanLoadCartLot(world, plan.Actor, world.Agents[plan.Actor], match.Lot) || !InventoryHoldRules.Allows(InventoryIndex.For(inventory), match.Lot)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected load is no longer available here");
        if (load && (!CanPullHandcart(world, cart) || plan.Quantity > InventoryContainerRules.HandcartCapacity - InventoryIndex.For(inventory).ContentsQuantity(cart.Id)) ||
            plan.Quantity <= 0 || !load && plan.Choice.Kind == HandcartCargoKind.Unload && world.FreeCarryCapacity(plan.Actor) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no longer room for the planned load");
        if (load && plan.StockOwner != plan.Actor && world.IsEdibleFood(plan.ItemKind) && world.SharedFoodCollectionAllowance(plan.Actor) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the household needs the selected food now");
        return new();
    }

    internal static void ApplyCheckedHandcartCargo(IHandcartWorld world, HandcartCargoActionPlan plan)
    {
        CompleteHandcartCargo(world, plan, world.Inventory.GetLot(plan.CartId), world.Inventory.GetLot(plan.Choice.StockId));
    }
}
