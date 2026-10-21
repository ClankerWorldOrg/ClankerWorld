using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static OrderTask? ParseKnowledgeReadOrder(IKnowledgeReadOrdersWorld world, string text)
    {
        text = text.Trim();
        if (text.StartsWith("please ", StringComparison.OrdinalIgnoreCase)) text = text[7..].TrimStart();
        if (text.Equals("read", StringComparison.OrdinalIgnoreCase))
            return OrderStore.CreateOrderTask(OrderStore.CreateReadKnowledgeOrderDefinition(), "queued", 1, 0, "knowledge_reads", false);
        if (!text.StartsWith("read ", StringComparison.OrdinalIgnoreCase)) return null;
        var subject = text[5..].Trim();
        if (subject.StartsWith("a ", StringComparison.OrdinalIgnoreCase)) subject = subject[2..];
        else if (subject.StartsWith("my ", StringComparison.OrdinalIgnoreCase)) subject = subject[3..];
        var completeSubject = subject;
        string? kind = null;
        foreach (var (name, value) in new[] { ("field record", "field_record"), ("written record", "field_record"),
                     ("record", "field_record"), ("field map", "field_map"), ("map", "field_map"), ("book", "book") })
        {
            if (!subject.Equals(name, StringComparison.OrdinalIgnoreCase) && !subject.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase)) continue;
            kind = value;
            subject = subject[name.Length..].Trim();
            break;
        }
        string? target = null;
        if (subject.Length != 0)
        {
            var matches = world.Knowledge.Artifacts.Where(item => (kind is null || item.Kind == kind) &&
                (item.Id == subject || item.LotId == subject || (item.Title.Equals(subject, StringComparison.OrdinalIgnoreCase) || item.Title.Equals(completeSubject, StringComparison.OrdinalIgnoreCase)))).ToArray();
            if (matches.Length != 1) return null;
            target = matches[0].Id;
        }
        else if (kind is null) return null;
        return OrderStore.CreateOrderTask(OrderStore.CreateReadKnowledgeOrderDefinition(TargetItemKind: kind, TargetKnowledgeArtifactId: target), "queued", 1, 0, "knowledge_reads", false);
    }


    internal static AgentKnowledgeArtifact? KnowledgeReadOrderArtifact(IKnowledgeReadOrdersWorld world, OwnerQueuedInstruction instruction) => instruction.Task!.TargetKnowledgeArtifactId is { } id
            ? world.Knowledge.Artifacts.FirstOrDefault(item => item.Id == id)
            : world.HeldKnowledgeArtifacts(instruction.TargetAgentId).FirstOrDefault(item =>
                (instruction.Task.TargetItemKind is null || item.Kind == instruction.Task.TargetItemKind) &&
                world.HasUnknownArtifactContents(instruction.TargetAgentId, item));

    internal static string? KnowledgeReadOrderBlocker(IKnowledgeReadOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        if (!world.IsAdult(actor)) return "Only an adult or elder resident can read this written item.";
        if (world.KnowledgeWritingFor(actor) is not null) return "Cancel this read order to finish the current writing work first.";
        var artifact = world.KnowledgeReadOrderArtifact(instruction);
        if (artifact is null) return instruction.Task!.TargetKnowledgeArtifactId is null
            ? "Waiting for a personally held written item with something new to read."
            : "The named written item is no longer available.";
        if (!world.HeldKnowledgeArtifacts(actor).Any(item => item.Id == artifact.Id))
            return "The named written item must be personally owned, carried and available to read.";
        if (!world.HasUnknownArtifactContents(actor, artifact))
            return artifact.Facts.Any(fact => !world.KnowsMapFact(actor, fact.Position)) ||
                artifact.Recipes.Any(recipe => !world.KnowsRecipe(actor, recipe.RecipeId))
                ? "The agent's knowledge ledger is full."
                : "The agent already knows every site and recipe written in this item.";
        return null;
    }


    internal static CognitionCandidate? KnowledgeReadOrderCandidate(IKnowledgeReadOrdersWorld world, OwnerQueuedInstruction instruction) => world.KnowledgeReadOrderBlocker(instruction) is null && world.KnowledgeReadOrderArtifact(instruction) is { } artifact
            ? new("read_knowledge", $"Read {artifact.Title}; learn only its actual written sites and recipes.", 0, artifact.Id) : null;

    internal static void ExecuteKnowledgeReadOrder(IKnowledgeReadOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        if (world.KnowledgeReadOrderBlocker(instruction) is { } blocker)
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, blocker);
            return;
        }
        var artifact = world.KnowledgeReadOrderArtifact(instruction)!;
        var actor = instruction.TargetAgentId;
        var before = world.Knowledge.Facts.Where(fact => fact.OwnerId == actor).Select(fact => fact.Position).ToHashSet();
        var beforeRecipes = world.Knowledge.Recipes.Where(recipe => recipe.OwnerId == actor).Select(recipe => recipe.RecipeId).ToHashSet(StringComparer.Ordinal);
        var learned = world.ReadIfKnowledgeArtifact(artifact.LotId, artifact.CreatorId, actor);
        if (learned <= 0)
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, "The written item could not add any new knowledge.");
            return;
        }
        var sites = world.Knowledge.Facts.Where(fact => fact.OwnerId == actor && !before.Contains(fact.Position) &&
                fact.Acquisition == "read" && fact.SourceArtifactId == artifact.Id && fact.LearnedTick == world.WorldTick)
            .Select(fact => fact.Position).OrderBy(point => point.Y).ThenBy(point => point.X).ToArray();
        var recipes = world.Knowledge.Recipes.Where(recipe => recipe.OwnerId == actor && !beforeRecipes.Contains(recipe.RecipeId) &&
                recipe.Acquisition == "read" && recipe.SourceArtifactId == artifact.Id && recipe.LearnedTick == world.WorldTick)
            .Select(recipe => recipe.RecipeId).Order(StringComparer.Ordinal).ToArray();
        if (sites.Length + recipes.Length != learned) throw new InvalidDataException("The native read effect lost its learned contents.");
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        current = OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(current, current.Task!.UpdateDefinition(definition => definition switch
{
    ReadKnowledgeOrderDefinition task => OrderStore.PrepareExecuteKnowledgeReadOrderReadKnowledgeOrderDefinition(task, artifact.Id, OrderStore.CreateOwnerKnowledgeReadCompletion(world.WorldTick, sites, recipes)),
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
})
);
        world.Orders.RecordKnowledgeReadOrder(current.IdempotencyKey, current);
        world.CreditOrderEffect(current, Identity.Parse(world.KnowledgeReadOrderEffectId(current)), 1);
    }

}
