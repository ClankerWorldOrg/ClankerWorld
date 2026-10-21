using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private HandcartCargoActionPlan? PlanHandcartCargo(string actor, HandcartCargoChoice choice,
        string? targetCartId = null, int maximumQuantity = int.MaxValue) =>
        TransportSystem.PlanHandcartCargo(HandcartWorld, actor, choice, targetCartId, maximumQuantity);

    private void AddHandcartCargoCandidate(List<CognitionCandidate> candidates, HandcartCargoActionPlan? plan)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, plan.Choice.Kind switch
            {
                HandcartCargoKind.Load => $"Load up to {plan.DisplayQuantity} nearby {plan.ItemKind.Replace('_', ' ')} into your handcart.",
                HandcartCargoKind.Unload => "Unload cart goods into your carried load, within your carrying limit.",
                _ => "Unload cart goods onto the ground here, keeping your ownership.",
            }, UnassignedCartPriority)
            { ActionPlan = plan });
    }

    private ActionCheck CheckHandcartCargoPlan(HandcartCargoActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        TransportSystem.CheckHandcartCargoPlan(HandcartWorld, plan, policy);

    private void ApplyHandcartCargoPlan(HandcartCargoActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedHandcartCargo(plan);
    }

    private void ApplyCheckedHandcartCargo(HandcartCargoActionPlan plan) =>
        TransportSystem.ApplyCheckedHandcartCargo(HandcartWorld, plan);
}
