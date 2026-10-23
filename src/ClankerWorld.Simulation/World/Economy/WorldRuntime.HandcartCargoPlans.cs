using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

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

    private void ApplyHandcartCargoPlan(HandcartCargoActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        TransportSystem.ApplyCheckedHandcartCargo(HandcartWorld,plan);
    }
}
