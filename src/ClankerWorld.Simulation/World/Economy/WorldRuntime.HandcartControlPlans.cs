using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private HandcartControlActionPlan? PlanHandcartControl(string actor, HandcartControlChoice choice, InventoryLot? selected = null) =>
        TransportSystem.PlanHandcartControl(HandcartWorld, actor, choice, selected);

    private void AddHandcartControlCandidate(List<CognitionCandidate> candidates, HandcartControlActionPlan? plan, string description)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, description, UnassignedCartPriority) { ActionPlan = plan });
    }

    private ActionCheck CheckHandcartControlPlan(HandcartControlActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        TransportSystem.CheckHandcartControlPlan(HandcartWorld, plan, policy);

    private void ApplyHandcartControlPlan(HandcartControlActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedHandcartControl(plan);
    }

    private void ApplyCheckedHandcartControl(HandcartControlActionPlan plan) =>
        TransportSystem.ApplyCheckedHandcartControl(HandcartWorld, plan);

    private ActionCheck CheckHandcartSelection(string actor, string cartId, GridPoint position,
        IReadOnlyList<PlannedStockMember> family, HandcartHitch? hitch) =>
        TransportSystem.CheckHandcartSelection(HandcartWorld, actor, cartId, position, family, hitch);
}
