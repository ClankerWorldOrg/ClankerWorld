using ClankerWorld.Simulation.Kernel;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;


/// <summary>
/// An agent-scoped account of a map fact. DiscovererId remains the original
/// observer when the fact is later shared or read from a physical artifact.
/// </summary>
public sealed record AgentKnowledgeFact(
    string Id,
    string OwnerId,
    string DiscovererId,
    GridPoint Position,
    string Terrain,
    IReadOnlyList<string> ResourceKinds,
    long LearnedTick,
    string Acquisition,
    string? SourceAgentId = null,
    string? SourceArtifactId = null);

/// <summary>An exact paid input, retained after its physical lot has been consumed.</summary>
public sealed record AgentKnowledgeMaterial(Identity ReservationId, string LotId, string ItemKind, int Quantity);

/// <summary>Writing is physical work on reserved personal materials, paused between actual work steps.</summary>
public sealed record AgentKnowledgeWritingProject(
    string Id, string ActorId, string Kind, string CandidateId, string? SourceArtifactId,
    IReadOnlyList<AgentKnowledgeFact> Facts, long StartedTick, long LastWorkedTick, int WorkDone,
    IReadOnlyList<AgentKnowledgeMaterial> Materials)
{
    [JsonRequired] public IReadOnlyList<AgentRecipeKnowledge> Recipes { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? OrderInstructionId { get; init; }
    [JsonIgnore] public IReadOnlyList<Identity> MaterialReservationIds => Materials.Select(item => item.ReservationId).ToArray();
    [JsonIgnore] public int WorkRequired => AgentKnowledgeRules.WritingWork(Kind);
}

/// <summary>A bounded, physical map, record or book containing its author's actual learned facts.</summary>
public sealed record AgentKnowledgeArtifact(
    string Id,
    string CreatorId,
    string LotId,
    string Kind,
    string Title,
    long CreatedTick,
    IReadOnlyList<AgentKnowledgeFact> Facts)
{
    public string? WritingProjectId { get; init; }
    public IReadOnlyList<AgentKnowledgeMaterial> Materials { get; init; } = [];
    public string? SourceArtifactId { get; init; }
    [JsonRequired] public IReadOnlyList<AgentRecipeKnowledge> Recipes { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? OrderInstructionId { get; init; }
    [JsonIgnore] public IReadOnlyList<Identity> MaterialReservationIds => Materials.Select(item => item.ReservationId).ToArray();
}

/// <summary>Saved personal knowledge and the bounded artifacts carrying it.</summary>
public sealed record WorldKnowledgeState(
    IReadOnlyList<AgentKnowledgeFact> Facts,
    IReadOnlyList<AgentKnowledgeArtifact> Artifacts)
{
    [JsonRequired] public IReadOnlyList<AgentKnowledgeWritingProject> WritingProjects { get; init; } = [];
    [JsonRequired] public IReadOnlyList<AgentKnowledgeFact> EarlierFacts { get; init; } = [];
    [JsonRequired] public IReadOnlyList<AgentRecipeKnowledge> Recipes { get; init; } = [];
    public static WorldKnowledgeState Empty { get; } = new([], []);
}

/// <summary>A person's recipe account, grounded in paid production or an actual written source.</summary>
public sealed record AgentRecipeKnowledge(string Id, string OwnerId, string RecipeId, string DiscovererId,
    long LearnedTick, string Acquisition, string SourceProductionJobId,
    string? SourceAgentId = null, string? SourceArtifactId = null);
internal static partial class AgentKnowledgeRules
{
    public const int MaximumFactsPerAgent = 128;
    public const int MaximumArtifactsPerCreator = 8;
    public const int MaximumArtifactsInWorld = 512;
    public const int MaximumFactsPerArtifact = 9;
    public const int MaximumResourceKindsPerFact = 4;
    public const int MaximumEarlierFactsPerAgent = MaximumFactsPerArtifact * (MaximumArtifactsPerCreator + 1);

    public static bool IsArtifactKind(string kind) => kind is "field_record" or "field_map" or "book";
    public static int WritingWork(string kind) => kind switch { "field_record" => 4, "field_map" => 6, "book" => 12, _ => 0 };
    public static IReadOnlyList<(string Kind, int Quantity)> WritingMaterials(string kind) => kind == "book"
        ? [("paper", 2), ("cloth", 1)] : [("paper", 1)];
    public static string MaterialPurpose(string projectId, string kind) => $"knowledge_writing:{projectId}:{kind}";
    internal static string WritingReceiptId(string artifactId) =>
        DerivedId.Create("writing:artifact:" + artifactId, "writing:artifact:sha256:");

    public static void Validate(WorldKnowledgeState knowledge, WorldMap map,
        SocietyCheckpoint society, long worldTick)
    {
        if (knowledge.Facts is null || knowledge.Artifacts is null || knowledge.WritingProjects is null || knowledge.EarlierFacts is null || knowledge.Recipes is null ||
            knowledge.Facts.Any(item => item is null) || knowledge.Artifacts.Any(item => item is null) ||
            knowledge.EarlierFacts.Any(item => item is null) ||
            knowledge.WritingProjects.Any(item => item is null || item.Recipes is null || item.Recipes.Any(recipe => recipe is null)) ||
            knowledge.Artifacts.Any(item => !BoundedText.IsValid(item.Id, DerivedId.MaximumLength) || item.Facts is null || item.Facts.Any(fact => fact is null) || item.Recipes is null || item.Recipes.Any(recipe => recipe is null)))
            throw new InvalidDataException("The agent knowledge collections are missing or contain null records.");
        var agents = society.Agents.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        CommunitySystem.ValidateLocalKnowledge(knowledge);
        if (knowledge.Facts.Count > checked(agents.Count * MaximumFactsPerAgent) || knowledge.WritingProjects.Count > agents.Count)
            throw new InvalidDataException("Agent knowledge exceeds the current population bounds.");
        var artifacts = knowledge.Artifacts.ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var fact in knowledge.Facts)
            if (!ValidFact(fact, worldTick))
                throw new InvalidDataException("The agent map-knowledge ledger contains an invalid fact or provenance.");
        if (knowledge.EarlierFacts.Count > 0)
        {
            var earlierKeys = knowledge.EarlierFacts.Select(FactVersionKey).ToHashSet();
            var liveKeys = knowledge.Facts.Select(FactVersionKey).ToHashSet();
            var references = FactReferencesFor(knowledge);
            var currentSites = knowledge.Facts.ToDictionary(fact => (fact.OwnerId, fact.Position));
            if (knowledge.EarlierFacts.Count > checked(agents.Count * MaximumEarlierFactsPerAgent) ||
                knowledge.EarlierFacts.GroupBy(fact => fact.OwnerId).Any(group => group.Count() > MaximumEarlierFactsPerAgent) ||
                earlierKeys.Count != knowledge.EarlierFacts.Count || knowledge.EarlierFacts.Any(fact =>
                    !ValidFact(fact, worldTick) || !currentSites.TryGetValue((fact.OwnerId, fact.Position), out var current) ||
                    current.Id != fact.Id || current.LearnedTick < fact.LearnedTick ||
                    liveKeys.Contains(FactVersionKey(fact)) || !references.Contains(fact)))
                throw new InvalidDataException("Earlier knowledge must be bounded, distinct and retained only for an existing written snapshot.");
        }
        var recordedFacts = knowledge.EarlierFacts.Count == 0 ? knowledge.Facts
            : knowledge.Facts.Concat(knowledge.EarlierFacts).ToArray();
        var usedReservations = new HashSet<string>(StringComparer.Ordinal);
        var usedProjects = new HashSet<string>(StringComparer.Ordinal);
        foreach (var artifact in knowledge.Artifacts)
        {
            var lot = society.Inventory.Lots.FirstOrDefault(item => item.Id == artifact.LotId);
            if (!BoundedText.IsValid(artifact.Id, DerivedId.MaximumLength) || !BoundedText.IsValid(artifact.LotId, DerivedId.MaximumLength) || !agents.Contains(artifact.CreatorId) ||
                !IsArtifactKind(artifact.Kind) || !BoundedText.IsValid(artifact.Title, 64) ||
                artifact.CreatedTick < 0 || artifact.CreatedTick > worldTick ||
                lot is null || lot.ItemKind != artifact.Kind || lot.Quantity != 1 ||
                !ValidFacts(artifact.Facts, artifact.CreatorId, artifact.CreatedTick, artifact.SourceArtifactId) ||
                artifact.Facts.Count + artifact.Recipes.Count == 0 ||
                artifact.Kind == "field_record" && artifact.Facts.Count > 1 ||
                !ValidSource(artifact.SourceArtifactId, artifact.Kind, artifact.Facts, artifact.CreatedTick) ||
                !ValidateMaterials(artifact.WritingProjectId, artifact.CreatorId, artifact.Kind, artifact.Materials, completed: true))
                throw new InvalidDataException("An agent knowledge artifact has invalid facts, inputs or physical identity.");
        }
        foreach (var project in knowledge.WritingProjects)
        {
            if (!BoundedText.IsValid(project.Id, DerivedId.MaximumLength) || !agents.Contains(project.ActorId) ||
                society.GetAgent(project.ActorId).Status != SocietyAgentStatus.Active || !IsArtifactKind(project.Kind) ||
                project.StartedTick < 0 || project.StartedTick > project.LastWorkedTick || project.LastWorkedTick > worldTick ||
                project.WorkDone < 0 || project.WorkDone >= WritingWork(project.Kind) ||
                project.CandidateId != (project.SourceArtifactId is null ? "knowledge_write:" + project.Kind : "knowledge_copy:" + project.SourceArtifactId) ||
                !ValidFacts(project.Facts, project.ActorId, project.StartedTick, project.SourceArtifactId) ||
                project.Facts.Count + project.Recipes.Count == 0 ||
                project.Kind == "field_record" && project.Facts.Count > 1 ||
                !ValidSource(project.SourceArtifactId, project.Kind, project.Facts, project.StartedTick) ||
                !ValidateMaterials(project.Id, project.ActorId, project.Kind, project.Materials, completed: false))
                throw new InvalidDataException("An agent knowledge-writing project has invalid work, facts or reserved inputs.");
        }
        // A source chain must end at an actual firsthand observation, even when several generations of copies exist.
        var validatedAncestry = new HashSet<string>(StringComparer.Ordinal);
        foreach (var artifact in knowledge.Artifacts)
            CheckAncestry(artifact, new HashSet<string>(StringComparer.Ordinal));

        bool ValidFact(AgentKnowledgeFact fact, long latestTick) => fact is not null && BoundedText.IsValid(fact.Id, DerivedId.MaximumLength) &&
            agents.Contains(fact.OwnerId) && agents.Contains(fact.DiscovererId) &&
            map.Contains(fact.Position) && map.IsPassable(fact.Position) &&
            Enum.TryParse<TerrainKind>(fact.Terrain, ignoreCase: false, out _) &&
            fact.ResourceKinds is not null && fact.ResourceKinds.Count <= MaximumResourceKindsPerFact &&
            fact.ResourceKinds.All(ContentPackageRules.IsValidContentLocalId) && fact.ResourceKinds.Distinct(StringComparer.Ordinal).Count() == fact.ResourceKinds.Count &&
            fact.LearnedTick >= 0 && fact.LearnedTick <= latestTick &&
            (fact.Acquisition == "firsthand" && fact.OwnerId == fact.DiscovererId && fact.SourceAgentId is null && fact.SourceArtifactId is null ||
             fact.Acquisition is "shared" or "read" && fact.SourceAgentId is { } source && agents.Contains(source) &&
             fact.SourceArtifactId is { } sourceArtifact && artifacts.TryGetValue(sourceArtifact, out var original) &&
             original.CreatedTick <= fact.LearnedTick && original.Facts is not null && original.Facts.Any(other => SameDiscovery(fact, other)));

        bool ValidFacts(IReadOnlyList<AgentKnowledgeFact>? facts, string actor, long tick, string? sourceArtifactId) =>
            facts is { Count: >= 0 and <= MaximumFactsPerArtifact } &&
            facts.Select(item => item?.Position).Distinct().Count() == facts.Count &&
            facts.All(fact => fact is not null && fact.OwnerId == actor && ValidFact(fact, tick) &&
                (sourceArtifactId is null
                    ? recordedFacts.Any(learned => SameLearnedFact(learned, fact))
                    : artifacts.TryGetValue(sourceArtifactId, out var source) &&
                      fact.Acquisition == "read" && fact.SourceArtifactId == sourceArtifactId &&
                      fact.SourceAgentId == source.CreatorId &&
                      recordedFacts.Any(learned => learned.OwnerId == actor && learned.Id == fact.Id &&
                          learned.LearnedTick <= fact.LearnedTick && learned.Position == fact.Position)));

        bool ValidSource(string? source, string kind, IReadOnlyList<AgentKnowledgeFact> facts, long tick) => source is null ||
            artifacts.TryGetValue(source, out var original) && original.Kind == kind && original.CreatedTick <= tick &&
            original.Facts is not null && original.Facts.Count == facts.Count &&
            original.Facts.All(fact => facts.Any(copied => SameDiscovery(fact, copied)));

        bool ValidateMaterials(string? projectId, string actor, string kind, IReadOnlyList<AgentKnowledgeMaterial>? materials, bool completed)
        {
            if (!BoundedText.IsValid(projectId, DerivedId.MaximumLength) || !usedProjects.Add(projectId!) || materials is not { Count: >= 1 and <= 3 } ||
                materials.Any(item => item is null || !BoundedText.IsValid(item.ReservationId, DerivedId.MaximumLength) || !BoundedText.IsValid(item.LotId, int.MaxValue) ||
                    item.Quantity <= 0 || !usedReservations.Add(item.ReservationId))) return false;
            var expected = WritingMaterials(kind);
            if (materials.Any(item => !expected.Any(input => input.Kind == item.ItemKind)) ||
                expected.Any(input => materials.Where(item => item.ItemKind == input.Kind).Sum(item => (long)item.Quantity) != input.Quantity)) return false;
            return materials.All(input =>
            {
                var reservation = society.Inventory.Reservations.FirstOrDefault(item => item.Id == input.ReservationId);
                if (reservation is null || reservation.OwnerId != actor || reservation.LotId != input.LotId ||
                    reservation.Quantity != input.Quantity || reservation.Purpose != MaterialPurpose(projectId!, input.ItemKind) ||
                    reservation.ExpiryTick != long.MaxValue || !reservation.IsExclusive ||
                    reservation.State != (completed ? InventoryReservationState.Completed : InventoryReservationState.Reserved)) return false;
                if (completed) return true;
                var lot = society.Inventory.Lots.FirstOrDefault(item => item.Id == input.LotId);
                return lot is not null && lot.OwnerId == actor && lot.ItemKind == input.ItemKind && lot.Quantity >= input.Quantity &&
                    lot.ContainerLotId is null && lot.DeliveryBuildingId is null && PersonalEquipmentRules.IsCarried(lot, actor);
            });
        }

        void CheckAncestry(AgentKnowledgeArtifact artifact, HashSet<string> path)
        {
            if (validatedAncestry.Contains(artifact.Id)) return;
            if (!path.Add(artifact.Id)) throw new InvalidDataException("Knowledge artifacts contain cyclic provenance.");
            foreach (var source in artifact.Facts.Select(fact => fact.SourceArtifactId)
                         .Concat(artifact.Recipes.Select(recipe => recipe.SourceArtifactId)).Append(artifact.SourceArtifactId)
                         .Where(id => id is not null).Distinct(StringComparer.Ordinal))
            {
                if (!artifacts.TryGetValue(source!, out var original))
                    throw new InvalidDataException("Knowledge artifacts refer to a missing written source.");
                CheckAncestry(original, path);
            }
            path.Remove(artifact.Id);
            validatedAncestry.Add(artifact.Id);
        }
    }

    internal static bool ReferencesFact(WorldKnowledgeState knowledge, AgentKnowledgeFact fact) =>
        FactReferencesFor(knowledge).Contains(fact);

    internal static WorldKnowledgeState PruneEarlierFacts(WorldKnowledgeState knowledge) => CommunitySystem.PruneEarlierFacts(knowledge);

    internal static IEnumerable<AgentKnowledgeFact> RetainedEarlierFacts(WorldKnowledgeState knowledge) =>
        knowledge.EarlierFacts.Where(FactReferencesFor(knowledge).Contains);

    private sealed record FactVersion(string Id, string Owner, string Discoverer, GridPoint Position, string Terrain,
        long Learned, string Acquisition, string? Agent, string? Artifact, string Resources);

    private static FactVersion FactVersionKey(AgentKnowledgeFact fact) =>
        new(fact.Id, fact.OwnerId, fact.DiscovererId, fact.Position, fact.Terrain, fact.LearnedTick, fact.Acquisition,
            fact.SourceAgentId, fact.SourceArtifactId, string.Join('\0', fact.ResourceKinds ?? []));

    private static (string Id, string Owner, GridPoint Position) CopySiteKey(AgentKnowledgeFact fact) =>
        (fact.Id, fact.OwnerId, fact.Position);

    private sealed record FactReferences(HashSet<FactVersion> Exact,
        Dictionary<(string Id, string Owner, GridPoint Position), long> Copies)
    {
        public bool Contains(AgentKnowledgeFact fact) => Exact.Contains(FactVersionKey(fact)) ||
            Copies.TryGetValue(CopySiteKey(fact), out var copiedTick) && fact.LearnedTick <= copiedTick;
    }

    private static FactReferences FactReferencesFor(WorldKnowledgeState knowledge)
    {
        var references = new FactReferences([], []);
        foreach (var artifact in knowledge.Artifacts) Add(artifact.Facts ?? [], artifact.SourceArtifactId);
        foreach (var project in knowledge.WritingProjects) Add(project.Facts ?? [], project.SourceArtifactId);
        return references;

        void Add(IReadOnlyList<AgentKnowledgeFact> facts, string? source)
        {
            foreach (var fact in facts.Where(fact => fact is not null))
            {
                if (source is null) references.Exact.Add(FactVersionKey(fact));
                else
                {
                    var key = CopySiteKey(fact);
                    references.Copies[key] = Math.Max(references.Copies.GetValueOrDefault(key), fact.LearnedTick);
                }
            }
        }
    }

    internal static bool SameSiteKnowledge(AgentKnowledgeFact first, AgentKnowledgeFact second) =>
        first.Position == second.Position && first.Terrain == second.Terrain &&
        first.ResourceKinds is not null && second.ResourceKinds is not null && first.ResourceKinds.SequenceEqual(second.ResourceKinds, StringComparer.Ordinal);
    internal static bool SameDiscovery(AgentKnowledgeFact first, AgentKnowledgeFact second) =>
        first.DiscovererId == second.DiscovererId && SameSiteKnowledge(first, second);
    internal static bool SameLearnedFact(AgentKnowledgeFact first, AgentKnowledgeFact second) => SameDiscovery(first, second) &&
        first.Id == second.Id && first.OwnerId == second.OwnerId && first.LearnedTick == second.LearnedTick &&
        first.Acquisition == second.Acquisition && first.SourceAgentId == second.SourceAgentId && first.SourceArtifactId == second.SourceArtifactId;
    public const int MaximumRecipesPerAgent = 128;
    public static int RecipeCapacity(string kind) => kind switch { "field_record" => 1, "book" => 9, _ => 0 };
    public static bool SameRecipe(AgentRecipeKnowledge first, AgentRecipeKnowledge second) =>
        first.RecipeId == second.RecipeId && first.DiscovererId == second.DiscovererId &&
        first.SourceProductionJobId == second.SourceProductionJobId;

    public static void ValidateRecipes(WorldKnowledgeState knowledge, SocietyCheckpoint society,
        DeclarativeWorldContentState content, WorldContentSimulationState simulation, long worldTick)
    {
        if (knowledge.Recipes is null || knowledge.Recipes.Any(item => item is null))
            throw new InvalidDataException("Recipe knowledge is missing or incomplete.");
        var agents = society.Agents.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var definitions = content.Recipes.Select(item => item.CanonicalId).ToHashSet(StringComparer.Ordinal);
        if (simulation.ProductionJobs.Select(item => item.JobId).Distinct(StringComparer.Ordinal).Count() != simulation.ProductionJobs.Count)
            throw new InvalidDataException("Recipe production evidence contains duplicate job identities.");
        var jobs = simulation.ProductionJobs.ToDictionary(item => item.JobId, StringComparer.Ordinal);
        var artifacts = knowledge.Artifacts.ToDictionary(item => item.Id, StringComparer.Ordinal);
        CommunitySystem.ValidateLocalRecipes(knowledge);
        if (knowledge.Recipes.Count > checked(agents.Count * MaximumRecipesPerAgent) ||
            knowledge.Recipes.Any(item => !ValidRecipe(item, worldTick)))
            throw new InvalidDataException("A person's recipe knowledge has invalid ownership, bounds or evidence.");
        foreach (var artifact in knowledge.Artifacts)
            if (!ValidContents(artifact.Recipes, artifact.CreatorId, artifact.Kind, artifact.CreatedTick, artifact.SourceArtifactId))
                throw new InvalidDataException("Written recipes differ from their author's knowledge or copied source.");
        foreach (var project in knowledge.WritingProjects)
            if (!ValidContents(project.Recipes, project.ActorId, project.Kind, project.StartedTick, project.SourceArtifactId))
                throw new InvalidDataException("Writing recipes must come from the writer's existing knowledge.");

        bool ValidRecipe(AgentRecipeKnowledge recipe, long latestTick) =>
            BoundedText.IsValid(recipe.Id, 160) && agents.Contains(recipe.OwnerId) && agents.Contains(recipe.DiscovererId) &&
            definitions.Contains(recipe.RecipeId) && recipe.LearnedTick >= 0 && recipe.LearnedTick <= latestTick &&
            BoundedText.IsValid(recipe.SourceProductionJobId, 128) && jobs.TryGetValue(recipe.SourceProductionJobId, out var job) && job.State == WorldProductionJobState.Completed &&
            job.WorkerId == recipe.DiscovererId && job.RecipeId == recipe.RecipeId && job.CompletionTick <= recipe.LearnedTick &&
            (recipe.Acquisition == "practice" && recipe.OwnerId == recipe.DiscovererId && recipe.SourceAgentId is null && recipe.SourceArtifactId is null ||
             recipe.Acquisition is "read" or "shared" && recipe.SourceAgentId is { } source && agents.Contains(source) &&
             recipe.SourceArtifactId is { } sourceId && artifacts.TryGetValue(sourceId, out var artifact) &&
             artifact.CreatedTick <= recipe.LearnedTick && artifact.Recipes.Any(original => SameRecipe(recipe, original)));

        bool ValidContents(IReadOnlyList<AgentRecipeKnowledge> recipes, string owner, string kind, long tick, string? sourceId)
        {
            if (recipes is null || recipes.Count > RecipeCapacity(kind) || recipes.Any(item => item is null) ||
                recipes.Select(item => item.RecipeId).Distinct(StringComparer.Ordinal).Count() != recipes.Count) return false;
            if (sourceId is not null && (!artifacts.TryGetValue(sourceId, out var source) ||
                source.Recipes.Count != recipes.Count || source.Recipes.Any(item => !recipes.Any(copy => SameRecipe(item, copy))))) return false;
            return recipes.All(recipe => recipe.OwnerId == owner && ValidRecipe(recipe, tick) &&
                (sourceId is null ? knowledge.Recipes.Contains(recipe) :
                    recipe.Acquisition == "read" && recipe.SourceArtifactId == sourceId &&
                    recipe.SourceAgentId == artifacts[sourceId].CreatorId && knowledge.Recipes.Any(known =>
                        known.OwnerId == owner && known.Id == recipe.Id && known.RecipeId == recipe.RecipeId && known.LearnedTick <= recipe.LearnedTick)));
        }
    }
}
