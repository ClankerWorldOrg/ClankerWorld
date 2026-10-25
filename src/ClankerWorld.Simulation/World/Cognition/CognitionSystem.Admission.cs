using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal static void ApplyDecision(ICognitionAdmissionWorld world, SocietyCognitionDispatchResult decision)
    {
        if (decision.ActionPlan is { } plan &&
            (decision.PlanRefusal ?? world.CheckActionPlan(plan).Refusal) is { } refusal)
        {
            world.RefuseChoice(plan, refusal);
            return;
        }
        if (world.AttemptedActions is not null && decision.Admission.Intention is { } attempted)
            world.AttemptedActions[decision.AgentId] = attempted.CandidateId;
        if (world.WithheldActions?.Contains(decision.AgentId) == true) return;
        if (decision.Admission.Accepted && !decision.Admission.FellBack &&
            decision.Admission.Intention?.Provider is DecisionProviderKind.Jev or DecisionProviderKind.OpenAiDecisions &&
            decision.Admission.MemorySummary is { } summary)
        {
            world.ApplyMemorySummary(decision.AgentId, summary);
        }
        if (decision.Admission.Accepted && !decision.Admission.FellBack &&
            decision.Admission.Intention?.Provider is DecisionProviderKind.Jev or DecisionProviderKind.OpenAiDecisions &&
            decision.Admission.MemoryCompactionScores is { Count: > 0 } memoryScores)
        {
            world.ApplyMemoryCompaction(decision.AgentId, memoryScores);
        }

        if (!decision.Admission.Accepted || decision.Admission.Intention is null ||
            !world.Agents.TryGetValue(decision.AgentId, out var state))
        {
            world.AppendEvent(new CognitionRejectedEventPayload(decision.AgentId, decision.Admission.Outcome));
            return;
        }

        _ = world.ApplyObserverGuidanceResult(decision.AgentId, decision.Admission);
        if (world.ApplyMedicalConsentDecision(decision) || world.ApplyOrnamentDecision(decision) || world.ApplyAnimalConsentDecision(decision))
            return;
        var pendingInstruction = world.PendingInstructionFor(decision.AgentId);
        var candidateId = decision.Admission.Intention.CandidateId;
        if (world.PassengerBoat(decision.AgentId) is not null && candidateId is not ("consume_food" or "drink_milk" or "safe_idle"))
        {
            world.AppendEvent(new BoatActionBlockedEventPayload(decision.AgentId));
            return;
        }
        if (world.PassengerBoat(decision.AgentId) is not null)
        {
            world.ApplyCandidate(decision.AgentId, state, candidateId, reportIdle: true);
            return;
        }
        if (candidateId != "safe_idle") world.GuardianPlacementActions.RecordAction(decision.AgentId);
        if (decision.Admission.Intention.OperativeOrderInstructionId != pendingInstruction?.InstructionId)
        {
            world.AppendEvent(new InstructionOrderStaleDecisionEventPayload(decision.AgentId));
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
                world.SetOrderStatus(order, OrderStatus.Blocked, "The order will try again after a short wait.", waitForDecision: true);
                world.ApplyCandidate(decision.AgentId, state, candidateId, reportIdle: true);
            }
            else
            {
                var orderCandidate = world.OrderCandidateFor(order, state);
                var urgentCandidate = world.NeedsUrgentFood(state) || world.NeedsUrgentWarmth(state)
                    ? world.UrgentSurvivalCandidateFor(decision.AgentId, state, order)
                    : null;
                if (world.ShouldInterruptOrder(state, order, orderCandidate, urgentCandidate))
                {
                    world.SetOrderStatus(order, OrderStatus.Interrupted, "Food or warmth needs come first.");
                    if (decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel &&
                        world.IsMarketFoodCandidate(decision.AgentId, candidateId))
                        world.ApplyMarketCandidate(decision.AgentId, state, candidateId, decision.ActionPlan);
                    else if (decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel &&
                        decision.ActionPlan is ShopBuyActionPlan or ShopResponseActionPlan &&
                        world.IsShopFoodCandidate(decision.AgentId, candidateId, decision.ActionPlan as ShopBuyActionPlan))
                        world.ApplyShopCandidate(decision.AgentId, state, candidateId, decision.ActionPlan);
                    else if (urgentCandidate is not null)
                        world.ApplyCandidate(decision.AgentId, state, urgentCandidate.Id, reportIdle: true);
                }
                else if (orderCandidate is not null)
                {
                    world.ExecuteOrderStep(order, state, orderCandidate);
                }
                else
                {
                    world.SetOrderStatus(order, OrderStatus.Blocked, world.OrderBlockedReason(order, state));
                    world.ApplyCandidate(decision.AgentId, state, "safe_idle", reportIdle: true);
                }
            }
        }
        else
        {
            if (candidateId.StartsWith("civic|", StringComparison.Ordinal))
            {
                if (!decision.Admission.FellBack && decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    world.ApplyPlannedTownCivicCandidate(decision.AgentId, candidateId, decision.Admission.CivicProposal,
                        decision.Admission.CivicBallot, decision.Admission.CivicLandTiles, decision.Admission.CivicLandHearing, decision.Admission.CivicNonLandHearing, decision.ActionPlan);
            }
            else if (candidateId.StartsWith("nonviolent_remedy:", StringComparison.Ordinal) || candidateId.StartsWith("nonviolent_relay:", StringComparison.Ordinal))
            {
                if (!decision.Admission.FellBack && decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    foreach (var town in world.Towns.ToArray())
                        if (world.ApplyNonLandRemedyAction(town, decision.AgentId, candidateId, decision.ActionPlan)) break;
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


    internal static List<string> ApplyObserverGuidanceResult(ICognitionAdmissionWorld world,
        string agentId,
        CognitionAdmissionResult admission)
    {
        if (!admission.Accepted || admission.FellBack ||
            admission.Intention?.Provider != DecisionProviderKind.LargeLanguageModel ||
            admission.ObserverGuidance is not { } result ||
            result.WorldId != world.Society.WorldId ||
            result.AgentId != agentId ||
            result.RunEpoch != admission.Intention.RunEpoch ||
            result.DecisionGeneration != admission.Intention.DecisionGeneration ||
            result.ObservationDigest != admission.Intention.ObservationDigest ||
            !world.Agents.ContainsKey(agentId))
            return [];

        var replies = result.Replies.ToDictionary(reply => reply.InstructionId, StringComparer.Ordinal);
        var completedSuggestions = new List<string>();
        foreach (var message in result.Messages)
        {
            var instruction = world.Orders.Instructions.Values.SingleOrDefault(item =>
                item.InstructionId == message.InstructionId);
            if (instruction is null || instruction.IssuerId != message.IssuerId ||
                instruction.TargetAgentId != agentId ||
                instruction.TargetAgentId != message.TargetAgentId ||
                world.ToWireValue(instruction.Kind) != message.Kind || instruction.Text != message.Text ||
                instruction.SubmittedTick != message.SubmittedTick || instruction.RunEpoch != message.RunEpoch ||
                instruction.SubmissionSequence != message.SubmissionSequence ||
                (instruction.Kind == OwnerInstructionKind.Order &&
                 world.UnderstoodTaskFor(instruction.Task?.Action) != message.UnderstoodTask) ||
                (instruction.Kind == OwnerInstructionKind.Suggestion && message.UnderstoodTask is not null) ||
                (instruction.ObserverReply is null) != message.ReplyAllowed ||
                world.IsInstructionComplete(instruction) &&
                    !(instruction.Kind == OwnerInstructionKind.Order && instruction.Task?.Status == "finished"))
                continue;

            var observerReply = replies.TryGetValue(instruction.InstructionId, out var reply)
                ? reply.Text
                : instruction.ObserverReply;
            world.Orders.RecordObserverReply(instruction.IdempotencyKey, OrderStore.PrepareApplyObserverGuidanceResultOwnerQueuedInstruction(instruction, instruction.ObservedTick ?? admission.Intention.WorldTick, observerReply));
            world.MarkCheckpointCurrent();
            // A reply for an order that has since finished is set aside as stale,
            // so a suggestion it carried stays open for the next fresh request.
            if (instruction.Kind == OwnerInstructionKind.Suggestion &&
                admission.Intention.OperativeOrderInstructionId == world.PendingInstructionFor(agentId)?.InstructionId &&
world.Orders.CompleteInstruction(instruction.InstructionId))
                completedSuggestions.Add(instruction.InstructionId);
        }

        return completedSuggestions;
    }


    internal static void ApplyCandidate(ICognitionAdmissionWorld world, string agentId, AgentState state, string candidateId, bool reportIdle) => world.ApplyRegisteredCandidate(agentId, state, candidateId, reportIdle);
    internal static HashSet<string> ApplyContinuingIntentions(ICognitionContinuationWorld world,
        IEnumerable<string> dispatchedAgentIds,
        IEnumerable<string> pendingModelAgentIds)
    {
        var dispatched = dispatchedAgentIds.ToHashSet(StringComparer.Ordinal);
        var pendingModelCalls = pendingModelAgentIds.ToHashSet(StringComparer.Ordinal);
        var orderActorsHandledThisTick = new HashSet<string>(StringComparer.Ordinal);
        var runtimes = world.Capture().Cognition.Runtimes
            .ToDictionary(item => item.AgentId, StringComparer.Ordinal);
        foreach (var agent in world.Society.Agents
                     .Where(item => item.Status == SocietyAgentStatus.Active)
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (world.WithheldActions?.Contains(agent.Id) == true) continue;
            if (dispatched.Contains(agent.Id) || !world.Agents.TryGetValue(agent.Id, out var state))
            {
                continue;
            }
            if (world.AttemptedActions is not null && runtimes[agent.Id].CurrentIntention is { } attempted)
                world.AttemptedActions[agent.Id] = attempted.CandidateId;
            if (world.PassengerBoat(agent.Id) is not null)
            {
                if ((!pendingModelCalls.Contains(agent.Id) || world.PendingInstructionFor(agent.Id) is not null) && world.NeedsUrgentFood(state) &&
                    world.CreateCandidates(agent.Id, state).Any(candidate => candidate.Id == "consume_food"))
                    world.ApplyCandidate(agent.Id, state, "consume_food", reportIdle: false);
                continue;
            }
            var order = world.PendingInstructionFor(agent.Id);
            var waitingForModel = pendingModelCalls.Contains(agent.Id);
            if (waitingForModel && (order is null || !world.NeedsUrgentFood(state) && !world.NeedsUrgentWarmth(state)))
            {
                // A delayed reply supplies no new choice. Urgent survival and nearby
                // practical care may interrupt the task the agent already chose.
                if (order is null) orderActorsHandledThisTick.Add(agent.Id);
                if (world.TryApplyWaitingRoutine(agent.Id, state))
                {
                    orderActorsHandledThisTick.Add(agent.Id);
                    if (order is not null) world.SetOrderStatus(order, OrderStatus.Interrupted, "Caring for a nearby dependent.");
                    continue;
                }
            }
            if (order is null && world.ContinueGuardianPlacement(agent.Id)) continue;
            if (world.IsConversationBusy(agent.Id))
            {
                continue;
            }
            if (LifeStageRules.IsInfant(agent.AgeBand))
            {
                continue;
            }
            if (order is not null)
            {
                orderActorsHandledThisTick.Add(agent.Id);
                var orderCandidate = world.OrderCandidateFor(order, state);
                var urgentCandidate = world.NeedsUrgentFood(state) || world.NeedsUrgentWarmth(state)
                    ? world.UrgentSurvivalCandidateFor(agent.Id, state, order)
                    : null;
                if (world.ShouldInterruptOrder(state, order, orderCandidate, urgentCandidate))
                {
                    world.SetOrderStatus(order, OrderStatus.Interrupted, "Food or warmth needs come first.");
                    if (urgentCandidate is not null && world.IsMarketFoodCandidate(agent.Id, urgentCandidate.Id) &&
                        runtimes[agent.Id].CurrentIntention is { Provider: DecisionProviderKind.LargeLanguageModel } orderMarketIntention &&
                        orderMarketIntention.OperativeOrderInstructionId == order.InstructionId &&
                        world.IsMarketFoodCandidate(agent.Id, orderMarketIntention.CandidateId))
                        world.ContinueMarketWalk(agent.Id, orderMarketIntention.CandidateId);
                    else if (urgentCandidate is not null)
                        world.ApplyCandidate(agent.Id, state, urgentCandidate.Id, reportIdle: false);
                    continue;
                }
                if (order.Task!.WaitForDecisionAfterFailure)
                {
                    if (urgentCandidate is not null)
                        world.ApplyCandidate(agent.Id, state, urgentCandidate.Id, reportIdle: false);
                    continue;
                }
                if (orderCandidate is not null)
                {
                    world.ExecuteOrderStep(order, state, orderCandidate);
                    continue;
                }
                world.SetOrderStatus(order, OrderStatus.Blocked, world.OrderBlockedReason(order, state));
                continue;
            }
            if (world.CanContinueEquipmentRepair(agent.Id))
            {
                world.ContinueEquipmentRepair(agent.Id);
                continue;
            }
            if (world.CanContinueLesson(agent.Id, waitingForModel))
            {
                world.ContinueLesson(agent.Id, waitingForModel);
                continue;
            }
            if (world.ContinueFarmWork(agent.Id)) continue;
            if (world.CanContinueProject(state, waitingForModel))
            {
                world.ContinueProject(agent.Id, state);
                continue;
            }
            if (runtimes[agent.Id].CurrentIntention is not { } intention)
            {
                continue;
            }

            if (intention.OperativeOrderInstructionId is not null &&
                intention.OperativeOrderInstructionId != order?.InstructionId)
                continue;

            if (intention.CandidateId.StartsWith(world.NonLandRemedyPrefix, StringComparison.Ordinal))
            {
                // Continue only the physical contribution the agent already chose. Every step
                // checks the live agreement, remaining goods, access and care needs again.
                if (intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    foreach (var town in world.Towns.ToArray())
                        if (world.ApplyNonLandRemedyAction(town, agent.Id, intention.CandidateId)) break;
                continue;
            }
            if (intention.CandidateId.StartsWith(world.NonLandRelayPrefix, StringComparison.Ordinal)) continue;
            if (intention.CandidateId.StartsWith("civic|", StringComparison.Ordinal))
            {
                // Formal civic acts require a fresh admitted personal choice. Only the physical trip
                // toward the notice place continues locally between ordinary model turns.
                var civic = intention.CandidateId.Split('|');
                if (civic.Length == 5 && civic[2] == "visit" && intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    world.ContinueTownCivicVisit(agent.Id, intention.CandidateId);
                else if (civic.Length == 5 && civic[2] is "land_transfer_collect_payment" or "land_transfer_pay" or "land_transfer_meet" &&
                    intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    world.ContinueTownLandSaleWalk(agent.Id, intention.CandidateId);
                continue;
            }
            if (world.IsTownProjectDonationCandidate(intention.CandidateId))
            {
                if (intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    world.ContinueTownProjectDonationWalk(agent.Id, intention.CandidateId);
                continue;
            }
            if (world.IsMarketCandidate(intention.CandidateId))
            {
                if (intention.Provider == DecisionProviderKind.LargeLanguageModel && intention.OperativeOrderInstructionId is null)
                    world.ContinueMarketWalk(agent.Id, intention.CandidateId);
                continue;
            }
            if (world.IsOrnamentCandidate(intention.CandidateId))
            {
                if (intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    world.ContinueOrnamentWalk(agent.Id, intention.CandidateId);
                continue;
            }
            if (waitingForModel && !world.CanContinuePendingTask(state, intention.CandidateId)) continue;
            // Safe idle is always offered outside the conversation/order branches
            // above. EnqueueDueCognition still rebuilds choices to reconsider work.
            if (intention.CandidateId != "safe_idle" &&
                !world.CreateCandidates(agent.Id, state).Any(candidate => candidate.Id == intention.CandidateId)) continue;
            world.ApplyCandidate(agent.Id, state, intention.CandidateId, reportIdle: false);
        }

        return orderActorsHandledThisTick;
    }
    internal sealed record ModelDecisionOutcome(CognitionDecisionResponse? Response, string? Failure);
    internal sealed record PendingModelDecision(
        CognitionDecisionRequest Request,
        Task<ModelDecisionOutcome> Task,
        CancellationTokenSource Cancellation,
        string? DecisionContext);
    internal sealed record ConversationTurnOutcome(AgentConversationTurnResponse? Response, Exception? Failure);
    internal sealed record PendingConversationTurn(
        AgentConversationTurnRequest Request,
        Task<ConversationTurnOutcome> Task,
        CancellationTokenSource Cancellation,
        long ProviderEpoch);

    internal sealed record IdentityMomentOutcome(CognitionDecisionResponse? Response);
    internal sealed record PendingIdentityMoment(AgentIdentityMomentKind Kind,
        CognitionDecisionRequest Request, Task<IdentityMomentOutcome> Task, CancellationTokenSource Cancellation);

    internal sealed record PreparedIdentityMoment(AgentIdentityMomentKind Kind,
        CognitionDecisionRequest Request, IDecisionProvider Provider);
    internal static void RecordModelAttempt(ICognitionModelOutcomesWorld world, string id, string status, string? acceptedCandidateId = null,
        string? setupBlocker = null)
    {
        if (!world.Agents.TryGetValue(id, out var physical)) return;
        var previous = physical.LastModelAttempt;
        var next = new ModelAttempt(status, world.WorldTick,
            acceptedCandidateId ?? previous?.LastAcceptedCandidateId,
            acceptedCandidateId is not null ? world.WorldTick : previous?.LastAcceptedTick, setupBlocker);
        if (next == previous) return;
        world.PhysicalAgents.RecordModelAttempt(id, physical, next);
        world.MarkCheckpointCurrent();
        world.AppendEvent(new ModelAttemptStatusEventPayload(id, status));
    }


    internal static void RecordModelCompletion(ICognitionModelOutcomesWorld world, string id, CognitionAdmissionResult admission, string? failure)
    {
        if (admission.Accepted && !admission.FellBack)
            world.RecordModelAttempt(id, "ready", admission.Intention?.CandidateId);
        else
        {
            var outcome = failure ?? admission.Outcome;
            world.RecordModelAttempt(id, CognitionProviderFailures.Status(outcome),
                setupBlocker: outcome == "unsupported_request" ? "unsupported_request" : null);
        }
    }
    internal static bool IsMatchingIdentityMomentResponse(CognitionDecisionRequest request, CognitionDecisionResponse? response)
    {
        try
        {
            response?.Validate();
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        return response is not null && response.RequestId == request.RequestId &&
            response.AgentId == request.Observation.AgentId && response.Provider == DecisionProviderKind.LargeLanguageModel &&
            response.ProviderEpoch == request.ProviderEpoch && response.RunEpoch == request.Observation.RunEpoch &&
            response.DecisionGeneration == request.Observation.DecisionGeneration &&
            response.ObservationDigest == request.Observation.ObservationDigest && response.SelectedCandidateId == "identity_optional";
    }
}
