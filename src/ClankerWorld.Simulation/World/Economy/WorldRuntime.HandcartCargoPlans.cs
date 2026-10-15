using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private HandcartCargoActionPlan? PlanHandcartCargo(string actor, HandcartCargoChoice choice,
        string? targetCartId = null, int maximumQuantity = int.MaxValue)
    {
        var inventory = society.Checkpoint.Inventory;
        var stock = InventoryIndex.For(inventory).Find(choice.StockId);
        var cartId = targetCartId ?? (choice.Kind == HandcartCargoKind.Load
            ? NearbyOwnedHandcart(actor, agents[actor].Position)?.Id : stock?.ContainerLotId);
        if (cartId is null || RecheckGoods(new(GoodsUse.HandcartControl, actor, GoodsOwners.One(actor),
                GoodsKinds.One(InventoryContainerRules.Handcart)), cartId) is not { Lot: var cart } || cart.GroundPosition is not { } point || stock is null)
            return null;
        var match = choice.Kind == HandcartCargoKind.Load
            ? RecheckGoods(new(GoodsUse.ConsumeAt, actor, GoodsOwners.One(stock.OwnerId), GoodsKinds.One(stock.ItemKind)), stock.Id)
            : RecheckGoods(new(GoodsUse.Cargo, actor, GoodsOwners.One(actor), GoodsKinds.One(stock.ItemKind), ContainerId: cartId), stock.Id);
        if (match is null) return null;
        var displayQuantity = choice.Kind == HandcartCargoKind.Load
            ? Math.Min(InventoryContainerRules.HandcartCapacity - InventoryIndex.For(inventory).ContentsQuantity(cartId), AvailableLotQuantity(stock))
            : stock.Quantity;
        var available = choice.Kind == HandcartCargoKind.Load && stock.OwnerId != actor && IsEdibleFood(stock.ItemKind)
            ? Math.Min(displayQuantity, SharedFoodCollectionAllowance(actor)) : displayQuantity;
        var quantity = Math.Min(maximumQuantity, choice.Kind == HandcartCargoKind.Unload ? Math.Min(available, FreeCarryCapacity(actor)) : available);
        return new(actor, choice, cartId, new(point.X, point.Y), GoodsPlanFamily.Capture(inventory, cart),
            handcartHitches.FirstOrDefault(item => item.PullerId == actor), stock.OwnerId, stock.ItemKind,
            GoodsPlanFamily.Capture(inventory, stock), match.Place, quantity, displayQuantity);
    }

    private void AddHandcartCargoCandidate(List<CognitionCandidate> candidates, HandcartCargoActionPlan? plan)
    {
        if (plan is not null && CheckHandcartCargoPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, plan.Choice.Kind switch
            {
                HandcartCargoKind.Load => $"Load up to {plan.DisplayQuantity} nearby {plan.ItemKind.Replace('_', ' ')} into your handcart.",
                HandcartCargoKind.Unload => "Unload cart goods into your carried load, within your carrying limit.",
                _ => "Unload cart goods onto the ground here, keeping your ownership.",
            }, UnassignedCartPriority)
            { ActionPlan = plan });
    }

    private ActionCheck CheckHandcartCargoPlan(HandcartCargoActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        var selection = CheckHandcartSelection(plan.Actor, plan.CartId, plan.Position, plan.CartFamily, plan.Hitch);
        if (!selection.Allowed) return selection;
        if (agents[plan.Actor].Position != plan.Position)
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "they are no longer beside the selected handcart");
        var inventory = society.Checkpoint.Inventory;
        var cart = inventory.GetLot(plan.CartId);
        if (!InventoryRules.CanMoveFamily(InventoryIndex.For(inventory), cart))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected cart cargo is promised now");
        var load = plan.Choice.Kind == HandcartCargoKind.Load;
        var match = RecheckGoods(new(load ? GoodsUse.ConsumeAt : GoodsUse.Cargo, plan.Actor, GoodsOwners.One(plan.StockOwner),
            GoodsKinds.One(plan.ItemKind), ContainerId: load ? null : plan.CartId), plan.Choice.StockId);
        if (match is null || match.Quantity < plan.Quantity || match.Place != plan.SourcePlace ||
            !GoodsPlanFamily.Matches(inventory, match.Lot, plan.StockFamily) ||
            load && (!CanLoadCartLot(plan.Actor, agents[plan.Actor], match.Lot) || !InventoryHoldRules.Allows(InventoryIndex.For(inventory), match.Lot)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected load is no longer available here");
        if (load && (!CanPullHandcart(cart) || plan.Quantity > InventoryContainerRules.HandcartCapacity - InventoryIndex.For(inventory).ContentsQuantity(cart.Id)) ||
            plan.Quantity <= 0 || !load && plan.Choice.Kind == HandcartCargoKind.Unload && FreeCarryCapacity(plan.Actor) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no longer room for the planned load");
        if (load && plan.StockOwner != plan.Actor && IsEdibleFood(plan.ItemKind) && SharedFoodCollectionAllowance(plan.Actor) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the household needs the selected food now");
        return new();
    }

    private void ApplyHandcartCargoPlan(HandcartCargoActionPlan plan)
    {
        if (CheckHandcartCargoPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteHandcartCargo(plan, society.Checkpoint.Inventory.GetLot(plan.CartId), society.Checkpoint.Inventory.GetLot(plan.Choice.StockId));
    }
}
