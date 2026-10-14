using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private BoatTripActionPlan? PlanBoatTrip(string actor, string townId, PlacedBuilding origin, PlacedBuilding destination)
    {
        var approach = origin.Entrance!.Value;
        foreach (var boat in boatTransport.Boats.Where(boat => boat.TownId == townId && boat.DockedPortId == origin.InstanceId && boat.Journey is null))
        {
            // This proves geography only. The queue later assigns a free boat and dock.
            var water = ReachBoatGeography(boat, destination);
            if (water.Count == 0) continue;
            return new(actor, new(townId, origin.InstanceId, destination.InstanceId), origin.Position, approach,
                destination.Position, boat.Id, boat.Position, Reach(actor, approach, ReachPurpose.BoatDeparture, 0, 0,
                    SwimmingPolicy.WhenCapable), water);
        }
        return null;
    }

    private BoatWaitingActionPlan? PlanBoatWaiting(string actor, BoatWaitingChoice choice)
    {
        if (WaitingBoatRequest(actor) is not { } request || request.Id != choice.RequestId) return null;
        var approach = choice.Kind == BoatWaitingKind.Queue ? Port(request.OriginPortId)?.Entrance : null;
        if (choice.Kind == BoatWaitingKind.Queue && approach is null) return null;
        return new(actor, choice, request, approach, approach is { } point
            ? Reach(actor, point, ReachPurpose.BoatDeparture, 0, 0, SwimmingPolicy.WhenCapable) : Array.AsReadOnly<GridPoint>([]));
    }

    private ActionCheck CheckBoatTripPlan(BoatTripActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? BoatTripCapabilities);
        if (!capability.Allowed) return capability;
        var origin = Port(plan.Choice.OriginPortId);
        var destination = Port(plan.Choice.DestinationPortId);
        var boat = boatTransport.Boats.FirstOrDefault(item => item.Id == plan.ProofBoatId);
        if (!ReadyToBoard(plan.Actor) || WaitingBoatRequest(plan.Actor) is not null || !CanUseTownBoat(plan.Choice.TownId, plan.Actor) ||
            origin is null || destination is null || !PortIsLegal(origin) || !PortIsLegal(destination) || origin.Position != plan.OriginPosition ||
            origin.Entrance != plan.Approach || destination.Position != plan.DestinationPosition || boat is null ||
            boat.TownId != plan.Choice.TownId || boat.DockedPortId != origin.InstanceId || boat.Journey is not null || boat.Position != plan.ProofBoatPosition)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected departure, boat access or trip is no longer available");
        if (ReachBoatGeography(boat, destination).Count == 0)
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected destination is no longer reachable by boat");
        return agents[plan.Actor].Position == plan.Approach || Reach(plan.Actor, plan.Approach, ReachPurpose.BoatDeparture, 0, 0,
            SwimmingPolicy.WhenCapable).Count > 0 ? new() :
            ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected departure Port is out of reach now");
    }

    private ActionCheck CheckBoatWaitingPlan(BoatWaitingActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? TransportCapabilities);
        if (!capability.Allowed) return capability;
        if (WaitingBoatRequest(plan.Actor) != plan.Request)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected waiting boat request has changed");
        if (plan.Choice.Kind == BoatWaitingKind.Cancel) return new();
        if (Port(plan.Request.OriginPortId)?.Entrance != plan.Approach || plan.Approach is null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected departure Port has changed");
        // Returning to a blocked departure remains a native movement wait.
        return new();
    }

    private void ApplyBoatTripPlan(BoatTripActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedBoatTrip(plan);
    }

    private void ApplyCheckedBoatTrip(BoatTripActionPlan plan)
    {
        if (agents[plan.Actor].Position != plan.Approach) { MoveToward(plan.Actor, agents[plan.Actor], plan.Approach, "boat_departure"); return; }
        QueueBoatTrip(plan.Actor, new(plan.CandidateId, plan.Choice.TownId, Port(plan.Choice.OriginPortId)!, Port(plan.Choice.DestinationPortId)!, plan));
    }

    private void ApplyBoatWaitingPlan(BoatWaitingActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedBoatWaiting(plan);
    }

    private void ApplyCheckedBoatWaiting(BoatWaitingActionPlan plan)
    {
        if (plan.Choice.Kind == BoatWaitingKind.Cancel) SetBoatRequest(plan.Request with { Status = "cancelled", SettledTick = WorldTick });
        else MoveToward(plan.Actor, agents[plan.Actor], plan.Approach!.Value, "boat_queue");
    }
}
