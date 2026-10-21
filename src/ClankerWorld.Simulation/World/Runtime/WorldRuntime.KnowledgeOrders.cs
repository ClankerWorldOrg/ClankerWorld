using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static bool IsKnowledgeOrder(string action) => OrderSystem.IsKnowledgeOrder(action);

    private static bool MatchesKnowledgeSource(OrderTask order, string? source, bool bound) =>
        order.Action == "write_knowledge" ? source is null && order.KnowledgeCopySourceArtifactId is null :
        order.Action == "copy_knowledge" && source is not null &&
            (order.KnowledgeCopySourceArtifactId == source || !bound && order.KnowledgeCopySourceArtifactId is null);

    private CognitionCandidate? KnowledgeOrderCandidateFor(OwnerQueuedInstruction instruction) =>
        OrderSystem.KnowledgeOrderCandidateFor(KnowledgeOrdersWorld, instruction);

    private string KnowledgeOrderBlockedReason(OwnerQueuedInstruction instruction) =>
        OrderSystem.KnowledgeOrderBlockedReason(KnowledgeOrdersWorld, instruction);

    private void ExecuteKnowledgeOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteKnowledgeOrderStep(KnowledgeOrdersWorld, instruction, person);

    private bool IsCurrentKnowledgeOrderProject(AgentKnowledgeWritingProject project) =>
        OrderSystem.IsCurrentKnowledgeOrderProject(KnowledgeOrdersWorld, project);

    private void ClearKnowledgeOrderBinding(AgentKnowledgeWritingProject project) =>
        OrderSystem.ClearKnowledgeOrderBinding(KnowledgeOrdersWorld, project);

    private void CreditKnowledgeOrderCompletion(AgentKnowledgeWritingProject project, string artifactId) =>
        OrderSystem.CreditKnowledgeOrderCompletion(KnowledgeOrdersWorld, project, artifactId);

    private void CancelKnowledgeWritingForOrder(OwnerQueuedInstruction instruction) =>
        OrderSystem.CancelKnowledgeWritingForOrder(KnowledgeOrdersWorld, instruction);

    private static void ValidateKnowledgeOrderBindings(WorldKnowledgeState knowledge,
        IEnumerable<OwnerQueuedInstruction> instructions)
    {
        var saved = instructions.ToArray();
        var orders = saved.Where(item => item.Task is { } order && IsKnowledgeOrder(order.Action))
            .ToDictionary(item => item.InstructionId, StringComparer.Ordinal);
        var projects = knowledge.WritingProjects.ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var project in knowledge.WritingProjects.Where(item => item.OrderInstructionId is not null))
        {
            if (!orders.TryGetValue(project.OrderInstructionId!, out var instruction) ||
                instruction.TargetAgentId != project.ActorId || instruction.Task!.TargetKnowledgeKind != project.Kind ||
                instruction.Task.KnowledgeWritingProjectId != project.Id || !MatchesKnowledgeSource(instruction.Task, project.SourceArtifactId, bound: true) ||
                !IsActiveOrder(instruction.Task.Status) || instruction.Task.Status == "queued" ||
                FirstActiveOrderFor(saved, project.ActorId)?.InstructionId != instruction.InstructionId)
                throw new InvalidDataException("Ordered writing has no matching current owner task.");
        }
        foreach (var artifact in knowledge.Artifacts.Where(item => item.OrderInstructionId is not null))
        {
            if (!orders.TryGetValue(artifact.OrderInstructionId!, out var instruction) ||
                instruction.TargetAgentId != artifact.CreatorId || instruction.Task!.TargetKnowledgeKind != artifact.Kind ||
                (instruction.Task.Action == "copy_knowledge") != (artifact.SourceArtifactId is not null) ||
                artifact.CreatedTick < instruction.SubmittedTick)
                throw new InvalidDataException("A written artifact does not belong to its recorded owner task.");
        }
        foreach (var instruction in orders.Values)
        {
            var order = instruction.Task!;
            if (order.KnowledgeCopySourceArtifactId is { } sourceId &&
                !knowledge.Artifacts.Any(item => item.Id == sourceId && item.Kind == order.TargetKnowledgeKind))
                throw new InvalidDataException("A copying order has no matching source artifact.");
            if (order.KnowledgeWritingProjectId is { } projectId &&
                (!projects.TryGetValue(projectId, out var project) || project.OrderInstructionId != instruction.InstructionId))
                throw new InvalidDataException("A writing order is bound to an unrelated job.");
            var completed = knowledge.Artifacts.Where(item => item.OrderInstructionId == instruction.InstructionId)
                .OrderBy(item => item.CreatedTick).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
            if (order.CompletedUnits != completed.Length || completed.Length > 0 &&
                order.LastEffectId != AgentKnowledgeRules.WritingReceiptId(completed[^1].Id))
                throw new InvalidDataException("Writing order progress has no matching completed physical artifacts.");
        }
    }
}
