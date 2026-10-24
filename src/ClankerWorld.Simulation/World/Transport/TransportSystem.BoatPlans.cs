
namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    internal static BoatTripActionPlan? PlanBoatTrip(IBoatWorld world, string actor, string townId, PlacedBuilding origin, PlacedBuilding destination)
    {
        var approach = origin.Entrance!.Value;
        foreach (var boat in world.Transport.Boats.Boats.Where(boat => boat.TownId == townId && boat.DockedPortId == origin.InstanceId && boat.Journey is null))
        {
            // This proves geography only. The queue later assigns a free boat and dock.
            var water = world.ReachBoatGeography(boat, destination);
            if (water.Count == 0) continue;
            return new(actor, new(townId, origin.InstanceId, destination.InstanceId), origin.Position, approach,
                destination.Position, boat.Id, boat.Position, world.Reach(actor, approach, ReachPurpose.BoatDeparture, 0, 0,
                    SwimmingPolicy.WhenCapable), water);
        }
        return null;
    }

    internal static BoatWaitingActionPlan? PlanBoatWaiting(IBoatWorld world, string actor, BoatWaitingChoice choice)
    {
        if (WaitingBoatRequest(world, actor) is not { } request || request.Id != choice.RequestId) return null;
        var approach = choice.Kind == BoatWaitingKind.Queue ? world.Port(request.OriginPortId)?.Entrance : null;
        if (choice.Kind == BoatWaitingKind.Queue && approach is null) return null;
        return new(actor, choice, request, approach, approach is { } point
            ? world.Reach(actor, point, ReachPurpose.BoatDeparture, 0, 0, SwimmingPolicy.WhenCapable) : Array.AsReadOnly<GridPoint>([]));
    }

    internal static ActionCheck CheckBoatTripPlan(IBoatWorld world, BoatTripActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? world.BoatTripCapabilities);
        if (!capability.Allowed) return capability;
        var origin = world.Port(plan.Choice.OriginPortId);
        var destination = world.Port(plan.Choice.DestinationPortId);
        var boat = world.Transport.Boats.Boats.FirstOrDefault(item => item.Id == plan.ProofBoatId);
        if (!ReadyToBoard(world, plan.Actor) || WaitingBoatRequest(world, plan.Actor) is not null || !CanUseTownBoat(world, plan.Choice.TownId, plan.Actor) ||
            origin is null || destination is null || !world.PortIsLegal(origin) || !world.PortIsLegal(destination) || origin.Position != plan.OriginPosition ||
            origin.Entrance != plan.Approach || destination.Position != plan.DestinationPosition || boat is null ||
            boat.TownId != plan.Choice.TownId || boat.DockedPortId != origin.InstanceId || boat.Journey is not null || boat.Position != plan.ProofBoatPosition)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected departure, boat access or trip is no longer available");
        if (world.ReachBoatGeography(boat, destination).Count == 0)
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected destination is no longer reachable by boat");
        return world.Agents[plan.Actor].Position == plan.Approach || world.Reach(plan.Actor, plan.Approach, ReachPurpose.BoatDeparture, 0, 0,
            SwimmingPolicy.WhenCapable).Count > 0 ? new() :
            ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected departure Port is out of reach now");
    }

    internal static ActionCheck CheckBoatWaitingPlan(IBoatWorld world, BoatWaitingActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? world.TransportCapabilities);
        if (!capability.Allowed) return capability;
        if (WaitingBoatRequest(world, plan.Actor) != plan.Request)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected waiting boat request has changed");
        if (plan.Choice.Kind == BoatWaitingKind.Cancel) return new();
        if (world.Port(plan.Request.OriginPortId)?.Entrance != plan.Approach || plan.Approach is null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected departure Port has changed");
        // Returning to a blocked departure remains a native movement wait.
        return new();
    }

    internal static void ApplyCheckedBoatTrip(IBoatWorld world, BoatTripActionPlan plan)
    {
        if (world.Agents[plan.Actor].Position != plan.Approach) { world.MoveToward(plan.Actor, world.Agents[plan.Actor], plan.Approach, "boat_departure"); return; }
        QueueBoatTrip(world, plan.Actor, new(plan.CandidateId, plan.Choice.TownId, world.Port(plan.Choice.OriginPortId)!, world.Port(plan.Choice.DestinationPortId)!, plan));
    }

    internal static void ApplyCheckedBoatWaiting(IBoatWorld world, BoatWaitingActionPlan plan)
    {
        if (plan.Choice.Kind == BoatWaitingKind.Cancel) world.Transport.RecordBoatRequest(plan.Request with { Status = "cancelled", SettledTick = world.WorldTick });
        else world.MoveToward(plan.Actor, world.Agents[plan.Actor], plan.Approach!.Value, "boat_queue");
    }
}
