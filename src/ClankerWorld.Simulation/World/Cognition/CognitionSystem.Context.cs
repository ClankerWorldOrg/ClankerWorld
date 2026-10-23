using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal static bool IsOrderDecisionObservationCurrent(ICognitionContextWorld world, AgentObservation observation) => observation.WorldId == world.Society.WorldId &&
        observation.RunEpoch == world.Society.RunEpoch &&
        (observation.OperativeOrderInstructionId == world.PendingInstructionFor(observation.AgentId)?.InstructionId ||
            world.IsFinishedOrderDecisionAwaitingReply(observation));

    internal static bool IsUndeliveredFinishedOrderDecision(ICognitionContextWorld world, AgentObservation observation) => world.IsFinishedOrderDecisionAwaitingReply(observation) && world.Orders.Instructions.Values.Any(item =>
            item.InstructionId == observation.OperativeOrderInstructionId && item.ObservedTick is null);

    internal static bool IsFinishedOrderDecisionAwaitingReply(ICognitionContextWorld world, AgentObservation observation)
    {
        if (observation.OperativeOrderInstructionId is not { } orderId ||
            observation.WorldId != world.Society.WorldId ||
            observation.RunEpoch != world.Society.RunEpoch)
            return false;
        var instruction = world.Orders.Instructions.Values.SingleOrDefault(item =>
            item.InstructionId == orderId && item.TargetAgentId == observation.AgentId &&
            item.Kind == OwnerInstructionKind.Order && item.Task?.Status == "finished" &&
            item.ObserverReply is null && world.IsInstructionComplete(item));
        // Local work can finish while its original personal reply is held.
        // Keep that exact message available for acknowledgement, never execution.
        return instruction is not null && observation.ObserverGuidance?.Any(message =>
            message.InstructionId == orderId && message.IssuerId == instruction.IssuerId &&
            message.TargetAgentId == instruction.TargetAgentId && message.Kind == "order" &&
            message.Text == instruction.Text && message.SubmittedTick == instruction.SubmittedTick &&
            message.RunEpoch == instruction.RunEpoch && message.SubmissionSequence == instruction.SubmissionSequence &&
            message.UnderstoodTask == world.UnderstoodTaskFor(instruction.Task?.Action) && message.ReplyAllowed) == true;
    }


    internal static bool NeedsCognition(ICognitionContextWorld world,
        string agentId,
        CognitionIntention? current,
        List<CognitionCandidate> candidates,
        string? conversationChoiceContext)
    {
        if (conversationChoiceContext is not null &&
            !HasPromptedConversationChoice(world.Agents[agentId].LastDecisionContext, conversationChoiceContext))
        {
            return true;
        }

        // A new instruction prompts one fresh decision. If it cannot progress
        // yet, it waits for the agent's usual decisions instead of requesting
        // another (possibly paid) decision on every tick.
        if (world.HasNewObserverGuidanceFor(agentId))
        {
            return true;
        }

        if (HasNewGuardianCandidate(world.Agents[agentId].LastDecisionContext, candidates))
        {
            return true;
        }

        // Cancellation or replacement ends an order-bound intention even when
        // its generic food candidate remains legal for ordinary personal plans.
        if (current?.OperativeOrderInstructionId is { } orderId &&
            orderId != world.PendingInstructionFor(agentId)?.InstructionId)
            return true;

        if (world.PendingInstructionFor(agentId) is { Task: { } order })
        {
            return current is null || checked(world.WorldTick - current.WorldTick) >= world.CognitionReevaluationIntervalTicks;
        }

        if (world.CanContinueEquipmentRepair(agentId) || world.CanContinueLesson(agentId) || world.CanContinueProject(world.Agents[agentId]))
        {
            return false;
        }

        if (candidates.Count == 1 && candidates[0].Id == "safe_idle")
        {
            return false;
        }

        if (current is null)
        {
            return true;
        }

        // Finishing a project frees its inputs and work site for the next
        // craft. Reconsider once instead of repeating the old recipe before
        // the usual reevaluation interval has elapsed.
        if (world.Agents[agentId].Project is { Stage: "completed" } completed &&
            current.CandidateId == completed.CandidateId && current.WorldTick <= completed.LastTransitionTick)
        {
            return true;
        }

        if (!candidates.Any(candidate => candidate.Id == current.CandidateId))
        {
            return true;
        }

        if (current.CandidateId is "harvest_food" or "consume_food")
        {
            return true;
        }

        if (current.CandidateId == "safe_idle")
        {
            var physical = world.Agents[agentId];
            return world.DecisionContext(physical, candidates, conversationChoiceContext) != physical.LastDecisionContext ||
                checked(world.WorldTick - current.WorldTick) >= 300;
        }

        return checked(world.WorldTick - current.WorldTick) >= world.CognitionReevaluationIntervalTicks;
    }


    internal static string DecisionContext(ICognitionContextWorld world,
        AgentState state,
        List<CognitionCandidate> candidates,
        string? conversationChoiceContext = null)
    {
        var context = $"{world.NeedsUrgentFood(state)}:{world.NeedsUrgentWarmth(state)}:" +
            // Optional advance permission remains available on ordinary decisions;
            // a healthy idle agent need not wake merely because someone walks past.
            string.Join('|', candidates.Where(candidate =>
                    !candidate.Id.StartsWith(world.MedicalAllowPrefix, StringComparison.Ordinal) ||
                    state.Survival is { IllnessBasisPoints: >= 2_500 })
                .Select(candidate => candidate.Id).Order(StringComparer.Ordinal));
        context += "|guardian_candidates=" + GuardianCandidateContext(candidates);
        return conversationChoiceContext is null
            ? context
            : $"{context}|conversation_choice={ConversationChoiceContextDigest(conversationChoiceContext)}";
    }

}
