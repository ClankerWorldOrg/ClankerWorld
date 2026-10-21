namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ToolRequestVisitActionPlan? PlanToolRequestVisit(string actor, ToolRequestVisitChoice choice)
    {
        if (!agents.ContainsKey(actor)) return null;
        foreach (var (smith, recipe) in ToolRequestOptions(actor))
            if (ToolRequestVisitChoice.Encode(smith.InstanceId, recipe.CanonicalId) == choice.Encode())
                return new(actor, choice, smith, recipe,
                    Reach(actor, smith.Position, ReachPurpose.ToolRequest, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
        return null;
    }

    private ActionCheck CheckToolRequestVisitPlan(ToolRequestVisitActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ToolRequestVisitCapabilities);
        if (!capability.Allowed) return capability;
        if (worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == plan.Smith.InstanceId) != plan.Smith ||
            !worldContent.Recipes.Any(recipe => recipe.CanonicalId == plan.Recipe.CanonicalId) ||
            !IdleForToolRequest(plan.Actor) || !MayRequestToolAt(plan.Actor, plan.Smith, plan.Recipe))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Blacksmith or tool request is no longer available");
        return new();
    }

}
