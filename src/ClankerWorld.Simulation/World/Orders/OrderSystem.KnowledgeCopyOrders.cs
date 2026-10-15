using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static AgentKnowledgeArtifact? KnowledgeCopySourceFor(IKnowledgeCopyOrdersWorld world, OwnerQueuedInstruction instruction) =>
        world.HeldKnowledgeArtifacts(instruction.TargetAgentId).FirstOrDefault(source =>
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
        if (KnowledgeCopySourceFor(world, instruction) is null)
            return "Everything in the held item has already been copied. Waiting for another item to copy.";
        return source.Kind == "book"
            ? "Waiting for permitted paper and cloth, a reachable supply and carrying space."
            : "Waiting for permitted paper, a reachable supply and carrying space.";
    }
}
