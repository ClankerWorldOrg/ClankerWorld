using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static CognitionCandidate? KnowledgeOrderCandidateFor(IKnowledgeOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        var kind = instruction.Task!.TargetKnowledgeKind!;
        if (!world.IsAdult(actor)) return null;
        if (world.KnowledgeWritingFor(actor) is { } project)
            return project.Kind == kind && world.MatchesKnowledgeSource(instruction.Task!, project.SourceArtifactId, project.OrderInstructionId is not null) &&
                (project.OrderInstructionId is null || world.IsCurrentKnowledgeOrderProject(project))
                ? new(instruction.Task!.Action, "Continue the requested writing using its reserved materials and learned contents.", 0) : null;
        if (instruction.Task!.Action == "copy_knowledge")
            return world.MayMakeKnowledgeArtifact(actor) && world.KnowledgeCopySourceFor(instruction) is not null && world.CanSupplyWriting(actor, kind)
                ? new("copy_knowledge", "Prepare a paid copy from the held source and contents you already know.", 0) : null;
        var facts = world.FactsToWrite(actor, kind);
        var recipes = world.RecipesToWrite(actor, kind);
        return world.MayMakeKnowledgeArtifact(actor) && facts.Length + recipes.Length > 0 && !world.AlreadyWrote(actor, kind, facts, recipes) && world.CanSupplyWriting(actor, kind)
            ? new("write_knowledge", "Prepare and write the requested item using real materials and personally learned contents.", 0) : null;
    }


    internal static string KnowledgeOrderBlockedReason(IKnowledgeOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        var kind = instruction.Task!.TargetKnowledgeKind!;
        if (!world.IsAdult(actor)) return "Only an adult can write or copy a record, map or book.";
        if (world.KnowledgeWritingFor(actor) is { } project &&
            (project.Kind != kind || !world.MatchesKnowledgeSource(instruction.Task!, project.SourceArtifactId, project.OrderInstructionId is not null) ||
             project.OrderInstructionId is not null && !world.IsCurrentKnowledgeOrderProject(project)))
            return "Another writing job is in progress. Cancel this order to finish that work first.";
        if (!world.MayMakeKnowledgeArtifact(actor)) return "The writing limit has been reached.";
        if (instruction.Task!.Action == "copy_knowledge") return world.KnowledgeCopyBlockedReason(instruction);
        var facts = world.FactsToWrite(actor, kind);
        var recipes = world.RecipesToWrite(actor, kind);
        if (facts.Length + recipes.Length == 0) return "Waiting for personally learned sites or recipes to write about.";
        if (world.KnowledgeWritingFor(actor) is null && world.AlreadyWrote(actor, kind, facts, recipes))
            return "These contents have already been written. Waiting to learn something new.";
        return kind == "book"
            ? "Waiting for permitted paper and cloth, a reachable supply and carrying space."
            : "Waiting for permitted paper, a reachable supply and carrying space.";
    }


    internal static void ExecuteKnowledgeOrderStep(IKnowledgeOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var kind = instruction.Task!.TargetKnowledgeKind!;
        if (world.KnowledgeOrderCandidateFor(instruction) is null)
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.KnowledgeOrderBlockedReason(instruction));
            return;
        }
        if (world.KnowledgeWritingFor(actor) is null)
        {
            var inventory = world.Society.Inventory;
            var candidate = world.KnowledgeWritePrefix + kind;
            if (instruction.Task!.Action == "copy_knowledge")
            {
                var source = world.KnowledgeCopySourceFor(instruction)!;
                var current = world.Orders.Instructions[instruction.IdempotencyKey];
                world.Orders.RecordKnowledgeOrderStep(instruction.IdempotencyKey, OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(current, current.Task!.UpdateDefinition(definition => definition switch
{
    CopyKnowledgeOrderDefinition task => OrderStore.PrepareExecuteKnowledgeOrderStepCopyKnowledgeOrderDefinition(task, source.Id),
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
})));
                candidate = world.KnowledgeCopyPrefix + source.Id;
            }
            world.ApplyKnowledgeWritingCandidate(actor, person, candidate);
            if (world.KnowledgeWritingFor(actor) is null)
            {
                if (world.Agents[actor].Position == person.Position && world.Society.Inventory == inventory)
                    world.SetOrderStatus(instruction, OrderStatus.Blocked, world.KnowledgeOrderBlockedReason(instruction));
                return;
            }
        }
        var project = world.KnowledgeWritingFor(actor)!;
        if (project.OrderInstructionId is null)
        {
            project = project with { OrderInstructionId = instruction.InstructionId };
            world.ReplaceKnowledgeWriting(project);
            var current = world.Orders.Instructions[instruction.IdempotencyKey];
            world.Orders.RecordKnowledgeOrderStep(instruction.IdempotencyKey, OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(current, current.Task!.UpdateDefinition(definition => definition switch
{
    WriteKnowledgeOrderDefinition task => OrderStore.PrepareExecuteKnowledgeOrderStepWriteKnowledgeOrderDefinition(task, project.Id),
    CopyKnowledgeOrderDefinition task => OrderStore.PrepareExecuteKnowledgeOrderStepCopyKnowledgeOrderDefinitionKnowledgeWritingProjectId(task, project.Id, project.SourceArtifactId),
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
})));
        }
        world.ContinueKnowledgeWriting(project);
        var updated = world.Orders.Instructions[instruction.IdempotencyKey];
        if (world.KnowledgeWritingFor(actor) is null && world.IsActiveOrder(updated.Task!.Status) &&
            updated.Task.CompletedUnits == instruction.Task!.CompletedUnits)
            world.SetOrderStatus(updated, OrderStatus.Blocked, world.KnowledgeOrderBlockedReason(updated));
    }


    internal static bool IsCurrentKnowledgeOrderProject(IKnowledgeOrdersWorld world, AgentKnowledgeWritingProject project) => world.PendingInstructionFor(project.ActorId) is { } instruction && instruction.InstructionId == project.OrderInstructionId &&
        instruction.Task is { } order && IsKnowledgeOrder(order.Action) && order.TargetKnowledgeKind == project.Kind &&
        order.KnowledgeWritingProjectId == project.Id && world.MatchesKnowledgeSource(order, project.SourceArtifactId, bound: true);

    internal static void ClearKnowledgeOrderBinding(IKnowledgeOrdersWorld world, AgentKnowledgeWritingProject project)
    {
        var instruction = world.Orders.Instructions.Values.FirstOrDefault(item => item.InstructionId == project.OrderInstructionId);
        if (instruction?.Task is not { } order || !IsKnowledgeOrder(order.Action) || order.KnowledgeWritingProjectId != project.Id) return;
        world.Orders.RecordClearKnowledgeOrderBinding(instruction.IdempotencyKey, OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(instruction, order.UpdateDefinition(definition => definition switch
{
    WriteKnowledgeOrderDefinition task => OrderStore.PrepareExecuteKnowledgeOrderStepWriteKnowledgeOrderDefinition(task, null),
    CopyKnowledgeOrderDefinition task => OrderStore.PrepareClearKnowledgeOrderBindingCopyKnowledgeOrderDefinition(task, null),
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
})));
    }


    internal static void CreditKnowledgeOrderCompletion(IKnowledgeOrdersWorld world, AgentKnowledgeWritingProject project, string artifactId)
    {
        if (project.OrderInstructionId is null || !world.IsCurrentKnowledgeOrderProject(project)) return;
        var instruction = world.PendingInstructionFor(project.ActorId)!;
        world.ClearKnowledgeOrderBinding(project);
        if (instruction.Task!.Action == "copy_knowledge")
        {
            var current = world.Orders.Instructions[instruction.IdempotencyKey];
            world.Orders.RecordCreditKnowledgeOrderCompletion(instruction.IdempotencyKey, OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(current, current.Task!.UpdateDefinition(definition => definition switch
{
    CopyKnowledgeOrderDefinition task => OrderStore.PrepareExecuteKnowledgeOrderStepCopyKnowledgeOrderDefinition(task, null),
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
})));
        }
        world.CreditOrderEffect(instruction, Identity.Parse(AgentKnowledgeRules.WritingReceiptId(artifactId)), 1);
    }


    internal static void CancelKnowledgeWritingForOrder(IKnowledgeOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        if (instruction.Task is { } order && IsKnowledgeOrder(order.Action) && world.KnowledgeWritingFor(instruction.TargetAgentId) is { } project &&
            project.OrderInstructionId == instruction.InstructionId)
            world.CancelKnowledgeWriting(project, "owner_order_cancelled");
    }
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
    internal static AgentKnowledgeArtifact? KnowledgeCopySourceFor(IKnowledgeCopyOrdersWorld world, OwnerQueuedInstruction instruction) => world.HeldKnowledgeArtifacts(instruction.TargetAgentId).FirstOrDefault(source =>
        {
            var order = instruction.Task!;
            if (source.Kind != order.TargetKnowledgeKind || order.KnowledgeCopySourceArtifactId is { } bound && source.Id != bound) return false;
            var actor = instruction.TargetAgentId;
            var facts = world.FactsToWrite(actor, source.Kind, source);
            var recipes = world.RecipesToWrite(actor, source.Kind, source);
            return facts.Length + recipes.Length > 0 && world.HasCompleteCopyContents(actor, source, facts, recipes) && !world.AlreadyWrote(actor, source.Kind, facts, recipes);
        });

    internal static string KnowledgeCopyBlockedReason(IKnowledgeCopyOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        var source = world.HeldKnowledgeArtifacts(actor).FirstOrDefault(item => item.Kind == order.TargetKnowledgeKind &&
            (order.KnowledgeCopySourceArtifactId is null || order.KnowledgeCopySourceArtifactId == item.Id));
        if (source is null) return "Waiting to own and carry the requested source record, map or book.";
        var facts = world.FactsToWrite(actor, source.Kind, source);
        var recipes = world.RecipesToWrite(actor, source.Kind, source);
        if (!world.HasCompleteCopyContents(actor, source, facts, recipes)) return "Waiting to learn all sites and recipes in the held source before copying it.";
        if (world.KnowledgeCopySourceFor(instruction) is null)
            return "Everything in the held item has already been copied. Waiting for another item to copy.";
        return source.Kind == "book"
            ? "Waiting for permitted paper and cloth, a reachable supply and carrying space."
            : "Waiting for permitted paper, a reachable supply and carrying space.";
    }

}
