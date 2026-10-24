using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    internal static HandcartPullActionPlan? PlanHandcartPull(IHandcartWorld world, string actor, HandcartPullChoice choice)
    {
        var hitch = world.Transport.Hitches.FirstOrDefault(item => item.PullerId == actor);
        var target = world.Buildings.FirstOrDefault(building => building.InstanceId == choice.BuildingId);
        if (hitch is null || target is null || world.RecheckGoods(new(GoodsUse.HandcartControl, actor, GoodsOwners.One(actor),
                GoodsKinds.One(InventoryContainerRules.Handcart)), hitch.CartLotId) is not { Lot: var cart } ||
            cart.GroundPosition is not { } ground) return null;
        return new(actor, choice, cart.Id, new(ground.X, ground.Y), GoodsPlanFamily.Capture(world.Inventory, cart),
            hitch, target.Position, world.Reach(actor, target.Position, ReachPurpose.HandcartPull, 0, 0, SwimmingPolicy.Never));
    }

    internal static ActionCheck CheckHandcartPullPlan(IHandcartWorld world, HandcartPullActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? world.TransportCapabilities);
        if (!capability.Allowed) return capability;
        var selection = CheckHandcartSelection(world, plan.Actor, plan.CartId, plan.Origin, plan.Family, plan.Hitch);
        if (!selection.Allowed) return selection;
        if (world.Agents[plan.Actor].Position != plan.Origin || world.Buildings.FirstOrDefault(building =>
                building.InstanceId == plan.Choice.BuildingId)?.Position != plan.Destination)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected cart or destination has moved");
        // An absent route is an existing wait step, with native movement-blocked events.
        var cart = world.Inventory.GetLot(plan.CartId);
        return CanPullHandcart(world, cart) && InventoryRules.CanMoveFamily(InventoryIndex.For(world.Inventory), cart)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected cart is broken or its cargo is promised now");
    }

    internal static void ApplyCheckedHandcartPull(IHandcartWorld world, HandcartPullActionPlan plan)
    {
        world.MoveToward(plan.Actor, world.Agents[plan.Actor], plan.Destination, "pull_handcart");
    }
}
