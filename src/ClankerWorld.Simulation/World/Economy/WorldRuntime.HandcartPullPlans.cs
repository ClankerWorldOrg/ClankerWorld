using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private HandcartPullActionPlan? PlanHandcartPull(string actor, HandcartPullChoice choice) =>
        TransportSystem.PlanHandcartPull(HandcartWorld, actor, choice);

    private void AddHandcartPullCandidate(List<CognitionCandidate> candidates, HandcartPullActionPlan? plan)
    {
        if (plan is not null && agents[plan.Actor].Position != plan.Destination && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, plan.Route.Count > 0
                ? "Pull the handcart and its cargo to this building along a legal route."
                : "The cart's route to this building is blocked. Wait for a route or park the cart here.",
                UnassignedCartPriority, plan.Choice.BuildingId)
            { ActionPlan = plan });
    }

    private ActionCheck CheckHandcartPullPlan(HandcartPullActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        TransportSystem.CheckHandcartPullPlan(HandcartWorld, plan, policy);

    private void ApplyHandcartPullPlan(HandcartPullActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedHandcartPull(plan);
    }

    private void ApplyCheckedHandcartPull(HandcartPullActionPlan plan) =>
        TransportSystem.ApplyCheckedHandcartPull(HandcartWorld, plan);
}
