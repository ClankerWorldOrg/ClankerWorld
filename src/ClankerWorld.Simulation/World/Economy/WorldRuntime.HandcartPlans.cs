using ClankerWorld.Simulation.Cognition;

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
        TransportSystem.ApplyCheckedHandcartBuildMaterial(HandcartWorld, plan);
    }

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
        TransportSystem.ApplyCheckedHandcartCargo(HandcartWorld, plan);
    }

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

    private void AddHandcartPullCandidate(List<CognitionCandidate> candidates, HandcartPullActionPlan? plan)
    {
        if (plan is not null && agents[plan.Actor].Position != plan.Destination && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, plan.Route.Count > 0
                ? "Pull the handcart and its cargo to this building along a legal route."
                : "The cart's route to this building is blocked. Wait for a route or park the cart here.",
                UnassignedCartPriority, plan.Choice.BuildingId)
            { ActionPlan = plan });
    }

    private void ApplyHandcartPullPlan(HandcartPullActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        TransportSystem.ApplyCheckedHandcartPull(HandcartWorld, plan);
    }

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
