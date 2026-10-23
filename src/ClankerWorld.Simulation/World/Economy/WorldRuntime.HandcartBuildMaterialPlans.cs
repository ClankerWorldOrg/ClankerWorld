using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private void AddHandcartBuildMaterialCandidate(List<CognitionCandidate> candidates, HandcartBuildMaterialActionPlan? plan)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId,
                $"Collect nearby {plan.Choice.ItemKind.Replace('_', ' ')} and carry it to build a handcart at your household Blacksmith.",
                25)
            { ActionPlan = plan });
    }

    private void ApplyHandcartBuildMaterialPlan(HandcartBuildMaterialActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        TransportSystem.ApplyCheckedHandcartBuildMaterial(HandcartWorld,plan);
    }
}
