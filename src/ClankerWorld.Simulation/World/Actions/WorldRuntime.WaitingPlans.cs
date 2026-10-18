using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<WaitingChoice, WaitingActionPlan> WaitingDefinition => WaitingActions.Definition;

    private static class WaitingActions
    {
        internal static readonly ActionDefinition<WaitingChoice, WaitingActionPlan> Definition = new(
        WaitingChoice.TryDecode, static _ => WaitingChoice.Encode(),
        new(ActionLifeStages.NonInfants, AllowBoat: true, AllowMounted: true,
            AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true),
        static (world, actor, _, policy) => world.PlanWaiting(actor, policy),
        static (world, plan, policy) => world.CheckWaiting(plan, policy),
        static (world, plan, action, phase) => world.ApplyWaiting(action, plan, phase == ActionPhase.BeforeAge));
    }

    private WaitingActionPlan? PlanWaiting(string actor, ActorCapabilityPolicy policy)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(actor), WaitingChoice.Candidate, policy);
        return capability.Refusal?.Reason == ActionRefusalReason.ActorUnavailable ? null : new(actor, capability.Allowed);
    }

    private ActionCheck CheckWaiting(WaitingActionPlan plan, ActorCapabilityPolicy policy)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy);
        if (capability.Refusal?.Reason == ActionRefusalReason.ActorUnavailable) return capability;
        return capability.Allowed == plan.LegacyAgePermitted ? new()
            : ActionCheck.Refuse(ActionRefusalReason.LifeStage, "their permitted waiting outcome has changed");
    }

    private CognitionCandidate WaitingCandidate(string actor, string text) =>
        new(WaitingDefinition.Encode(new()), text, 100) { ActionPlan = WaitingDefinition.Offer(this, actor, new()) };

    private bool ApplyWaiting(ActionContext action, WaitingActionPlan plan, bool beforeAge)
    {
        if (beforeAge && plan.LegacyAgePermitted) return false;
        if (!plan.LegacyAgePermitted) AppendEvent(new AgeActionRejectedEventPayload(plan.Actor, plan.CandidateId));
        else if (action.ReportIdle) AppendEvent(new AgentIdleEventPayload(plan.Actor));
        return true;
    }
}
