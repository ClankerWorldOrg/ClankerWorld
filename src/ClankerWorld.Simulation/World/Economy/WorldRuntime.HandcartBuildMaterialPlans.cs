using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private HandcartBuildMaterialActionPlan? PlanHandcartBuildMaterial(string actor, HandcartBuildMaterialChoice choice,
        RecipeDefinition? selected = null) =>
        TransportSystem.PlanHandcartBuildMaterial(HandcartWorld, actor, choice, selected);

    private void AddHandcartBuildMaterialCandidate(List<CognitionCandidate> candidates, HandcartBuildMaterialActionPlan? plan)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId,
                $"Collect nearby {plan.Choice.ItemKind.Replace('_', ' ')} and carry it to build a handcart at your household Blacksmith.",
                25)
            { ActionPlan = plan });
    }

    private ActionCheck CheckHandcartBuildMaterialPlan(HandcartBuildMaterialActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        TransportSystem.CheckHandcartBuildMaterialPlan(HandcartWorld, plan, policy);

    private void ApplyHandcartBuildMaterialPlan(HandcartBuildMaterialActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedHandcartBuildMaterial(plan);
    }

    private void ApplyCheckedHandcartBuildMaterial(HandcartBuildMaterialActionPlan plan) =>
        TransportSystem.ApplyCheckedHandcartBuildMaterial(HandcartWorld, plan);
}
