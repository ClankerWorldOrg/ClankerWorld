using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IEnumerable<InventoryLot> WornHandcarts(string actor) =>
        TransportSystem.WornHandcarts(HandcartWorld, actor);

    private HandcartRepairActionPlan? PlanHandcartRepair(string actor, HandcartRepairChoice choice) =>
        TransportSystem.PlanHandcartRepair(HandcartWorld, actor, choice);

    private HandcartRepairInputActionPlan? PlanHandcartRepairInput(string actor, HandcartRepairInputChoice choice) =>
        TransportSystem.PlanHandcartRepairInput(HandcartWorld, actor, choice);

    private void AddHandcartRepairCandidate(List<CognitionCandidate> candidates, ActionPlan? plan)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, plan is HandcartRepairInputActionPlan input
                ? $"Collect physically accessible {input.Choice.ItemKind.Replace('_', ' ')} to repair your handcart."
                : "Reach and repair your handcart using one carried wood, iron fittings and rope.",
                plan is HandcartRepairInputActionPlan ? 21 : 18)
            { ActionPlan = plan });
    }

    private ActionCheck CheckHandcartRepairPlan(HandcartRepairActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        TransportSystem.CheckHandcartRepairPlan(HandcartWorld, plan, policy);

    private ActionCheck CheckHandcartRepairInputPlan(HandcartRepairInputActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        TransportSystem.CheckHandcartRepairInputPlan(HandcartWorld, plan, policy);

    private void ApplyHandcartRepairPlan(HandcartRepairActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedHandcartRepair(plan);
    }

    private void ApplyCheckedHandcartRepair(HandcartRepairActionPlan plan) =>
        TransportSystem.ApplyCheckedHandcartRepair(HandcartWorld, plan);

    private void ApplyHandcartRepairInputPlan(HandcartRepairInputActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedHandcartRepairInput(plan);
    }

    private void ApplyCheckedHandcartRepairInput(HandcartRepairInputActionPlan plan) =>
        TransportSystem.ApplyCheckedHandcartRepairInput(HandcartWorld, plan);
}
