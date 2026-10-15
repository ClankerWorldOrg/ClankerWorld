using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

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
                (project.OrderInstructionId is null || IsCurrentKnowledgeOrderProject(world, project))
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
             project.OrderInstructionId is not null && !IsCurrentKnowledgeOrderProject(world, project)))
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
        if (KnowledgeOrderCandidateFor(world, instruction) is null)
        {
            world.SetOrderStatus(instruction, "blocked", KnowledgeOrderBlockedReason(world, instruction));
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
                world.Orders.RecordKnowledgeOrderStep(instruction.IdempotencyKey, OrderStore.PrepareTaskBinding(current, OrderStore.PrepareKnowledgeCopy(current.Task!, source.Id)));
                candidate = world.KnowledgeCopyPrefix + source.Id;
            }
            world.ApplyKnowledgeWritingCandidate(actor, person, candidate);
            if (world.KnowledgeWritingFor(actor) is null)
            {
                if (world.Agents[actor].Position == person.Position && world.Society.Inventory == inventory)
                    world.SetOrderStatus(instruction, "blocked", KnowledgeOrderBlockedReason(world, instruction));
                return;
            }
        }
        var project = world.KnowledgeWritingFor(actor)!;
        if (project.OrderInstructionId is null)
        {
            project = project with { OrderInstructionId = instruction.InstructionId };
            world.ReplaceKnowledgeWriting(project);
            var current = world.Orders.Instructions[instruction.IdempotencyKey];
            world.Orders.RecordKnowledgeOrderStep(instruction.IdempotencyKey, OrderStore.PrepareTaskBinding(current, OrderStore.PrepareKnowledgeWriting(current.Task!, project.Id, project.SourceArtifactId)));
        }
        world.ContinueKnowledgeWriting(project);
        var updated = world.Orders.Instructions[instruction.IdempotencyKey];
        if (world.KnowledgeWritingFor(actor) is null && world.IsActiveOrder(updated.Task!.Status) &&
            updated.Task.CompletedUnits == instruction.Task!.CompletedUnits)
            world.SetOrderStatus(updated, "blocked", KnowledgeOrderBlockedReason(world, updated));
    }

    internal static bool IsCurrentKnowledgeOrderProject(IKnowledgeOrdersWorld world, AgentKnowledgeWritingProject project) =>
        world.PendingInstructionFor(project.ActorId) is { } instruction && instruction.InstructionId == project.OrderInstructionId &&
        instruction.Task is { } order && world.IsKnowledgeOrder(order.Action) && order.TargetKnowledgeKind == project.Kind &&
        order.KnowledgeWritingProjectId == project.Id && world.MatchesKnowledgeSource(order, project.SourceArtifactId, bound: true);

    internal static void ClearKnowledgeOrderBinding(IKnowledgeOrdersWorld world, AgentKnowledgeWritingProject project)
    {
        var instruction = world.Orders.Instructions.Values.FirstOrDefault(item => item.InstructionId == project.OrderInstructionId);
        if (instruction?.Task is not { } order || !world.IsKnowledgeOrder(order.Action) || order.KnowledgeWritingProjectId != project.Id) return;
        world.Orders.RecordClearKnowledgeOrderBinding(instruction.IdempotencyKey, OrderStore.PrepareTaskBinding(instruction, OrderStore.PrepareKnowledgeProject(order, null)));
    }

    internal static void CreditKnowledgeOrderCompletion(IKnowledgeOrdersWorld world, AgentKnowledgeWritingProject project, string artifactId)
    {
        if (project.OrderInstructionId is null || !IsCurrentKnowledgeOrderProject(world, project)) return;
        var instruction = world.PendingInstructionFor(project.ActorId)!;
        ClearKnowledgeOrderBinding(world, project);
        if (instruction.Task!.Action == "copy_knowledge")
        {
            var current = world.Orders.Instructions[instruction.IdempotencyKey];
            world.Orders.RecordCreditKnowledgeOrderCompletion(instruction.IdempotencyKey, OrderStore.PrepareTaskBinding(current, OrderStore.PrepareKnowledgeCopy(current.Task!, null)));
        }
        world.CreditOrderEffect(instruction, AgentKnowledgeRules.WritingReceiptId(artifactId), 1);
    }

    internal static void CancelKnowledgeWritingForOrder(IKnowledgeOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        if (instruction.Task is { } order && world.IsKnowledgeOrder(order.Action) && world.KnowledgeWritingFor(instruction.TargetAgentId) is { } project &&
            project.OrderInstructionId == instruction.InstructionId)
            world.CancelKnowledgeWriting(project, "owner_order_cancelled");
    }
}
