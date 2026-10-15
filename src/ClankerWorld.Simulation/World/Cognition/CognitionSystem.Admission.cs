using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal static void ApplyDecision(ICognitionAdmissionWorld world, SocietyCognitionDispatchResult decision)
    {
        if (decision.ActionPlan is { } plan && (decision.PlanRefusal ?? world.CheckActionPlan(plan).Refusal) is { } refusal)
        {
            world.RefuseChoice(plan, refusal);
            return;
        }

        if (world.AttemptedActions is not null && decision.Admission.Intention is { } attempted)
            world.RecordAttemptedAction(decision.AgentId, attempted.CandidateId);
        if (world.WithheldActions?.Contains(decision.AgentId) == true)
            return;
        if (decision.Admission.Accepted && !decision.Admission.FellBack && decision.Admission.Intention?.Provider is DecisionProviderKind.Jev or DecisionProviderKind.OpenAiDecisions && decision.Admission.MemorySummary is { } summary)
        {
            world.ApplyMemorySummary(decision.AgentId, summary);
        }

        if (decision.Admission.Accepted && !decision.Admission.FellBack && decision.Admission.Intention?.Provider is DecisionProviderKind.Jev or DecisionProviderKind.OpenAiDecisions && decision.Admission.MemoryCompactionScores is { Count: > 0 } memoryScores)
        {
            world.ApplyMemoryCompaction(decision.AgentId, memoryScores);
        }

        if (!decision.Admission.Accepted || decision.Admission.Intention is null || !world.Agents.TryGetValue(decision.AgentId, out var state))
        {
            world.AppendEvent("cognition_rejected", $"{decision.AgentId}:{decision.Admission.Outcome}");
            return;
        }

        _ = world.ApplyObserverGuidanceResult(decision.AgentId, decision.Admission);
        if (world.ApplyMedicalConsentDecision(decision) || world.ApplyOrnamentDecision(decision) || world.ApplyAnimalConsentDecision(decision))
            return;
        var pendingInstruction = world.PendingInstructionFor(decision.AgentId);
        var candidateId = decision.Admission.Intention.CandidateId;
        if (world.PassengerBoat(decision.AgentId) is not null && candidateId is not ("consume_food" or "drink_milk" or "safe_idle"))
        {
            world.AppendEvent("boat_action_blocked", $"{decision.AgentId}:stay_aboard");
            return;
        }

        if (world.PassengerBoat(decision.AgentId) is not null)
        {
            world.ApplyCandidate(decision.AgentId, state, candidateId, reportIdle: true);
            return;
        }

        if (candidateId != "safe_idle")
            world.FamilyTurn.RecordAction(decision.AgentId);
        if (decision.Admission.Intention.OperativeOrderInstructionId != pendingInstruction?.InstructionId)
        {
            world.AppendEvent("instruction_order_stale_decision", decision.AgentId);
            return;
        }

        if (pendingInstruction is { } order)
        {
            if (world.TalkOrderHasConversation(order))
            {
                world.ApplyTalkOrderConversationDecision(order, decision);
                return;
            }

            if (decision.Admission.FellBack)
            {
                world.SetOrderStatus(order, "blocked", "The order will try again after a short wait.", waitForDecision: true);
                world.ApplyCandidate(decision.AgentId, state, candidateId, reportIdle: true);
            }
            else
            {
                var orderCandidate = world.OrderCandidateFor(order, state);
                var urgentCandidate = world.NeedsUrgentFood(state) || world.NeedsUrgentWarmth(state) ? world.UrgentSurvivalCandidateFor(decision.AgentId, state, order) : null;
                if (world.ShouldInterruptOrder(state, order, orderCandidate, urgentCandidate))
                {
                    world.SetOrderStatus(order, "interrupted", "Food or warmth needs come first.");
                    if (decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel && world.IsMarketFoodCandidate(decision.AgentId, candidateId))
                        world.ApplyMarketCandidate(decision.AgentId, state, candidateId, decision.ActionPlan);
                    else if (urgentCandidate is not null)
                        world.ApplyCandidate(decision.AgentId, state, urgentCandidate.Id, reportIdle: true);
                }
                else if (orderCandidate is not null)
                {
                    world.ExecuteOrderStep(order, state, orderCandidate);
                }
                else
                {
                    world.SetOrderStatus(order, "blocked", world.OrderBlockedReason(order, state));
                    world.ApplyCandidate(decision.AgentId, state, "safe_idle", reportIdle: true);
                }
            }
        }
        else
        {
            if (candidateId.StartsWith("civic|", StringComparison.Ordinal))
            {
                if (!decision.Admission.FellBack && decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    world.ApplyPlannedTownCivicCandidate(decision.AgentId, candidateId, decision.Admission.CivicProposal, decision.Admission.CivicBallot, decision.Admission.CivicLandTiles, decision.Admission.CivicLandHearing, decision.Admission.CivicNonLandHearing, decision.ActionPlan);
            }
            else if (candidateId.StartsWith("nonviolent_remedy:", StringComparison.Ordinal) || candidateId.StartsWith("nonviolent_relay:", StringComparison.Ordinal))
            {
                if (!decision.Admission.FellBack && decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    foreach (var town in world.Towns.ToArray())
                        if (world.ApplyNonLandRemedyAction(town, decision.AgentId, candidateId, decision.ActionPlan))
                            break;
            }
            else if (world.IsTownProjectDonationCandidate(candidateId))
            {
                if (!decision.Admission.FellBack && decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    world.ApplyTownProjectDonation(decision.AgentId, candidateId, decision.ActionPlan as TownProjectDonationActionPlan);
            }
            else if (world.IsMarketCandidate(candidateId))
            {
                if (!decision.Admission.FellBack && decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    world.ApplyMarketCandidate(decision.AgentId, state, candidateId, decision.ActionPlan);
            }
            else if (!world.ApplyToolMakingRequestDecision(decision))
                world.ApplyRegisteredCandidate(decision.AgentId, state, candidateId, reportIdle: true, decision.ActionPlan);
        }
    }

    internal static List<string> ApplyObserverGuidanceResult(ICognitionAdmissionWorld world, string agentId, CognitionAdmissionResult admission)
    {
        if (!admission.Accepted || admission.FellBack || admission.Intention?.Provider != DecisionProviderKind.LargeLanguageModel || admission.ObserverGuidance is not { } result || result.WorldId != world.Society.WorldId || result.AgentId != agentId || result.RunEpoch != admission.Intention.RunEpoch || result.DecisionGeneration != admission.Intention.DecisionGeneration || result.ObservationDigest != admission.Intention.ObservationDigest || !world.Agents.ContainsKey(agentId))
            return [];
        var replies = result.Replies.ToDictionary(reply => reply.InstructionId, StringComparer.Ordinal);
        var completedSuggestions = new List<string>();
        foreach (var message in result.Messages)
        {
            var instruction = world.Orders.Instructions.Values.SingleOrDefault(item => item.InstructionId == message.InstructionId);
            if (instruction is null || instruction.IssuerId != message.IssuerId || instruction.TargetAgentId != agentId || instruction.TargetAgentId != message.TargetAgentId || world.ToWireValue(instruction.Kind) != message.Kind || instruction.Text != message.Text || instruction.SubmittedTick != message.SubmittedTick || instruction.RunEpoch != message.RunEpoch || instruction.SubmissionSequence != message.SubmissionSequence || (instruction.Kind == OwnerInstructionKind.Order && world.UnderstoodTaskFor(instruction.Task?.Action) != message.UnderstoodTask) || (instruction.Kind == OwnerInstructionKind.Suggestion && message.UnderstoodTask is not null) || (instruction.ObserverReply is null) != message.ReplyAllowed || world.Orders.Completed.Contains(instruction.InstructionId) && !(instruction.Kind == OwnerInstructionKind.Order && instruction.Task?.Status == "finished"))
                continue;
            var observerReply = replies.TryGetValue(instruction.InstructionId, out var reply) ? reply.Text : instruction.ObserverReply;
            world.Orders.RecordObserverReply(instruction.IdempotencyKey, OrderStore.PrepareObservedReply(instruction, instruction.ObservedTick ?? admission.Intention.WorldTick, observerReply));
            world.MarkCheckpointCurrent();
            // A reply for an order that has since finished is set aside as stale,
            // so a suggestion it carried stays open for the next fresh request.
            if (instruction.Kind == OwnerInstructionKind.Suggestion && admission.Intention.OperativeOrderInstructionId == world.PendingInstructionFor(agentId)?.InstructionId && world.Orders.CompleteInstruction(instruction.InstructionId))
                completedSuggestions.Add(instruction.InstructionId);
        }

        return completedSuggestions;
    }

    internal static void ApplyCandidate(ICognitionAdmissionWorld world, string agentId, AgentState state, string candidateId, bool reportIdle) => world.ApplyRegisteredCandidate(agentId, state, candidateId, reportIdle);
}
