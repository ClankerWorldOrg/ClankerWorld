using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void CopyRecipeProjectPlansFrom(WorldRuntime source)
    {
        if (ReferenceEquals(this, source)) return;
        productionSystem.CopyRecipePlansFrom(source.productionSystem, (actor, started, instruction, plan) =>
            agents.TryGetValue(actor, out var person) && person.Project is { Stage: not ("completed" or "cancelled") } project &&
            project.StartedTick == started && project.CandidateId == plan.CandidateId &&
            instruction == RecipeProjectInstructionId(actor, plan.Recipe.CanonicalId));
    }

    private RecipeProjectActionPlan PlanRecipeProject(string actor, RecipeDefinition recipe, PlacedBuilding workstation) =>
        ProductionSystem.PlanRecipeProject(RecipeWorld, actor, recipe, workstation);

    private RecipeProjectInput[]? PlanRecipeInputs(RecipeDefinition recipe, string owner, string? storage, bool carried) =>
        ProductionSystem.PlanRecipeInputs(RecipeWorld, recipe, owner, storage, carried);

    private static ActorCapabilityPolicy RecipeCapabilities(RecipeDefinition recipe) => ProductionSystem.RecipeCapabilities(recipe);

    private ActionCheck CheckRecipeProject(RecipeProjectActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        productionSystem.CheckRecipeProject(RecipeWorld, plan, policy);

    private ActionCheck CheckRecipeInputs(RecipeProjectActionPlan plan) => ProductionSystem.CheckRecipeInputs(RecipeWorld, plan);

    private RecipeProjectActionPlan? RetainedRecipeProject(string actor, AgentProject project) =>
        productionSystem.RecipePlan(actor, project, TownConstructionCandidateIds.TryParse(project.CandidateId, out var choice)
            ? RecipeProjectInstructionId(actor, choice.DefinitionId) : null);

    private string? RecipeProjectInstructionId(string actor, string recipeId) =>
        PendingInstructionFor(actor) is { Task.Action: "produce_item" } instruction && instruction.Task.TargetRecipeId == recipeId
            ? instruction.InstructionId : null;
}
