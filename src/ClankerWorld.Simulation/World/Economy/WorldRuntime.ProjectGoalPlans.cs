namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy ProjectGoalCapabilities =>
        new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private RecipeActionPlan? PlanRecipeStart(string actor, RecipeChoice choice) =>
        productionSystem.PlanRecipeStart(RecipeWorld, actor, choice);

    private ConstructionProjectActionPlan? PlanConstructionStart(string actor, ConstructionChoice choice)
    {
        if (worldContent.Buildings.FirstOrDefault(definition => definition.CanonicalId == choice.DefinitionId) is not { } definition) return null;
        return choice.Site is { } site ? PlanConstruction(actor, definition, site) : new ConstructionGoalActionPlan(actor, definition);
    }

    private ActionCheck CheckRecipeGoal(RecipeGoalActionPlan plan, ActorCapabilityPolicy policy) =>
        ProductionSystem.CheckRecipeGoal(RecipeWorld, plan, policy);

    private ActionCheck CheckConstructionGoal(ConstructionGoalActionPlan plan, ActorCapabilityPolicy policy)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy);
        if (!capability.Allowed) return capability;
        return worldContent.Buildings.Any(definition => definition.CanonicalId == plan.Definition.CanonicalId) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected building goal is no longer available");
    }
}
