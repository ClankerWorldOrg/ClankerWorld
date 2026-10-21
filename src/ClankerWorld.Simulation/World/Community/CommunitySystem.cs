namespace ClankerWorld.Simulation.World;

internal sealed partial class CommunitySystem(WorldKnowledgeState state)
{
    internal WorldKnowledgeState State { get; private set; } = state;

    internal void RecordObservedFact(AgentKnowledgeFact fact, AgentKnowledgeFact? previous) => Commit(State with
    {
        Facts = previous is null ? State.Facts.Append(fact).ToArray() : State.Facts.Select(item => item.Id == previous.Id ? fact : item).ToArray(),
        EarlierFacts = previous is not null && AgentKnowledgeRules.ReferencesFact(State, previous)
            ? State.EarlierFacts.Append(previous).ToArray() : State.EarlierFacts,
    });

    internal void StartWriting(AgentKnowledgeWritingProject project) => Commit(State with { WritingProjects = State.WritingProjects.Append(project).ToArray() });

    internal void AdvanceWriting(AgentKnowledgeWritingProject project) => Commit(State with
    {
        WritingProjects = State.WritingProjects.Select(item => item.Id == project.Id ? project : item).ToArray(),
    });

    internal void CompleteWriting(string projectId, AgentKnowledgeArtifact artifact) => Commit(State with
    {
        WritingProjects = State.WritingProjects.Where(item => item.Id != projectId).ToArray(),
        Artifacts = State.Artifacts.Append(artifact).ToArray(),
    });

    internal void CancelWriting(string projectId) => Commit(PruneEarlierFacts(State with
    {
        WritingProjects = State.WritingProjects.Where(item => item.Id != projectId).ToArray(),
    }));

    internal static WorldKnowledgeState PruneEarlierFacts(WorldKnowledgeState knowledge) =>
        knowledge with { EarlierFacts = AgentKnowledgeRules.RetainedEarlierFacts(knowledge).ToArray() };

    internal void RecordProducedRecipe(AgentRecipeKnowledge recipe) => Commit(State with { Recipes = State.Recipes.Append(recipe).ToArray() });

    private void Commit(WorldKnowledgeState next)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalKnowledge(next);
        State = next;
    }

    internal static void ValidateLocalKnowledge(WorldKnowledgeState knowledge)
    {
        ValidateLocalRecipes(knowledge);
        if (knowledge.Facts.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != knowledge.Facts.Count ||
            knowledge.Facts.Select(item => (item.OwnerId, item.Position)).Distinct().Count() != knowledge.Facts.Count ||
            knowledge.Facts.GroupBy(item => item.OwnerId).Any(group => group.Count() > AgentKnowledgeRules.MaximumFactsPerAgent) ||
            knowledge.Artifacts.Count > AgentKnowledgeRules.MaximumArtifactsInWorld ||
            knowledge.Artifacts.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != knowledge.Artifacts.Count ||
            knowledge.Artifacts.Select(item => item.LotId).Distinct(StringComparer.Ordinal).Count() != knowledge.Artifacts.Count ||
            knowledge.Artifacts.GroupBy(item => item.CreatorId).Any(group => group.Count() > AgentKnowledgeRules.MaximumArtifactsPerCreator) ||
            knowledge.WritingProjects.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != knowledge.WritingProjects.Count ||
            knowledge.WritingProjects.Select(item => item.ActorId).Distinct(StringComparer.Ordinal).Count() != knowledge.WritingProjects.Count)
            throw new InvalidDataException("Community knowledge has invalid local identities or bounds.");
    }

    internal static void ValidateLocalRecipes(WorldKnowledgeState knowledge)
    {
        if (knowledge.Recipes is null || knowledge.Recipes.Any(item => item is null) ||
            knowledge.Recipes.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != knowledge.Recipes.Count ||
            knowledge.Recipes.Select(item => (item.OwnerId, item.RecipeId)).Distinct().Count() != knowledge.Recipes.Count ||
            knowledge.Recipes.GroupBy(item => item.OwnerId).Any(group => group.Count() > AgentKnowledgeRules.MaximumRecipesPerAgent))
            throw new InvalidDataException("Community recipe knowledge has invalid local identities or bounds.");
    }

    internal static void ValidateLocalAgentCommunity(AgentState person)
    {
        if (person.Lesson is { } lesson) ValidateLocalLesson(lesson);
        if (person.Housing is { } housing) ValidateLocalHousing(housing);
        if (person.Skills is { } skills) ValidateLocalSkills(person.AgentId, skills);
        if (person.SocialStanding is { } standing) ValidateLocalStanding(person.AgentId, standing);
        ValidateProficiency(person);
    }
    internal void RetireOrderLinks(HashSet<string> retired) => Commit(State with
    {
        WritingProjects = State.WritingProjects.Select(project => retired.Contains(project.OrderInstructionId ?? "")
            ? project with { OrderInstructionId = null } : project).ToArray(),
        Artifacts = State.Artifacts.Select(artifact => retired.Contains(artifact.OrderInstructionId ?? "")
            ? artifact with { OrderInstructionId = null } : artifact).ToArray(),
    });
}
