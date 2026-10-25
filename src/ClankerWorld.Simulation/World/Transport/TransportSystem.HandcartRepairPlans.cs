using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    internal static readonly string[] CartRepairKinds = ["wood", "iron_fittings", "rope"];
    internal static IEnumerable<InventoryLot> WornHandcarts(IHandcartWorld world, string actor) => world.HandcartControlStock(actor)
        .Where(cart => cart.ConditionBasisPoints < 10_000 && !world.HasActiveContainerReservation(world.Inventory, cart.Id));

    internal static HandcartRepairActionPlan? PlanHandcartRepair(IHandcartWorld world, string actor, HandcartRepairChoice choice)
    {
        if (world.RecheckGoods(new(GoodsUse.HandcartControl, actor, GoodsOwners.One(actor), GoodsKinds.One(InventoryContainerRules.Handcart)),
                choice.CartId) is not { Lot: var cart } || cart.GroundPosition is not { } ground) return null;
        var materials = new List<PlannedRepairMaterial>();
        foreach (var kind in CartRepairKinds)
        {
            if (world.SpendingGoods(actor, GoodsUse.Spend, [actor], kind).FirstOrDefault(match =>
                ToolProgressionRules.IsTopLevelCarriedLot(match.Lot, actor)) is not { } stock) return null;
            materials.Add(new(stock.Lot.Id, kind, 1));
        }
        var position = new GridPoint(ground.X, ground.Y);
        return new(actor, choice, position, GoodsPlanFamily.Capture(world.Inventory, cart),
            world.Transport.Hitches.FirstOrDefault(item => item.PullerId == actor), materials.AsReadOnly(),
            world.Reach(actor, position, ReachPurpose.HandcartRepair, 0, 0, SwimmingPolicy.WhenCapable));
    }

    internal static HandcartRepairInputActionPlan? PlanHandcartRepairInput(IHandcartWorld world, string actor, HandcartRepairInputChoice choice)
    {
        if (!CartRepairKinds.Contains(choice.ItemKind, StringComparer.Ordinal) || WornHandcarts(world, actor).FirstOrDefault() is not { } cart ||
            world.SharedItem(choice.ItemKind, actor) is not { } stock || cart.GroundPosition is not { } ground) return null;
        var pickup = new PlannedEquipmentPickup(stock.Id, stock.OwnerId, stock.ItemKind,
            world.HouseholdStockPosition(stock), world.HouseholdStockInteractionRange(stock));
        return new(actor, choice, cart.Id, new(ground.X, ground.Y), GoodsPlanFamily.Capture(world.Inventory, cart),
            world.Transport.Hitches.FirstOrDefault(item => item.PullerId == actor), pickup,
            world.Reach(actor, pickup.Position, ReachPurpose.HandcartRepair, pickup.InteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    internal static ActionCheck CheckHandcartRepairTarget(IHandcartWorld world, ActionPlan plan, string cartId, GridPoint position,
        IReadOnlyList<PlannedStockMember> family, HandcartHitch? hitch, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? world.TransportCapabilities);
        if (!capability.Allowed) return capability;
        var selection = CheckHandcartSelection(world, plan.Actor, cartId, position, family, hitch);
        if (!selection.Allowed) return selection;
        var cart = world.Inventory.GetLot(cartId);
        return cart.ConditionBasisPoints < 10_000 && InventoryRules.CanMoveFamily(InventoryIndex.For(world.Inventory),
            cart, InventoryHoldRules.BorrowedBy(plan.Actor)) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected cart no longer needs repair or its cargo is promised");
    }

    internal static ActionCheck CheckHandcartRepairPlan(IHandcartWorld world, HandcartRepairActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var target = CheckHandcartRepairTarget(world, plan, plan.Choice.CartId, plan.Position, plan.Family, plan.Hitch, policy);
        if (!target.Allowed) return target;
        var index = InventoryIndex.For(world.Inventory);
        foreach (var material in plan.Materials)
            if (world.RecheckGoods(new(GoodsUse.Spend, plan.Actor, GoodsOwners.One(plan.Actor), GoodsKinds.One(material.ItemKind)), material.StockId) is not { } match ||
                !ToolProgressionRules.IsTopLevelCarriedLot(match.Lot, plan.Actor) ||
                !InventoryRules.CanReserve(index, match.Lot, plan.Actor, material.Quantity, InventoryHoldRules.BorrowedBy(plan.Actor)))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected repair material is no longer available");
        return world.Agents[plan.Actor].Position == plan.Position || world.Reach(plan.Actor, plan.Position, ReachPurpose.HandcartRepair, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected handcart is out of reach now");
    }

    internal static ActionCheck CheckHandcartRepairInputPlan(IHandcartWorld world, HandcartRepairInputActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var target = CheckHandcartRepairTarget(world, plan, plan.CartId, plan.Position, plan.Family, plan.Hitch, policy);
        if (!target.Allowed) return target;
        return world.HasCarriedMaterial(plan.Actor, plan.Choice.ItemKind, 1) || world.HasCarriedEquipmentAtLeast(plan.Actor, plan.Choice.ItemKind)
            ? ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they already carry this repair input")
            : world.CheckEquipmentPickup(plan.Actor, plan.Pickup, "repair input", ReachPurpose.HandcartRepair);
    }

    internal static void ApplyCheckedHandcartRepair(IHandcartWorld world, HandcartRepairActionPlan plan)
    {
        if (world.Agents[plan.Actor].Position != plan.Position) { world.MoveToward(plan.Actor, world.Agents[plan.Actor], plan.Position, "repair_handcart"); return; }
        CompleteHandcartRepair(world, plan, world.Inventory.GetLot(plan.Choice.CartId));
    }

    internal static void ApplyCheckedHandcartRepairInput(IHandcartWorld world, HandcartRepairInputActionPlan plan)
    {
        world.ApplyPlannedEquipmentPickup(plan.Actor, plan.Pickup);
    }
}
