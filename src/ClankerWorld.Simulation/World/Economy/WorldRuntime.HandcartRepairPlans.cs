using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private void AddHandcartRepairCandidate(List<CognitionCandidate> candidates, ActionPlan? plan)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, plan is HandcartRepairInputActionPlan input
                ? $"Collect physically accessible {input.Choice.ItemKind.Replace('_', ' ')} to repair your handcart."
                : "Reach and repair your handcart using one carried wood, iron fittings and rope.",
                plan is HandcartRepairInputActionPlan ? 21 : 18)
            { ActionPlan = plan });
    }

    private void ApplyHandcartRepairPlan(HandcartRepairActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        TransportSystem.ApplyCheckedHandcartRepair(HandcartWorld, plan);
    }

    private void ApplyHandcartRepairInputPlan(HandcartRepairInputActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        TransportSystem.ApplyCheckedHandcartRepairInput(HandcartWorld, plan);
    }
}
