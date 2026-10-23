using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private void AddHandcartControlCandidate(List<CognitionCandidate> candidates, HandcartControlActionPlan? plan, string description)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, description, UnassignedCartPriority) { ActionPlan = plan });
    }

    private void ApplyHandcartControlPlan(HandcartControlActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        TransportSystem.ApplyCheckedHandcartControl(HandcartWorld, plan);
    }
}
