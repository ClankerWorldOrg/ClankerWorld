using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
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

}
