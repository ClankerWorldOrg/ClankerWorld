using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void CopyConstructionProjectPlansFrom(WorldRuntime source)
    {
        if (ReferenceEquals(this, source)) return;
        constructionSystem.CopyPlansFrom(source.constructionSystem, (actor, started, plan) =>
            agents.TryGetValue(actor, out var person) && person.Project is { Stage: not ("completed" or "cancelled") } project &&
            project.StartedTick == started && project.CandidateId == plan.CandidateId && project.OrderInstructionId == plan.InstructionId);
    }

    private ConstructionActionPlan PlanConstruction(string actor, BuildingDefinition definition, GridPoint site, string? selectedInstance = null) =>
        ConstructionSystem.PlanConstruction(ConstructionWorld, actor, definition, site, selectedInstance);

    private ConstructionMaterialInput[]? PlanConstructionInputs(IReadOnlyList<ContentQuantity> costs, string owner) =>
        ConstructionSystem.PlanConstructionInputs(ConstructionWorld, costs, owner);

    private static ActorCapabilityPolicy ConstructionCapabilities(BuildingDefinition definition) => ConstructionSystem.ConstructionCapabilities(definition);

    private ActionCheck CheckConstruction(ConstructionActionPlan plan, bool checkDestination = true, ActorCapabilityPolicy? policy = null) =>
        constructionSystem.CheckConstruction(ConstructionWorld, plan, checkDestination, policy);

    private ConstructionActionPlan? RetainedConstruction(string actor, AgentProject project) => constructionSystem.RetainedPlan(actor, project);
}
