namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void ApplyBoatTripPlan(BoatTripActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        TransportSystem.ApplyCheckedBoatTrip(BoatWorld, plan);
    }

    private void ApplyBoatWaitingPlan(BoatWaitingActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        TransportSystem.ApplyCheckedBoatWaiting(BoatWorld, plan);
    }
}
