using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private HandcartPullActionPlan? PlanHandcartPull(string actor, HandcartPullChoice choice)
    {
        var hitch = handcartHitches.FirstOrDefault(item => item.PullerId == actor);
        var target = worldSimulation.Buildings.FirstOrDefault(building => building.InstanceId == choice.BuildingId);
        if (hitch is null || target is null || RecheckGoods(new(GoodsUse.HandcartControl, actor, GoodsOwners.One(actor),
                GoodsKinds.One(InventoryContainerRules.Handcart)), hitch.CartLotId) is not { Lot: var cart } ||
            cart.GroundPosition is not { } ground) return null;
        return new(actor, choice, cart.Id, new(ground.X, ground.Y), GoodsPlanFamily.Capture(society.Checkpoint.Inventory, cart),
            hitch, target.Position, Reach(actor, target.Position, ReachPurpose.HandcartPull, 0, 0, SwimmingPolicy.Never));
    }

    private void AddHandcartPullCandidate(List<CognitionCandidate> candidates, HandcartPullActionPlan? plan)
    {
        if (plan is not null && agents[plan.Actor].Position != plan.Destination && CheckHandcartPullPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, plan.Route.Count > 0
                ? "Pull the handcart and its cargo to this building along a legal route."
                : "The cart's route to this building is blocked. Wait for a route or park the cart here.",
                UnassignedCartPriority, plan.Choice.BuildingId)
            { ActionPlan = plan });
    }

    private ActionCheck CheckHandcartPullPlan(HandcartPullActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        var selection = CheckHandcartSelection(plan.Actor, plan.CartId, plan.Origin, plan.Family, plan.Hitch);
        if (!selection.Allowed) return selection;
        if (agents[plan.Actor].Position != plan.Origin || worldSimulation.Buildings.FirstOrDefault(building =>
                building.InstanceId == plan.Choice.BuildingId)?.Position != plan.Destination)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected cart or destination has moved");
        return CanPullHandcart(society.Checkpoint.Inventory.GetLot(plan.CartId)) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected cart is broken or its cargo is promised now");
        // An absent route is an existing wait step, with native movement-blocked events.
    }

    private void ApplyHandcartPullPlan(HandcartPullActionPlan plan)
    {
        if (CheckHandcartPullPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        MoveToward(plan.Actor, agents[plan.Actor], plan.Destination, "pull_handcart");
    }
}
