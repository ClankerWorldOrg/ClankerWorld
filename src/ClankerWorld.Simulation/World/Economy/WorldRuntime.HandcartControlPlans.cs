using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private HandcartControlActionPlan? PlanHandcartControl(string actor, HandcartControlChoice choice, InventoryLot? selected = null)
    {
        var hitch = handcartHitches.FirstOrDefault(item => item.PullerId == actor);
        var cart = selected ?? (choice.Kind == HandcartControlKind.Attach
            ? RecheckGoods(new(GoodsUse.HandcartControl, actor, GoodsOwners.One(actor), GoodsKinds.One(InventoryContainerRules.Handcart)), choice.TargetId!)?.Lot :
            choice.Kind == HandcartControlKind.Park ? hitch is not null ? InventoryIndex.For(society.Checkpoint.Inventory).Find(hitch.CartLotId) : null :
            HandcartControlStock(actor).FirstOrDefault(item => item.GroundPosition == new InventoryGroundPosition(agents[actor].Position.X, agents[actor].Position.Y)));
        if (cart?.GroundPosition is not { } ground) return null;
        var position = new GridPoint(ground.X, ground.Y);
        return new(actor, choice, cart.Id, position, GoodsPlanFamily.Capture(society.Checkpoint.Inventory, cart), hitch,
            choice.Kind == HandcartControlKind.Give && agents.TryGetValue(choice.TargetId!, out var recipient) ? recipient.Position : null,
            choice.Kind == HandcartControlKind.Attach ? Reach(actor, position, ReachPurpose.HandcartControl, 0, 0, SwimmingPolicy.WhenCapable) : Array.AsReadOnly<GridPoint>([]));
    }

    private void AddHandcartControlCandidate(List<CognitionCandidate> candidates, HandcartControlActionPlan? plan, string description)
    {
        if (plan is not null && CheckHandcartControlPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, description, UnassignedCartPriority) { ActionPlan = plan });
    }

    private ActionCheck CheckHandcartControlPlan(HandcartControlActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowMounted: plan.Choice.Kind != HandcartControlKind.Attach,
                AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if (RecheckGoods(new(GoodsUse.HandcartControl, plan.Actor, GoodsOwners.One(plan.Actor), GoodsKinds.One(InventoryContainerRules.Handcart)), plan.CartId) is not { Lot: var cart } ||
            cart.GroundPosition != new InventoryGroundPosition(plan.Position.X, plan.Position.Y) || !GoodsPlanFamily.Matches(society.Checkpoint.Inventory, cart, plan.Family))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected cart or cargo has changed");
        var hitch = handcartHitches.FirstOrDefault(item => item.PullerId == plan.Actor);
        if (hitch != plan.Hitch)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected cart attachment has changed");
        if (plan.Choice.Kind == HandcartControlKind.Park)
            return hitch?.CartLotId == plan.CartId && agents[plan.Actor].Position == plan.Position ? new() :
                ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected handcart is no longer attached here");
        if (hitch is not null || HasActiveContainerReservation(society.Checkpoint.Inventory, cart.Id))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected cart is attached or reserved now");
        if (plan.Choice.Kind == HandcartControlKind.Attach)
        {
            if (!CanPullHandcart(cart) || animalWorld.Animals.Any(animal => animal.RiderId == plan.Actor || animal.LeaderId == plan.Actor) ||
                handcartHitches.Any(item => item.CartLotId == plan.CartId))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected handcart is no longer available to attach");
            return Reach(plan.Actor, plan.Position, ReachPurpose.HandcartControl, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
                ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected handcart is out of reach now");
        }
        if (agents[plan.Actor].Position != plan.Position || !agents.TryGetValue(plan.Choice.TargetId!, out var recipient) || !IsAdult(recipient.AgentId) ||
            recipient.Position != plan.RecipientPosition || !IsWithinInteractionRange(plan.Position, recipient.Position, 1))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected recipient is no longer here to receive the cart");
        return InventoryRules.CanMoveFamily(InventoryIndex.For(society.Checkpoint.Inventory), cart, InventoryHoldRules.BorrowedBy(plan.Actor))
            ? new() : ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected cart cargo is promised to other work");
    }

    private void ApplyHandcartControlPlan(HandcartControlActionPlan plan)
    {
        if (CheckHandcartControlPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        if (plan.Choice.Kind == HandcartControlKind.Attach && agents[plan.Actor].Position != plan.Position)
        { MoveToward(plan.Actor, agents[plan.Actor], plan.Position, "attach_handcart"); return; }
        CompleteHandcartControl(plan, society.Checkpoint.Inventory.GetLot(plan.CartId));
    }
}
