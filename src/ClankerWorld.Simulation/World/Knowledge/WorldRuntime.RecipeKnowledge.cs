using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private bool KnowsRecipe(string actor, string recipeId) =>
        knowledge.Recipes.Any(item => item.OwnerId == actor && item.RecipeId == recipeId);

    private static string RecipeKnowledgeId(string actor, string recipeId) => CommunitySystem.RecipeKnowledgeId(actor, recipeId);

    private void LearnProducedRecipe(WorldProductionJob job, RecipeDefinition recipe, long tick)
    {
        if (!agents.ContainsKey(job.WorkerId) ||
            society.Checkpoint.GetAgent(job.WorkerId).Status != SocietyAgentStatus.Active ||
            KnowsRecipe(job.WorkerId, recipe.CanonicalId) ||
            knowledge.Recipes.Count(item => item.OwnerId == job.WorkerId) >= AgentKnowledgeRules.MaximumRecipesPerAgent) return;
        communitySystem.RecordProducedRecipe(new AgentRecipeKnowledge(
            RecipeKnowledgeId(job.WorkerId, recipe.CanonicalId), job.WorkerId, recipe.CanonicalId, job.WorkerId,
            tick, "practice", job.JobId));
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent(new AgentRecipeLearnedEventPayload(job.WorkerId, recipe.CanonicalId));
    }

    private bool HasUnknownArtifactContents(string actor, AgentKnowledgeArtifact artifact) =>
        knowledge.Facts.Count(item => item.OwnerId == actor) < AgentKnowledgeRules.MaximumFactsPerAgent &&
            artifact.Facts.Any(item => !KnowsMapFact(actor, item.Position)) ||
        knowledge.Recipes.Count(item => item.OwnerId == actor) < AgentKnowledgeRules.MaximumRecipesPerAgent &&
            artifact.Recipes.Any(item => !KnowsRecipe(actor, item.RecipeId));

    private AgentRecipeKnowledge[] RecipesToWrite(string actor, string kind, AgentKnowledgeArtifact? source = null)
    {
        var learned = knowledge.Recipes.Where(item => item.OwnerId == actor).ToArray();
        if (source is null) return learned.OrderByDescending(item => item.LearnedTick).ThenBy(item => item.RecipeId, StringComparer.Ordinal)
            .Take(AgentKnowledgeRules.RecipeCapacity(kind)).ToArray();
        if (source.Recipes.Any(item => !learned.Any(known => known.RecipeId == item.RecipeId))) return [];
        return source.Recipes.Select(item => item with
        {
            Id = learned.Single(known => known.RecipeId == item.RecipeId).Id,
            OwnerId = actor,
            LearnedTick = WorldTick,
            Acquisition = "read",
            SourceAgentId = source.CreatorId,
            SourceArtifactId = source.Id,
        }).ToArray();
    }

    private bool HasCompleteCopyContents(string actor, AgentKnowledgeArtifact source, AgentKnowledgeFact[] facts, AgentRecipeKnowledge[] recipes) =>
        facts.Length == source.Facts.Count && recipes.Length == source.Recipes.Count &&
        source.Facts.All(item => KnowsMapFact(actor, item.Position)) && source.Recipes.All(item => KnowsRecipe(actor, item.RecipeId));
}
