namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<ConstructionChoice, ConstructionProjectActionPlan> ConstructionDefinition => ConstructionActions.Building;
    private static ActionDefinition<ExpansionChoice, ExpansionActionPlan> ExpansionDefinition => ConstructionActions.Expansion;

    private static class ConstructionActions
    {
        internal static readonly ActionDefinition<ConstructionChoice, ConstructionProjectActionPlan> Building = new(
            DecodeReferenceChoice(ConstructionChoice.Decode), static choice => choice.Encode(),
            new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: true, AllowActiveOrder: true),
            static (world, actor, choice, _) => world.PlanConstructionStart(actor, choice),
            static (world, plan, policy) => plan switch
            {
                ConstructionActionPlan placed => world.CheckConstruction(placed, policy: policy),
                ConstructionGoalActionPlan goal => world.CheckConstructionGoal(goal, policy),
                _ => ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the building plan is not available"),
            },
            static (world, plan, action, _) =>
            {
                world.BeginProject(action.AgentId, action.State, action.CandidateId, originalPlan: plan);
                return true;
            },
            PlanCapabilities: static plan => plan is ConstructionActionPlan placed ? ConstructionCapabilities(placed.Definition) : ProjectGoalCapabilities);

        internal static readonly ActionDefinition<ExpansionChoice, ExpansionActionPlan> Expansion = new(
            DecodeReferenceChoice(ExpansionChoice.Decode), static choice => choice.Encode(), ExpansionCapabilities,
            static (world, actor, choice, _) => world.worldSimulation.Buildings.FirstOrDefault(building => building.InstanceId == choice.BuildingId) is { } building
                ? world.PlanExpansion(actor, building) : null,
            static (world, plan, policy) => world.CheckExpansion(plan, policy),
            static (world, plan, action, _) =>
            {
                world.ApplyBuildingExpansionCandidate(action.AgentId, action.State, plan.Choice.BuildingId, originalPlan: plan);
                return true;
            });
    }
}
