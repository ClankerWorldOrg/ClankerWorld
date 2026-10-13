using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IEnumerable<InventoryLot> WornHandcarts(string actor) => HandcartControlStock(actor)
        .Where(cart => cart.ConditionBasisPoints < 10_000 && !HasActiveContainerReservation(society.Checkpoint.Inventory, cart.Id));

    private HandcartRepairActionPlan? PlanHandcartRepair(string actor, HandcartRepairChoice choice)
    {
        if (RecheckGoods(new(GoodsUse.HandcartControl, actor, GoodsOwners.One(actor), GoodsKinds.One(InventoryContainerRules.Handcart)),
                choice.CartId) is not { Lot: var cart } || cart.GroundPosition is not { } ground) return null;
        var materials = new List<PlannedRepairMaterial>();
        foreach (var kind in CartRepairKinds)
        {
            if (SpendingGoods(actor, GoodsUse.Spend, [actor], kind).FirstOrDefault(match =>
                ToolProgressionRules.IsTopLevelCarriedLot(match.Lot, actor)) is not { } stock) return null;
            materials.Add(new(stock.Lot.Id, kind, 1));
        }
        var position = new GridPoint(ground.X, ground.Y);
        return new(actor, choice, position, GoodsPlanFamily.Capture(society.Checkpoint.Inventory, cart),
            handcartHitches.FirstOrDefault(item => item.PullerId == actor), materials.AsReadOnly(),
            Reach(actor, position, ReachPurpose.HandcartRepair, 0, 0, SwimmingPolicy.WhenCapable));
    }

    private HandcartRepairInputActionPlan? PlanHandcartRepairInput(string actor, HandcartRepairInputChoice choice)
    {
        if (!CartRepairKinds.Contains(choice.ItemKind, StringComparer.Ordinal) || WornHandcarts(actor).FirstOrDefault() is not { } cart ||
            SharedItem(choice.ItemKind, actor) is not { } stock || cart.GroundPosition is not { } ground) return null;
        var pickup = new PlannedEquipmentPickup(stock.Id, stock.OwnerId, stock.ItemKind,
            HouseholdStockPosition(stock), HouseholdStockInteractionRange(stock));
        return new(actor, choice, cart.Id, new(ground.X, ground.Y), GoodsPlanFamily.Capture(society.Checkpoint.Inventory, cart),
            handcartHitches.FirstOrDefault(item => item.PullerId == actor), pickup,
            Reach(actor, pickup.Position, ReachPurpose.HandcartRepair, pickup.InteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    private void AddHandcartRepairCandidate(List<CognitionCandidate> candidates, ActionPlan? plan)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, plan is HandcartRepairInputActionPlan input
                ? $"Collect physically accessible {input.Choice.ItemKind.Replace('_', ' ')} to repair your handcart."
                : "Reach and repair your handcart using one carried wood, iron fittings and rope.",
                plan is HandcartRepairInputActionPlan ? 21 : 18)
            { ActionPlan = plan });
    }

    private ActionCheck CheckHandcartRepairTarget(ActionPlan plan, string cartId, GridPoint position,
        IReadOnlyList<PlannedStockMember> family, HandcartHitch? hitch)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        var selection = CheckHandcartSelection(plan.Actor, cartId, position, family, hitch);
        if (!selection.Allowed) return selection;
        var cart = society.Checkpoint.Inventory.GetLot(cartId);
        return cart.ConditionBasisPoints < 10_000 && InventoryRules.CanMoveFamily(InventoryIndex.For(society.Checkpoint.Inventory),
            cart, InventoryHoldRules.BorrowedBy(plan.Actor)) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected cart no longer needs repair or its cargo is promised");
    }

    private ActionCheck CheckHandcartRepairPlan(HandcartRepairActionPlan plan)
    {
        var target = CheckHandcartRepairTarget(plan, plan.Choice.CartId, plan.Position, plan.Family, plan.Hitch);
        if (!target.Allowed) return target;
        var index = InventoryIndex.For(society.Checkpoint.Inventory);
        foreach (var material in plan.Materials)
            if (RecheckGoods(new(GoodsUse.Spend, plan.Actor, GoodsOwners.One(plan.Actor), GoodsKinds.One(material.ItemKind)), material.StockId) is not { } match ||
                !ToolProgressionRules.IsTopLevelCarriedLot(match.Lot, plan.Actor) ||
                !InventoryRules.CanReserve(index, match.Lot, plan.Actor, material.Quantity, InventoryHoldRules.BorrowedBy(plan.Actor)))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected repair material is no longer available");
        return agents[plan.Actor].Position == plan.Position || Reach(plan.Actor, plan.Position, ReachPurpose.HandcartRepair, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected handcart is out of reach now");
    }

    private ActionCheck CheckHandcartRepairInputPlan(HandcartRepairInputActionPlan plan)
    {
        var target = CheckHandcartRepairTarget(plan, plan.CartId, plan.Position, plan.Family, plan.Hitch);
        if (!target.Allowed) return target;
        return HasCarriedMaterial(plan.Actor, plan.Choice.ItemKind, 1) || HasCarriedEquipmentAtLeast(plan.Actor, plan.Choice.ItemKind)
            ? ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they already carry this repair input")
            : CheckEquipmentPickup(plan.Actor, plan.Pickup);
    }

    private void ApplyHandcartRepairPlan(HandcartRepairActionPlan plan)
    {
        if (CheckHandcartRepairPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        if (agents[plan.Actor].Position != plan.Position) { MoveToward(plan.Actor, agents[plan.Actor], plan.Position, "repair_handcart"); return; }
        CompleteHandcartRepair(plan, society.Checkpoint.Inventory.GetLot(plan.Choice.CartId));
    }

    private void ApplyHandcartRepairInputPlan(HandcartRepairInputActionPlan plan)
    {
        if (CheckHandcartRepairInputPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyPlannedEquipmentPickup(plan.Actor, plan.Pickup);
    }
}
