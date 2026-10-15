using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    internal static HandcartControlActionPlan? PlanHandcartControl(IHandcartWorld world, string actor, HandcartControlChoice choice, InventoryLot? selected = null)
    {
        var hitch = world.Transport.Hitches.FirstOrDefault(item => item.PullerId == actor);
        var cart = selected ?? (choice.Kind == HandcartControlKind.Attach
            ? world.RecheckGoods(new(GoodsUse.HandcartControl, actor, GoodsOwners.One(actor), GoodsKinds.One(InventoryContainerRules.Handcart)), choice.TargetId!)?.Lot :
            choice.Kind == HandcartControlKind.Park ? hitch is not null ? InventoryIndex.For(world.Inventory).Find(hitch.CartLotId) : null :
            world.HandcartControlStock(actor).FirstOrDefault(item => item.GroundPosition == new InventoryGroundPosition(world.Agents[actor].Position.X, world.Agents[actor].Position.Y)));
        if (cart?.GroundPosition is not { } ground) return null;
        var position = new GridPoint(ground.X, ground.Y);
        return new(actor, choice, cart.Id, position, GoodsPlanFamily.Capture(world.Inventory, cart), hitch,
            choice.Kind == HandcartControlKind.Give && world.Agents.TryGetValue(choice.TargetId!, out var recipient) ? recipient.Position : null,
            choice.Kind == HandcartControlKind.Attach ? world.Reach(actor, position, ReachPurpose.HandcartControl, 0, 0, SwimmingPolicy.WhenCapable) : Array.AsReadOnly<GridPoint>([]));
    }

    internal static ActionCheck CheckHandcartControlPlan(IHandcartWorld world, HandcartControlActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? world.HandcartControlCapabilities(plan.Choice.Kind));
        if (!capability.Allowed) return capability;
        var selection = CheckHandcartSelection(world, plan.Actor, plan.CartId, plan.Position, plan.Family, plan.Hitch);
        if (!selection.Allowed) return selection;
        var cart = world.Inventory.GetLot(plan.CartId);
        var hitch = world.Transport.Hitches.FirstOrDefault(item => item.PullerId == plan.Actor);
        if (plan.Choice.Kind == HandcartControlKind.Park)
            return hitch?.CartLotId == plan.CartId && world.Agents[plan.Actor].Position == plan.Position ? new() :
                ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected handcart is no longer attached here");
        if (hitch is not null || world.HasActiveContainerReservation(world.Inventory, cart.Id))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected cart is attached or reserved now");
        if (plan.Choice.Kind == HandcartControlKind.Attach)
        {
            if (!CanPullHandcart(world, cart) || world.Animals.Any(animal => animal.RiderId == plan.Actor || animal.LeaderId == plan.Actor) ||
                world.Transport.Hitches.Any(item => item.CartLotId == plan.CartId))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected handcart is no longer available to attach");
            return world.Reach(plan.Actor, plan.Position, ReachPurpose.HandcartControl, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
                ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected handcart is out of reach now");
        }
        if (world.Agents[plan.Actor].Position != plan.Position || !world.Agents.TryGetValue(plan.Choice.TargetId!, out var recipient) || !world.IsAdult(recipient.AgentId) ||
            recipient.Position != plan.RecipientPosition || !world.IsWithinInteractionRange(plan.Position, recipient.Position, 1))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected recipient is no longer here to receive the cart");
        return InventoryRules.CanMoveFamily(InventoryIndex.For(world.Inventory), cart, InventoryHoldRules.BorrowedBy(plan.Actor))
            ? new() : ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected cart cargo is promised to other work");
    }

    internal static void ApplyCheckedHandcartControl(IHandcartWorld world, HandcartControlActionPlan plan)
    {
        if (plan.Choice.Kind == HandcartControlKind.Attach && world.Agents[plan.Actor].Position != plan.Position)
        { world.MoveToward(plan.Actor, world.Agents[plan.Actor], plan.Position, "attach_handcart"); return; }
        CompleteHandcartControl(world, plan, world.Inventory.GetLot(plan.CartId));
    }

    internal static ActionCheck CheckHandcartSelection(IHandcartWorld world, string actor, string cartId, GridPoint position,
        IReadOnlyList<PlannedStockMember> family, HandcartHitch? hitch)
    {
        if (world.RecheckGoods(new(GoodsUse.HandcartControl, actor, GoodsOwners.One(actor), GoodsKinds.One(InventoryContainerRules.Handcart)), cartId) is not { Lot: var cart } ||
            cart.GroundPosition != new InventoryGroundPosition(position.X, position.Y) || !GoodsPlanFamily.Matches(world.Inventory, cart, family))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected cart or cargo has changed");
        return world.Transport.Hitches.FirstOrDefault(item => item.PullerId == actor) == hitch ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected cart attachment has changed");
    }
}
