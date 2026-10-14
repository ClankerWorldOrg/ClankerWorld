namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<ConstructionChoice, ConstructionActionPlan> ConstructionDefinition => ConstructionActions.Building;
    private static ActionDefinition<ExpansionChoice, ExpansionActionPlan> ExpansionDefinition => ConstructionActions.Expansion;

    private static class ConstructionActions
    {
        internal static readonly ActionDefinition<ConstructionChoice, ConstructionActionPlan> Building = new(
            DecodeReferenceChoice(ConstructionChoice.Decode), static choice => choice.Encode(),
            new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: true, AllowActiveOrder: true),
            static (world, actor, choice, _) => world.worldContent.Buildings.FirstOrDefault(definition => definition.CanonicalId == choice.DefinitionId) is { } definition
                ? world.PlanConstruction(actor, definition, choice.Site) : null,
            static (world, plan, policy) => world.CheckConstruction(plan, policy: policy),
            static (world, plan, action, _) =>
            {
                world.BeginProject(action.AgentId, action.State, action.CandidateId, originalPlan: plan);
                return true;
            },
            PlanCapabilities: static plan => ConstructionCapabilities(plan.Definition),
            NoPlanFallback: static (world, action) =>
            {
                world.BeginProject(action.AgentId, action.State, action.CandidateId, originalPlan: action.Plan);
                return true;
            });

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
