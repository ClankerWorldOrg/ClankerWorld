using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static readonly ActorCapabilityPolicy WaitingPolicy = new(ActionLifeStages.NonInfants,
        AllowBoat: true, AllowMounted: true, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private WaitingActionPlan? PlanWaiting(string actor)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(actor), WaitingChoice.Candidate, WaitingPolicy);
        return capability.Refusal?.Reason == ActionRefusalReason.ActorUnavailable ? null : new(actor, capability.Allowed);
    }

    private ActionCheck CheckWaiting(WaitingActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, WaitingPolicy);
        if (capability.Refusal?.Reason == ActionRefusalReason.ActorUnavailable) return capability;
        return capability.Allowed == plan.LegacyAgePermitted ? new()
            : ActionCheck.Refuse(ActionRefusalReason.LifeStage, "their permitted waiting outcome has changed");
    }

    private CognitionCandidate WaitingCandidate(string actor, string text) =>
        new(WaitingChoice.Candidate, text, 100) { ActionPlan = PlanWaiting(actor) };

    private bool ApplyWaiting(ActionContext action, bool beforeAge)
    {
        var plan = action.Plan as WaitingActionPlan ?? PlanWaiting(action.AgentId);
        if (plan is null) return true;
        if (CheckWaiting(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return true; }
        if (beforeAge && plan.LegacyAgePermitted) return false;
        if (!plan.LegacyAgePermitted) AppendEvent("age_action_rejected", $"{plan.Actor}:{plan.CandidateId}");
        else if (action.ReportIdle) AppendEvent("agent_idle", plan.Actor);
        return true;
    }
}
