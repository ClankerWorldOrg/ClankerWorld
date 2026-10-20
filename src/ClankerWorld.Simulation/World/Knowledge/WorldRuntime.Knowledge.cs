using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string KnowledgeSharePrefix = "knowledge_share:";

    private bool RecordKnowledgeFact(string actor, GridPoint position)
    {
        if (!map.IsPassable(position) || map.TerrainKindAt(position) is not { } terrain)
            return false;
        var resourcesAtTile = map.Resources.Where(item => item.Position == position)
            .Select(FoodKnowledgeKind).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).Take(AgentKnowledgeRules.MaximumResourceKindsPerFact).ToArray();
        var previous = knowledge.Facts.FirstOrDefault(fact => fact.OwnerId == actor && fact.Position == position);
        if (previous is not null && previous.Terrain == terrain.ToString() &&
            previous.ResourceKinds.SequenceEqual(resourcesAtTile, StringComparer.Ordinal)) return false;
        if (previous is null && knowledge.Facts.Count(fact => fact.OwnerId == actor) >= AgentKnowledgeRules.MaximumFactsPerAgent)
            return false;
        var fact = new AgentKnowledgeFact(
            previous?.Id ?? KnowledgeFactId(actor, position), actor, actor, position,
            terrain.ToString(), resourcesAtTile, WorldTick, "firsthand");
        communitySystem.RecordObservedFact(fact, previous);
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent(new AgentKnowledgeLearnedEventPayload(actor));
        return true;
    }

    private void AddKnowledgeCandidates(
        List<CognitionCandidate> candidates,
        string actor,
        AgentState person)
    {
        if (NeedsUrgentWarmth(person))
            return;

        AddKnowledgeWritingCandidates(candidates, actor, person);
        AddKnowledgeStorageCandidates(candidates, actor, person);
        foreach (var artifact in HeldKnowledgeArtifacts(actor))
        {
            foreach (var target in agents.Values
                .Where(item => item.AgentId != actor && IsAdult(item.AgentId) &&
                    IsWithinInteractionRange(person.Position, item.Position, 1))
                 .OrderBy(item => item.AgentId, StringComparer.Ordinal))
            {
                if (!HasUnknownArtifactContents(target.AgentId, artifact))
                    continue;
                var targetName = society.Checkpoint.GetAgent(target.AgentId).Name;
                var plan = new KnowledgeReadingActionPlan(actor, artifact, target.AgentId, true);
                if (CheckActionPlan(plan).Allowed) candidates.Add(new CognitionCandidate(
                    plan.CandidateId,
                    $"Share {artifact.Title} with {targetName}; they can learn the written sites and recipes they do not already know, and you keep the physical artifact.",
                    52)
                { ActionPlan = plan });
            }
        }
    }

    private void ReadTradedKnowledge(string firstPartyId, string secondPartyId, string firstLotId, string secondLotId)
    {
        ReadIfKnowledgeArtifact(firstLotId, firstPartyId, secondPartyId);
        ReadIfKnowledgeArtifact(secondLotId, secondPartyId, firstPartyId);
    }

    private int ReadIfKnowledgeArtifact(string lotId, string sourceAgentId, string recipientId, AgentKnowledgeArtifact? selectedArtifact = null)
    {
        var artifact = selectedArtifact ?? knowledge.Artifacts.FirstOrDefault(item => item.LotId == lotId);
        if (artifact is null || !HeldKnowledgeArtifacts(recipientId).Any(item => item.Id == artifact.Id))
            return 0;
        var learned = LearnArtifactFacts(recipientId, sourceAgentId, artifact, "read") + LearnArtifactRecipes(recipientId, sourceAgentId, artifact, "read");
        if (learned > 0)
        {
            checkpointSchemaVersion = StateSchemaVersion;
            AppendEvent(new AgentKnowledgeArtifactReadEventPayload(recipientId, sourceAgentId, recipientId, artifact.Id, learned));
        }
        return learned;
    }

    private int LearnArtifactFacts(string recipientId, string sourceAgentId, AgentKnowledgeArtifact artifact, string acquisition) =>
        communitySystem.LearnArtifactFacts(recipientId, sourceAgentId, artifact, acquisition, WorldTick);

    private bool KnowsMapFact(string agentId, GridPoint position) =>
        knowledge.Facts.Any(fact => fact.OwnerId == agentId && fact.Position == position);

    private CognitionKnowledgeFact[] KnownMapFactsForCognition(string agentId) =>
        knowledge.Facts.Where(fact => fact.OwnerId == agentId)
            .OrderByDescending(fact => fact.LearnedTick)
            .ThenBy(fact => fact.Id, StringComparer.Ordinal)
            .Take(16)
            .Select(fact => new CognitionKnowledgeFact(
                fact.Position.X, fact.Position.Y, fact.Terrain, fact.ResourceKinds,
                CognitionDiscovererId(fact.DiscovererId), fact.LearnedTick, fact.Acquisition))
            .ToArray();

    private AgentKnowledgeArtifact[] HeldKnowledgeArtifacts(string ownerId)
    {
        var heldLotIds = society.Checkpoint.Inventory.Lots
            .Where(item => item.OwnerId == ownerId && PersonalEquipmentRules.IsCarried(item, ownerId) &&
                item.DeliveryBuildingId is null && item.ContainerLotId is null &&
                AgentKnowledgeRules.IsArtifactKind(item.ItemKind) && AvailableLotQuantity(item) == 1)
            .Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        return knowledge.Artifacts.Where(item => heldLotIds.Contains(item.LotId))
            .OrderBy(item => item.CreatedTick).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
    }

    private static string KnowledgeFactId(string ownerId, GridPoint position) => CommunitySystem.KnowledgeFactId(ownerId, position);

    private static string CognitionDiscovererId(string discovererId) =>
        // The saved ledger retains the actual identity and provenance. Only the
        // bounded model-facing reference needs a stable alias for long IDs.
        DerivedId.Create(discovererId, "agent-sha256:");

    private static string FoodKnowledgeKind(MapResource resource) => resource.Kind switch
    {
        "fruit" => "fruit",
        "food" when resource.NaturalObjectKind == "wild_greens" => "wild_greens",
        "food" => "berries",
        _ => resource.Kind,
    };
}
