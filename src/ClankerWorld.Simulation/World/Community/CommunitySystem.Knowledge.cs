namespace ClankerWorld.Simulation.World;

internal sealed partial class CommunitySystem
{
    internal int LearnArtifactFacts(
        string recipientId,
        string sourceAgentId,
        AgentKnowledgeArtifact artifact,
        string acquisition, long tick)
    {
        var facts = State.Facts.ToList();
        var learned = 0;
        foreach (var fact in artifact.Facts)
        {
            if (facts.Any(existing => existing.OwnerId == recipientId && existing.Position == fact.Position) ||
                facts.Count(existing => existing.OwnerId == recipientId) >= AgentKnowledgeRules.MaximumFactsPerAgent)
                continue;

            facts.Add(fact with
            {
                Id = KnowledgeFactId(recipientId, fact.Position),
                OwnerId = recipientId,
                LearnedTick = tick,
                Acquisition = acquisition,
                SourceAgentId = sourceAgentId,
                SourceArtifactId = artifact.Id,
            });
            learned++;
        }
        if (learned > 0)
        {
            Commit(State with { Facts = facts.ToArray() });
        }
        return learned;
    }

    internal static string KnowledgeFactId(string ownerId, GridPoint position)
    {
        // Existing short IDs stay unchanged. Descendant IDs contain ancestry and
        // can exceed the ledger's limit; hash the complete key, never truncate it.
        return DerivedId.Create($"knowledge-fact:{ownerId}:{position.X}:{position.Y}", "knowledge-fact-sha256:");
    }

    internal int LearnArtifactRecipes(string actor, string sourceActor, AgentKnowledgeArtifact artifact, string acquisition, long tick)
    {
        var recipes = State.Recipes.ToList();
        var count = 0;
        foreach (var source in artifact.Recipes)
        {
            if (recipes.Any(item => item.OwnerId == actor && item.RecipeId == source.RecipeId) ||
                recipes.Count(item => item.OwnerId == actor) >= AgentKnowledgeRules.MaximumRecipesPerAgent) continue;
            recipes.Add(source with
            {
                Id = RecipeKnowledgeId(actor, source.RecipeId),
                OwnerId = actor,
                LearnedTick = tick,
                Acquisition = acquisition,
                SourceAgentId = sourceActor,
                SourceArtifactId = artifact.Id,
            });
            count++;
        }
        if (count > 0) Commit(State with { Recipes = recipes.ToArray() });
        return count;
    }

    internal static string RecipeKnowledgeId(string actor, string recipeId) =>
        DerivedId.Hash($"{actor}|{recipeId}", "recipe-knowledge:");
}
