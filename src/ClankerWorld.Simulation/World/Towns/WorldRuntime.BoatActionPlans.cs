namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private BoatWaitingActionPlan? PlanBoatWaiting(string actor, BoatWaitingChoice choice) =>
        TransportSystem.PlanBoatWaiting(BoatWorld, actor, choice);

    private ActionCheck CheckBoatTripPlan(BoatTripActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        TransportSystem.CheckBoatTripPlan(BoatWorld, plan, policy);

    private ActionCheck CheckBoatWaitingPlan(BoatWaitingActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        TransportSystem.CheckBoatWaitingPlan(BoatWorld, plan, policy);

    private void ApplyBoatTripPlan(BoatTripActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedBoatTrip(plan);
    }

    private void ApplyCheckedBoatTrip(BoatTripActionPlan plan) =>
        TransportSystem.ApplyCheckedBoatTrip(BoatWorld, plan);

    private void ApplyBoatWaitingPlan(BoatWaitingActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedBoatWaiting(plan);
    }

    private void ApplyCheckedBoatWaiting(BoatWaitingActionPlan plan) =>
        TransportSystem.ApplyCheckedBoatWaiting(BoatWorld, plan);
}
