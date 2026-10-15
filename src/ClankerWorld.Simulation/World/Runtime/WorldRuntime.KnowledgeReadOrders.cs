using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string KnowledgeReadOrderTask = "read one personally held written record, map or book and learn only its written contents";

    private OrderTask? ParseKnowledgeReadOrder(string text) =>
        OrderSystem.ParseKnowledgeReadOrder(KnowledgeReadOrdersWorld, text);

    private AgentKnowledgeArtifact? KnowledgeReadOrderArtifact(OwnerQueuedInstruction instruction) =>
        OrderSystem.KnowledgeReadOrderArtifact(KnowledgeReadOrdersWorld, instruction);

    private string? KnowledgeReadOrderBlocker(OwnerQueuedInstruction instruction) =>
        OrderSystem.KnowledgeReadOrderBlocker(KnowledgeReadOrdersWorld, instruction);

    private CognitionCandidate? KnowledgeReadOrderCandidate(OwnerQueuedInstruction instruction) =>
        OrderSystem.KnowledgeReadOrderCandidate(KnowledgeReadOrdersWorld, instruction);

    private void ExecuteKnowledgeReadOrder(OwnerQueuedInstruction instruction) =>
        OrderSystem.ExecuteKnowledgeReadOrder(KnowledgeReadOrdersWorld, instruction);

    private static string KnowledgeReadOrderEffectId(OwnerQueuedInstruction instruction)
    {
        var completion = instruction.Task!.KnowledgeReadCompletion!;
        return DerivedId.Hash(
            $"{instruction.InstructionId}|{instruction.TargetAgentId}|{instruction.Task.TargetKnowledgeArtifactId}|{completion.WorldTick}|" +
            string.Join(';', completion.LearnedSites.Select(point => $"{point.X},{point.Y}")) + "|" +
            string.Join(';', completion.LearnedRecipes), "read-order:");
    }

    private static bool IsValidKnowledgeReadOrderShape(OrderTask order, OwnerQueuedInstruction instruction, long worldTick) =>
        order.TargetItemKind is null or "field_record" or "field_map" or "book" &&
        order.TargetAgentId is null && order.TargetFoodKind is null && order.TargetResourceId is null && order.TargetPosition is null &&
        order.RequestedUnits == 1 && order.CompletedUnits is >= 0 and <= 1 && !order.RepeatUntilCancelled && !order.QuantityIsExplicit &&
        order.ProgressUnit == "knowledge_reads" && order.Status != "not_understood" &&
        (order.Status == "finished") == (order.CompletedUnits == 1) &&
        (order.TargetKnowledgeArtifactId is null || IsKnowledgeArtifactOrderId(order.TargetKnowledgeArtifactId)) &&
        (order.CompletedUnits == 0 ? order.KnowledgeReadCompletion is null && order.LastEffectId is null :
            order.TargetKnowledgeArtifactId is not null && order.KnowledgeReadCompletion is { } completion &&
            completion.WorldTick >= instruction.SubmittedTick && completion.WorldTick <= worldTick &&
            completion.LearnedSites is { Count: >= 0 and <= AgentKnowledgeRules.MaximumFactsPerArtifact } &&
            completion.LearnedRecipes is { Count: <= AgentKnowledgeRules.MaximumFactsPerArtifact } &&
            completion.LearnedSites.Count + completion.LearnedRecipes.Count > 0 &&
            completion.LearnedRecipes.All(IsKnowledgeRecipeOrderId) &&
            completion.LearnedRecipes.Distinct(StringComparer.Ordinal).Count() == completion.LearnedRecipes.Count &&
            completion.LearnedRecipes.SequenceEqual(completion.LearnedRecipes.Order(StringComparer.Ordinal)) &&
            completion.LearnedSites.All(point => point is { X: >= -10_000_000 and <= 10_000_000, Y: >= -10_000_000 and <= 10_000_000 }) &&
            completion.LearnedSites.Distinct().Count() == completion.LearnedSites.Count &&
            completion.LearnedSites.SequenceEqual(completion.LearnedSites.OrderBy(point => point.Y).ThenBy(point => point.X)) &&
            order.LastEffectId == KnowledgeReadOrderEffectId(instruction));

    private static bool IsKnowledgeArtifactOrderId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= DerivedId.MaximumLength &&
        id == id.Trim() && !id.Any(char.IsControl);

    // Canonical recipe IDs follow active content definitions, whose local names have no length cap.
    // ValidateRecipes and the read bindings below check the full definition and native provenance.
    private static bool IsKnowledgeRecipeOrderId(string id) => !string.IsNullOrWhiteSpace(id) &&
        id == id.Trim() && !id.Any(char.IsControl);

    private static void ValidateKnowledgeReadOrderBindings(IEnumerable<OwnerQueuedInstruction> instructions, WorldKnowledgeState knowledge)
    {
        var readingOrders = instructions.Where(instruction => instruction.Task is
        { Action: "read_knowledge", TargetKnowledgeArtifactId: not null }).ToArray();
        if (readingOrders.Length == 0) return;
        var artifacts = knowledge.Artifacts.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var facts = knowledge.Facts.ToDictionary(item => (item.OwnerId, item.Position));
        var recipes = knowledge.Recipes.ToDictionary(item => (item.OwnerId, item.RecipeId));
        foreach (var instruction in readingOrders)
        {
            if (instruction.Task is not { Action: "read_knowledge", TargetKnowledgeArtifactId: { } id } order) continue;
            artifacts.TryGetValue(id, out var artifact);
            if (artifact is not null && order.TargetItemKind is { } kind && kind != artifact.Kind)
                throw new InvalidDataException("A reading order names another kind of written item.");
            if (order.KnowledgeReadCompletion is not { } completion) continue;
            foreach (var site in completion.LearnedSites)
            {
                var written = artifact?.Facts.FirstOrDefault(fact => fact.Position == site);
                if (!facts.TryGetValue((instruction.TargetAgentId, site), out var learned) || learned.LearnedTick < completion.WorldTick ||
                    artifact is not null && written is null || learned.LearnedTick == completion.WorldTick &&
                    (written is null || learned.Id != KnowledgeFactId(instruction.TargetAgentId, site) ||
                     learned.Acquisition != "read" || learned.SourceArtifactId != id || learned.SourceAgentId != artifact!.CreatorId ||
                     !AgentKnowledgeRules.SameDiscovery(learned, written)))
                    throw new InvalidDataException("A reading order does not match its actual learned sites and provenance.");
            }
            foreach (var recipeId in completion.LearnedRecipes)
            {
                var written = artifact?.Recipes.FirstOrDefault(recipe => recipe.RecipeId == recipeId);
                if (written is null || !recipes.TryGetValue((instruction.TargetAgentId, recipeId), out var learned) ||
                    learned.LearnedTick != completion.WorldTick || learned.Id != RecipeKnowledgeId(instruction.TargetAgentId, recipeId) ||
                    learned.Acquisition != "read" || learned.SourceArtifactId != id || learned.SourceAgentId != artifact!.CreatorId ||
                    !AgentKnowledgeRules.SameRecipe(learned, written))
                    throw new InvalidDataException("A reading order does not match its actual learned recipes and provenance.");
            }
        }
    }
}
