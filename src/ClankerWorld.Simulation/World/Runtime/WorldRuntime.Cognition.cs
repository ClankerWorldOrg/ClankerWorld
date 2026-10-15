using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    // Keep authoritative membership keys in saves; only model context needs a bounded alias.
    private static string? CognitionHouseholdId(string? id) => id is null ? null : DerivedId.Create(id, "household-sha256:");

    private void EnqueueDueCognition(IReadOnlySet<string> activeModelCallIds)
    {
        var cognitionState = society.Capture().Cognition;
        var runtimes = cognitionState.Runtimes
            .ToDictionary(item => item.AgentId, StringComparer.Ordinal);
        var namingRetries = cognitionState.Queue
            .Where(entry => entry.TriggerIds.Contains(
                SocietyCognitionScheduler.NameRetryTriggerId, StringComparer.Ordinal))
            .Select(entry => entry.AgentId)
            .ToHashSet(StringComparer.Ordinal);
        var staleRunDecisions = cognitionState.Queue
            .Where(entry => entry.Observation.RunEpoch != society.Checkpoint.RunEpoch)
            .Select(entry => entry.AgentId)
            .ToHashSet(StringComparer.Ordinal);
        // Retained work has already advanced LastDecisionContext. Once its
        // earlier call ends, rebuild it even if the ordinary cadence is quiet.
        var awaitingDispatch = cognitionState.Queue
            .Where(entry => !activeModelCallIds.Contains(entry.AgentId))
            .Select(entry => entry.AgentId)
            .ToHashSet(StringComparer.Ordinal);
        var waitingForModelIds = society.PendingModelAgentIds();
        var waitingOrderDecisions = cognitionState.Queue
            .Where(entry => waitingForModelIds.Contains(entry.AgentId) &&
                entry.Observation.OperativeOrderInstructionId is not null &&
                IsOrderDecisionObservationCurrent(entry.Observation) &&
                (!awaitingDispatch.Contains(entry.AgentId) ||
                 IsUndeliveredFinishedOrderDecision(entry.Observation)))
            .Select(entry => entry.AgentId)
            .ToHashSet(StringComparer.Ordinal);
        var staleOrderDecisions = cognitionState.Queue
            .Where(entry => waitingForModelIds.Contains(entry.AgentId) &&
                entry.Observation.OperativeOrderInstructionId is not null &&
                !IsOrderDecisionObservationCurrent(entry.Observation))
            .Select(entry => entry.AgentId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var agent in society.Checkpoint.Agents
                     .Where(item => item.Status == SocietyAgentStatus.Active)
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (agent.AgeBand == SocietyAgeBand.Infant)
            {
                continue;
            }
            // Submission already closes these. Recognition can change between
            // versions, so orders that are already waiting follow the same rule.
            CloseOrdersNotUnderstood(agent.Id);
            var physical = agents[agent.Id];
            var operativeOrder = PendingInstructionFor(agent.Id);
            if (waitingOrderDecisions.Contains(agent.Id) && !HasNewObserverGuidanceFor(agent.Id))
                continue;
            if (HasWaitingIdentityMoment(physical) && !NeedsUrgentFood(physical) && !NeedsUrgentWarmth(physical) &&
                !IsConversationBusy(agent.Id))
                continue;
            if (FarmWorkFor(agent.Id) is not null && operativeOrder is null &&
                !NeedsUrgentFood(physical) && !NeedsUrgentWarmth(physical) &&
                !staleRunDecisions.Contains(agent.Id) &&
                !awaitingDispatch.Contains(agent.Id) &&
                !ShouldDispatchConversationChoice(agent.Id) && GuardianPlacementCandidate(agent.Id) is null)
                continue;
            if (operativeOrder is not null && physical.Project is { Stage: not ("completed" or "cancelled") } orderedProject &&
                !IsProductionOrderProject(operativeOrder, orderedProject) &&
                !IsConstructionOrderProject(operativeOrder, orderedProject))
            {
                SetProject(agent.Id, orderedProject with { Stage = "paused", Blocker = "Following an owner order." });
                physical = agents[agent.Id];
            }
            if (physical.Project is { Stage: not ("completed" or "cancelled") } project &&
                operativeOrder is null &&
                (NeedsUrgentFood(physical) || NeedsUrgentWarmth(physical) && !IsProtectiveProject(project)))
            {
                SetProject(agent.Id, project with
                {
                    Stage = "paused",
                    Blocker = NeedsUrgentWarmth(physical) ? "Seeking warmth" : "Meeting food needs",
                });
                physical = agents[agent.Id];
            }
            if (IsConversationBusy(agent.Id) && !ShouldDispatchConversationChoice(agent.Id))
                continue;
            var conversationChoiceContext = ConversationChoiceContextFor(agent.Id);
            var candidates = CreateCandidates(agent.Id, physical)
                .Select(candidate => candidate with { DestinationName = DestinationNameForModel(candidate.DestinationId) })
                .ToList();
            if ((operativeOrder is null || !IsProductionOrderProject(operativeOrder, physical.Project)) &&
                PauseExpiredUnfillableHouseholdRecipeProject(agent.Id))
            {
                physical = agents[agent.Id];
                candidates = CreateCandidates(agent.Id, physical)
                    .Select(candidate => candidate with { DestinationName = DestinationNameForModel(candidate.DestinationId) })
                    .ToList();
            }
            var current = runtimes[agent.Id].CurrentIntention;
            if (!namingRetries.Contains(agent.Id) && !staleOrderDecisions.Contains(agent.Id) &&
                !staleRunDecisions.Contains(agent.Id) &&
                !awaitingDispatch.Contains(agent.Id) &&
                !NeedsCognition(agent.Id, current, candidates, conversationChoiceContext))
            {
                continue;
            }

            var generation = checked((int)(WorldTick + 1));
            var checkpoint = society.Checkpoint;
            var compaction = (checkpoint.MemoryCompactions ?? [])
                .SingleOrDefault(item => item.OwnerId == agent.Id);
            var retrievedMemories = WorldMemoryRetrieval.Retrieve(
                checkpoint.Memories,
                checkpoint.Beliefs ?? [],
                compaction,
                agent.Id,
                WorldTick,
                candidates, checkpoint);
            var observerGuidance = ObserverGuidanceFor(agent.Id);
            var requiresPersonalProvider = checkpoint.Births.Any(birth => birth.ChildId == agent.Id);
            var knownMapFacts = KnownMapFactsForCognition(agent.Id);
            var self = new CognitionSelfContext(agent.Id, agent.Name, agent.AgeBand.ToString(),
                physical.Personality, physical.Aspiration, CognitionHouseholdId(agent.HouseholdId),
                physical.Survival?.WarmthBasisPoints, physical.Survival?.IllnessBasisPoints,
                physical.RecentThoughts is { Count: > 0 } thoughts ? thoughts[^1].Text : null,
                checkpoint.Households.SingleOrDefault(item => item.Id == agent.HouseholdId)?.Name,
                towns.SingleOrDefault(item => item.ResidentIds.Contains(agent.Id, StringComparer.Ordinal))?.Name,
                GuardianPlacementModelNote(agent.Id), EquipmentNote(agent.Id), ContinuityNote(agent.Id),
                DepartureNote: DepartureNote(agent.Id), CivicNote: CivicNote(agent.Id),
                MedicalCareNote: MedicalCareNoteCore(agent.Id), TownMembershipNote: TownMembershipNote(agent.Id),
                ToolMakingRequestNote: ToolMakingRequestNoteCore(agent.Id),
                AllowedChildSurnames: agent.NeedsName && AgentNameRules.RequiresParentSurname(checkpoint, agent.Id)
                    ? AgentNameRules.AllowedChildSurnames(checkpoint, agent.Id) : null,
                MarriageNote: marriages.Where(item => AgentMarriageRules.HasParticipant(item, agent.Id))
                    .OrderBy(item => item.EndReceipt is not null).ThenByDescending(item => item.AcceptedTick)
                    .ThenBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault() is { } marriage
                    ? AgentMarriageRules.Note(marriage, agent.Id, checkpoint) : null,
                FamilyBackground: InitialChildFamilyBackground(agent.Id, physical),
                KnownRecipes: knowledge.Recipes.Where(item => item.OwnerId == agent.Id)
                    .OrderByDescending(item => item.LearnedTick).ThenBy(item => item.RecipeId, StringComparer.Ordinal).Take(16)
                    .Select(item => worldContent.Recipes.Single(recipe => recipe.CanonicalId == item.RecipeId).DisplayName).ToArray(),
                Skills: (physical.Skills ?? []).OrderBy(skill => skill.Kind)
                    .Select(skill => skill.Kind.ToString().ToLowerInvariant()).ToArray());
            var observation = new AgentObservation(
                agent.Id,
                WorldTick,
                society.Checkpoint.RunEpoch,
                generation,
                ObservationDigest(agent.Id, checkpoint.WorldId, physical, candidates, retrievedMemories,
                    [], knownMapFacts, self, observerGuidance),
                physical.FullnessBasisPoints,
                candidates,
                NeedsName: agent.NeedsName,
                RequiresPersonalProvider: requiresPersonalProvider,
                RetrievedMemories: retrievedMemories,
                KnownMapFacts: knownMapFacts, Self: self,
                NeedsPersonality: physical.IdentityChoicePending,
                NeedsAspiration: physical.IdentityChoicePending)
            {
                WorldId = checkpoint.WorldId,
                ObserverGuidance = observerGuidance,
                ConversationChoiceContext = conversationChoiceContext,
                OperativeOrderInstructionId = operativeOrder?.InstructionId,
            };
            if (jevEnabled && IsAdult(agent.Id) && providerFactory is not null)
            {
                try
                {
                    var provider = providerFactory(agent.Id);
                    if (provider.KindFor(observation) is DecisionProviderKind.Jev or DecisionProviderKind.OpenAiDecisions)
                    {
                        var memoryCandidates = WorldMemoryRetrieval.Unassessed(
                            checkpoint.Memories,
                            checkpoint.Beliefs ?? [],
                            compaction,
                            agent.Id,
                            WorldTick);
                        var summaryOptions = WorldMemoryRetrieval.SummaryOptions(checkpoint, agent.Id);
                        if (memoryCandidates.Count > 0 || summaryOptions.Count > 0)
                        {
                            observation = observation with
                            {
                                MemoryCompactionCandidates = memoryCandidates,
                                MemorySummaryOptions = summaryOptions,
                                ObservationDigest = ObservationDigest(
                                    agent.Id, checkpoint.WorldId, physical, candidates, retrievedMemories,
                                    memoryCandidates, knownMapFacts, self, observerGuidance, summaryOptions),
                            };
                        }
                    }
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    // Optional memory assistance cannot prevent the local
                    // retrieval path from supplying safe existing records.
                }
            }
            var accepted = society.EnqueueCognition(new SocietyCognitionScheduleEntry(
                $"tick:{WorldTick}:{agent.Id}",
                agent.Id,
                PriorityFor(physical),
                WorldTick,
                ["routine_tick"],
                observation));
            if (!accepted)
            {
                AppendEvent("cognition_backpressure", agent.Id);
            }
            else
            {
                foreach (var message in observerGuidance)
                {
                    var instruction = instructionsByIdempotency.Values.SingleOrDefault(item =>
                        item.InstructionId == message.InstructionId);
                    if (instruction is not null && instruction.GuidancePromptedTick is null)
                    {
                        instructionsByIdempotency[instruction.IdempotencyKey] = instruction with
                        {
                            GuidancePromptedTick = WorldTick,
                        };
                    }
                }
                if (observerGuidance.Length > 0)
                    checkpointSchemaVersion = StateSchemaVersion;
                agents[agent.Id] = agents[agent.Id] with
                {
                    LastDecisionContext = DecisionContext(physical, candidates, conversationChoiceContext),
                };
            }
        }
    }

    private bool IsOrderDecisionObservationCurrent(AgentObservation observation) =>
        observation.WorldId == society.Checkpoint.WorldId &&
        observation.RunEpoch == society.Checkpoint.RunEpoch &&
        (observation.OperativeOrderInstructionId == PendingInstructionFor(observation.AgentId)?.InstructionId ||
            IsFinishedOrderDecisionAwaitingReply(observation));

    private bool IsUndeliveredFinishedOrderDecision(AgentObservation observation) =>
        IsFinishedOrderDecisionAwaitingReply(observation) && instructionsByIdempotency.Values.Any(item =>
            item.InstructionId == observation.OperativeOrderInstructionId && item.ObservedTick is null);

    private bool IsFinishedOrderDecisionAwaitingReply(AgentObservation observation)
    {
        if (observation.OperativeOrderInstructionId is not { } orderId ||
            observation.WorldId != society.Checkpoint.WorldId ||
            observation.RunEpoch != society.Checkpoint.RunEpoch)
            return false;
        var instruction = instructionsByIdempotency.Values.SingleOrDefault(item =>
            item.InstructionId == orderId && item.TargetAgentId == observation.AgentId &&
            item.Kind == OwnerInstructionKind.Order && item.Task?.Status == "finished" &&
            item.ObserverReply is null && completedInstructionIds.Contains(orderId));
        // Local work can finish while its original personal reply is held.
        // Keep that exact message available for acknowledgement, never execution.
        return instruction is not null && observation.ObserverGuidance?.Any(message =>
            message.InstructionId == orderId && message.IssuerId == instruction.IssuerId &&
            message.TargetAgentId == instruction.TargetAgentId && message.Kind == "order" &&
            message.Text == instruction.Text && message.SubmittedTick == instruction.SubmittedTick &&
            message.RunEpoch == instruction.RunEpoch && message.SubmissionSequence == instruction.SubmissionSequence &&
            message.UnderstoodTask == UnderstoodTaskFor(instruction.Task?.Action) && message.ReplyAllowed) == true;
    }

    private bool NeedsCognition(
        string agentId,
        CognitionIntention? current,
        List<CognitionCandidate> candidates,
        string? conversationChoiceContext)
    {
        if (conversationChoiceContext is not null &&
            !HasPromptedConversationChoice(agents[agentId].LastDecisionContext, conversationChoiceContext))
        {
            return true;
        }

        // A new instruction prompts one fresh decision. If it cannot progress
        // yet, it waits for the agent's usual decisions instead of requesting
        // another (possibly paid) decision on every tick.
        if (HasNewObserverGuidanceFor(agentId))
        {
            return true;
        }

        if (HasNewGuardianCandidate(agents[agentId].LastDecisionContext, candidates))
        {
            return true;
        }

        // Cancellation or replacement ends an order-bound intention even when
        // its generic food candidate remains legal for ordinary personal plans.
        if (current?.OperativeOrderInstructionId is { } orderId &&
            orderId != PendingInstructionFor(agentId)?.InstructionId)
            return true;

        if (PendingInstructionFor(agentId) is { Task: { } order })
        {
            return current is null || checked(WorldTick - current.WorldTick) >= CognitionReevaluationIntervalTicks;
        }

        if (CanContinueEquipmentRepair(agentId) || CanContinueLesson(agentId) || CanContinueProject(agents[agentId]))
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
        if (agents[agentId].Project is { Stage: "completed" } completed &&
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
            var physical = agents[agentId];
            return DecisionContext(physical, candidates, conversationChoiceContext) != physical.LastDecisionContext ||
                checked(WorldTick - current.WorldTick) >= 300;
        }

        return checked(WorldTick - current.WorldTick) >= CognitionReevaluationIntervalTicks;
    }

    private string DecisionContext(
        AgentState state,
        List<CognitionCandidate> candidates,
        string? conversationChoiceContext = null)
    {
        var context = $"{NeedsUrgentFood(state)}:{NeedsUrgentWarmth(state)}:" +
            // Optional advance permission remains available on ordinary decisions;
            // a healthy idle agent need not wake merely because someone walks past.
            string.Join('|', candidates.Where(candidate =>
                    !candidate.Id.StartsWith(MedicalAllowPrefix, StringComparison.Ordinal) ||
                    state.Survival is { IllnessBasisPoints: >= 2_500 })
                .Select(candidate => candidate.Id).Order(StringComparer.Ordinal));
        context += "|guardian_candidates=" + GuardianCandidateContext(candidates);
        return conversationChoiceContext is null
            ? context
            : $"{context}|conversation_choice={ConversationChoiceContextDigest(conversationChoiceContext)}";
    }

    private static string GuardianCandidateContext(IEnumerable<CognitionCandidate> candidates)
    {
        var ids = candidates.Where(candidate => (candidate.Id.StartsWith("guardian_accept:", StringComparison.Ordinal) ||
            candidate.Id.StartsWith("guardian_relocate:", StringComparison.Ordinal) ||
            candidate.Id.StartsWith("guardian_follow:", StringComparison.Ordinal)))
            .Select(candidate => Convert.ToBase64String(Encoding.UTF8.GetBytes(candidate.Id))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_'))
            .Order(StringComparer.Ordinal).ToArray();
        return string.Join(',', ids);
    }

    private static bool HasNewGuardianCandidate(string? lastDecisionContext, IEnumerable<CognitionCandidate> candidates)
    {
        var previouslyOffered = GuardianCandidateContextFromLastDecision(lastDecisionContext);
        return candidates.Where(candidate => (candidate.Id.StartsWith("guardian_accept:", StringComparison.Ordinal) ||
            candidate.Id.StartsWith("guardian_relocate:", StringComparison.Ordinal) ||
            candidate.Id.StartsWith("guardian_follow:", StringComparison.Ordinal)))
            .Select(candidate => Convert.ToBase64String(Encoding.UTF8.GetBytes(candidate.Id))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_'))
            .Any(candidate => !previouslyOffered.Contains(candidate));
    }

    private static HashSet<string> GuardianCandidateContextFromLastDecision(string? context)
    {
        const string marker = "|guardian_candidates=";
        if (context is null) return new(StringComparer.Ordinal);
        var start = context.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return new(StringComparer.Ordinal);
        start += marker.Length;
        var end = context.IndexOf('|', start);
        var encodedIds = end < 0 ? context[start..] : context[start..end];
        return encodedIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
    }

    private static bool HasPromptedConversationChoice(string? lastDecisionContext, string conversationChoiceContext) =>
        lastDecisionContext?.EndsWith(
            $"|conversation_choice={ConversationChoiceContextDigest(conversationChoiceContext)}",
            StringComparison.Ordinal) == true;

    private static string ConversationChoiceContextDigest(string conversationChoiceContext) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(conversationChoiceContext)));

    private HashSet<string> ApplyContinuingIntentions(
        IEnumerable<string> dispatchedAgentIds,
        IEnumerable<string> pendingModelAgentIds)
    {
        var dispatched = dispatchedAgentIds.ToHashSet(StringComparer.Ordinal);
        var pendingModelCalls = pendingModelAgentIds.ToHashSet(StringComparer.Ordinal);
        var orderActorsHandledThisTick = new HashSet<string>(StringComparer.Ordinal);
        var runtimes = society.Capture().Cognition.Runtimes
            .ToDictionary(item => item.AgentId, StringComparer.Ordinal);
        foreach (var agent in society.Checkpoint.Agents
                     .Where(item => item.Status == SocietyAgentStatus.Active)
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (withheldActions?.Contains(agent.Id) == true) continue;
            if (dispatched.Contains(agent.Id) || !agents.TryGetValue(agent.Id, out var state))
            {
                continue;
            }
            if (attemptedActions is not null && runtimes[agent.Id].CurrentIntention is { } attempted)
                attemptedActions[agent.Id] = attempted.CandidateId;
            if (PassengerBoat(agent.Id) is not null)
            {
                if ((!pendingModelCalls.Contains(agent.Id) || PendingInstructionFor(agent.Id) is not null) && NeedsUrgentFood(state) &&
                    CreateCandidates(agent.Id, state).Any(candidate => candidate.Id == "consume_food"))
                    ApplyCandidate(agent.Id, state, "consume_food", reportIdle: false);
                continue;
            }
            var order = PendingInstructionFor(agent.Id);
            var waitingForModel = pendingModelCalls.Contains(agent.Id);
            if (waitingForModel && (order is null || !NeedsUrgentFood(state) && !NeedsUrgentWarmth(state)))
            {
                // A delayed reply supplies no new choice. Urgent survival and nearby
                // practical care may interrupt the task the agent already chose.
                if (order is null) orderActorsHandledThisTick.Add(agent.Id);
                if (TryApplyWaitingRoutine(agent.Id, state))
                {
                    orderActorsHandledThisTick.Add(agent.Id);
                    if (order is not null) SetOrderStatus(order, "interrupted", "Caring for a nearby dependent.");
                    continue;
                }
            }
            if (order is null && ContinueGuardianPlacement(agent.Id)) continue;
            if (IsConversationBusy(agent.Id))
            {
                continue;
            }
            if (agent.AgeBand == SocietyAgeBand.Infant)
            {
                continue;
            }
            if (order is not null)
            {
                orderActorsHandledThisTick.Add(agent.Id);
                var orderCandidate = OrderCandidateFor(order, state);
                var urgentCandidate = NeedsUrgentFood(state) || NeedsUrgentWarmth(state)
                    ? UrgentSurvivalCandidateFor(agent.Id, state, order)
                    : null;
                if (ShouldInterruptOrder(state, order, orderCandidate, urgentCandidate))
                {
                    SetOrderStatus(order, "interrupted", "Food or warmth needs come first.");
                    if (urgentCandidate is not null && IsMarketFoodCandidate(agent.Id, urgentCandidate.Id) &&
                        runtimes[agent.Id].CurrentIntention is { Provider: DecisionProviderKind.LargeLanguageModel } orderMarketIntention &&
                        orderMarketIntention.OperativeOrderInstructionId == order.InstructionId &&
                        IsMarketFoodCandidate(agent.Id, orderMarketIntention.CandidateId))
                        ContinueMarketWalk(agent.Id, orderMarketIntention.CandidateId);
                    else if (urgentCandidate is not null)
                        ApplyCandidate(agent.Id, state, urgentCandidate.Id, reportIdle: false);
                    continue;
                }
                if (order.Task!.WaitForDecisionAfterFailure)
                {
                    if (urgentCandidate is not null)
                        ApplyCandidate(agent.Id, state, urgentCandidate.Id, reportIdle: false);
                    continue;
                }
                if (orderCandidate is not null)
                {
                    ExecuteOrderStep(order, state, orderCandidate);
                    continue;
                }
                SetOrderStatus(order, "blocked", OrderBlockedReason(order, state));
                continue;
            }
            if (CanContinueEquipmentRepair(agent.Id))
            {
                ContinueEquipmentRepair(agent.Id);
                continue;
            }
            if (CanContinueLesson(agent.Id, waitingForModel))
            {
                ContinueLesson(agent.Id, waitingForModel);
                continue;
            }
            if (ContinueFarmWork(agent.Id)) continue;
            if (CanContinueProject(state, waitingForModel))
            {
                ContinueProject(agent.Id, state);
                continue;
            }
            if (runtimes[agent.Id].CurrentIntention is not { } intention)
            {
                continue;
            }

            if (intention.OperativeOrderInstructionId is not null &&
                intention.OperativeOrderInstructionId != order?.InstructionId)
                continue;

            if (intention.CandidateId.StartsWith(NonLandRemedyPrefix, StringComparison.Ordinal))
            {
                // Continue only the physical contribution the agent already chose. Every step
                // checks the live agreement, remaining goods, access and care needs again.
                if (intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    foreach (var town in towns.ToArray())
                        if (ApplyNonLandRemedyAction(town, agent.Id, intention.CandidateId)) break;
                continue;
            }
            if (intention.CandidateId.StartsWith(NonLandRelayPrefix, StringComparison.Ordinal)) continue;
            if (intention.CandidateId.StartsWith("civic|", StringComparison.Ordinal))
            {
                // Formal civic acts require a fresh admitted personal choice. Only the physical trip
                // toward the notice place continues locally between ordinary model turns.
                var civic = intention.CandidateId.Split('|');
                if (civic.Length == 5 && civic[2] == "visit" && intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    ContinueTownCivicVisit(agent.Id, intention.CandidateId);
                else if (civic.Length == 5 && civic[2] is "land_transfer_collect_payment" or "land_transfer_pay" or "land_transfer_meet" &&
                    intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    ContinueTownLandSaleWalk(agent.Id, intention.CandidateId);
                continue;
            }
            if (IsTownProjectDonationCandidate(intention.CandidateId))
            {
                if (intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    ContinueTownProjectDonationWalk(agent.Id, intention.CandidateId);
                continue;
            }
            if (IsMarketCandidate(intention.CandidateId))
            {
                if (intention.Provider == DecisionProviderKind.LargeLanguageModel && intention.OperativeOrderInstructionId is null)
                    ContinueMarketWalk(agent.Id, intention.CandidateId);
                continue;
            }
            if (IsOrnamentCandidate(intention.CandidateId))
            {
                if (intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    ContinueOrnamentWalk(agent.Id, intention.CandidateId);
                continue;
            }
            if (waitingForModel && !CanContinuePendingTask(state, intention.CandidateId)) continue;
            // Safe idle is always offered outside the conversation/order branches
            // above. EnqueueDueCognition still rebuilds choices to reconsider work.
            if (intention.CandidateId != "safe_idle" &&
                !CreateCandidates(agent.Id, state).Any(candidate => candidate.Id == intention.CandidateId)) continue;
            ApplyCandidate(agent.Id, state, intention.CandidateId, reportIdle: false);
        }

        return orderActorsHandledThisTick;
    }

    private void ApplySafeRoutinesWhileWaiting(
        IEnumerable<string> waitingIds,
        IEnumerable<string> orderActorsHandledThisTick)
    {
        var handledOrders = orderActorsHandledThisTick.ToHashSet(StringComparer.Ordinal);
        foreach (var id in waitingIds.OrderBy(item => item, StringComparer.Ordinal))
        {
            if (!agents.TryGetValue(id, out var state)) continue;
            if (handledOrders.Contains(id) || HasGuardianPlacementTask(id) && guardianPlacementActions.Contains(id)) continue;
            if (PendingInstructionFor(id) is not null) continue;
            TryApplyWaitingRoutine(id, state);
        }
    }

    private bool TryApplyWaitingRoutine(string id, AgentState state)
    {
        if (withheldActions?.Contains(id) == true) return false;
        if (NeedsUrgentFood(state) || NeedsUrgentWarmth(state))
        {
            var candidate = CreateCandidates(id, state, restrictForOrder: false)
                .Where(item => IsWaitingSurvivalCandidate(item.Id))
                .OrderBy(item => item.DeterministicPriority)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (candidate is not null) ApplyCandidate(id, state, candidate.Id, reportIdle: false);
            return true;
        }
        if (!IsAdult(id) || !ReadyForBriefInteraction(id)) return false;
        var nearby = ChildrenNeedingCare(id).Select(child => child.AgentId).Concat(IllDependentsNeedingCare(id))
            .Where(dependent => IsWithinInteractionRange(state.Position, agents[dependent].Position, ResourceInteractionRange))
            .ToHashSet(StringComparer.Ordinal);
        if (nearby.Count == 0) return false;
        var care = CreateCandidates(id, state, restrictForOrder: false).Where(candidate =>
                candidate.Id.StartsWith("care:", StringComparison.Ordinal) && nearby.Contains(candidate.Id[5..]) ||
                candidate.Id.StartsWith("guardian_tend:", StringComparison.Ordinal) && nearby.Contains(candidate.Id[14..]))
            .OrderBy(candidate => candidate.DeterministicPriority).ThenBy(candidate => candidate.Id, StringComparer.Ordinal).FirstOrDefault();
        if (care is null) return false;
        // Use the ordinary physical care boundary without cancelling an existing
        // repair or starting, ending or accepting a caregiving relationship.
        if (care.Id.StartsWith("care:", StringComparison.Ordinal)) CareForChild(id, care.Id[5..], care.ActionPlan);
        else TendToIllDependent(id, care.Id[14..], care.ActionPlan);
        return true;
    }

    private static bool IsWaitingSurvivalCandidate(string id) => id is
        "consume_food" or "collect_shared_food" or "take_food_from_pot" or "make_room_for_food" or "recover_household_delivery" or
        "harvest_food" or "seek_food" or "wear_clothing" or "tend_fire" or "seek_warmth" ||
        id.StartsWith(TownProjectReturnPrefix, StringComparison.Ordinal);

    private bool CanContinuePendingTask(AgentState state, string id) =>
        IsWaitingSurvivalCandidate(id) || IsTownProjectCandidate(id) || id is "knowledge_continue" or "haul_household_stock" or
            "store_household_food" or "store_town_resources" or "haul_farm_grain" or "haul_farm_flour" or
            "haul_smith_input" or "gather_smith_ore" or "deliver_smith_ore" or "collect_water_jug" or "return_water_jug" or
            "store_food_in_pot" ||
        KnowledgeWritingFor(state.AgentId) is { OrderInstructionId: null } writing && writing.CandidateId == id ||
        id == "explore" && state.Exploration?.OutingPath.Count > 0 ||
        state.Exploration is { Returning: false, OutingPath.Count: > 0, Goal: { OrderInstructionId: null } goal } &&
            ExplorationGoalCandidateId(goal) == id ||
        id.StartsWith("assist:", StringComparison.Ordinal) ||
        id.StartsWith(GatherBlacksmithInputPrefix, StringComparison.Ordinal) ||
        id.StartsWith(GatherBuildingMaterialPrefix, StringComparison.Ordinal) ||
        id.StartsWith(RareMiningPrefix, StringComparison.Ordinal) ||
        id.StartsWith(SupplyWorkstationPrefix, StringComparison.Ordinal) ||
        id.StartsWith(ReturnEmptyVesselPrefix, StringComparison.Ordinal) ||
        id.StartsWith(FillWaterJugPrefix, StringComparison.Ordinal) ||
        id.StartsWith(PullCartPrefix, StringComparison.Ordinal) ||
        id.StartsWith("boat_queue:", StringComparison.Ordinal) ||
        id.StartsWith("animal:supply_trip:", StringComparison.Ordinal) ||
        id.StartsWith("animal:care:", StringComparison.Ordinal) ||
        id.StartsWith("animal:collect:", StringComparison.Ordinal) ||
        id.StartsWith("animal:lead_home:", StringComparison.Ordinal);

    private void ApplyDecision(SocietyCognitionDispatchResult decision)
    {
        if (decision.ActionPlan is { } plan &&
            (decision.PlanRefusal ?? CheckActionPlan(plan).Refusal) is { } refusal)
        {
            RefuseChoice(plan, refusal);
            return;
        }
        if (attemptedActions is not null && decision.Admission.Intention is { } attempted)
            attemptedActions[decision.AgentId] = attempted.CandidateId;
        if (withheldActions?.Contains(decision.AgentId) == true) return;
        if (decision.Admission.Accepted && !decision.Admission.FellBack &&
            decision.Admission.Intention?.Provider is DecisionProviderKind.Jev or DecisionProviderKind.OpenAiDecisions &&
            decision.Admission.MemorySummary is { } summary)
        {
            ApplyMemorySummary(decision.AgentId, summary);
        }
        if (decision.Admission.Accepted && !decision.Admission.FellBack &&
            decision.Admission.Intention?.Provider is DecisionProviderKind.Jev or DecisionProviderKind.OpenAiDecisions &&
            decision.Admission.MemoryCompactionScores is { Count: > 0 } memoryScores)
        {
            ApplyMemoryCompaction(decision.AgentId, memoryScores);
        }

        if (!decision.Admission.Accepted || decision.Admission.Intention is null ||
            !agents.TryGetValue(decision.AgentId, out var state))
        {
            AppendEvent("cognition_rejected", $"{decision.AgentId}:{decision.Admission.Outcome}");
            return;
        }

        _ = ApplyObserverGuidanceResult(decision.AgentId, decision.Admission);
        if (ApplyMedicalConsentDecision(decision) || ApplyOrnamentDecision(decision) || ApplyAnimalConsentDecision(decision))
            return;
        var pendingInstruction = PendingInstructionFor(decision.AgentId);
        var candidateId = decision.Admission.Intention.CandidateId;
        if (PassengerBoat(decision.AgentId) is not null && candidateId is not ("consume_food" or "drink_milk" or "safe_idle"))
        {
            AppendEvent("boat_action_blocked", $"{decision.AgentId}:stay_aboard");
            return;
        }
        if (PassengerBoat(decision.AgentId) is not null)
        {
            ApplyCandidate(decision.AgentId, state, candidateId, reportIdle: true);
            return;
        }
        if (candidateId != "safe_idle") guardianPlacementActions.Add(decision.AgentId);
        if (decision.Admission.Intention.OperativeOrderInstructionId != pendingInstruction?.InstructionId)
        {
            AppendEvent("instruction_order_stale_decision", decision.AgentId);
            return;
        }

        if (pendingInstruction is { } order)
        {
            if (TalkOrderHasConversation(order))
            {
                ApplyTalkOrderConversationDecision(order, decision);
                return;
            }
            if (decision.Admission.FellBack)
            {
                SetOrderStatus(order, "blocked", "The order will try again after a short wait.", waitForDecision: true);
                ApplyCandidate(decision.AgentId, state, candidateId, reportIdle: true);
            }
            else
            {
                var orderCandidate = OrderCandidateFor(order, state);
                var urgentCandidate = NeedsUrgentFood(state) || NeedsUrgentWarmth(state)
                    ? UrgentSurvivalCandidateFor(decision.AgentId, state, order)
                    : null;
                if (ShouldInterruptOrder(state, order, orderCandidate, urgentCandidate))
                {
                    SetOrderStatus(order, "interrupted", "Food or warmth needs come first.");
                    if (decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel &&
                        IsMarketFoodCandidate(decision.AgentId, candidateId))
                        ApplyMarketCandidate(decision.AgentId, state, candidateId, decision.ActionPlan);
                    else if (decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel &&
                        decision.ActionPlan is ShopBuyActionPlan or ShopResponseActionPlan &&
                        IsShopFoodCandidate(decision.AgentId, candidateId, decision.ActionPlan as ShopBuyActionPlan))
                        ApplyShopCandidate(decision.AgentId, state, candidateId, decision.ActionPlan);
                    else if (urgentCandidate is not null)
                        ApplyCandidate(decision.AgentId, state, urgentCandidate.Id, reportIdle: true);
                }
                else if (orderCandidate is not null)
                {
                    ExecuteOrderStep(order, state, orderCandidate);
                }
                else
                {
                    SetOrderStatus(order, "blocked", OrderBlockedReason(order, state));
                    ApplyCandidate(decision.AgentId, state, "safe_idle", reportIdle: true);
                }
            }
        }
        else
        {
            if (candidateId.StartsWith("civic|", StringComparison.Ordinal))
            {
                if (!decision.Admission.FellBack && decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    ApplyPlannedTownCivicCandidate(decision.AgentId, candidateId, decision.Admission.CivicProposal,
                        decision.Admission.CivicBallot, decision.Admission.CivicLandTiles, decision.Admission.CivicLandHearing, decision.Admission.CivicNonLandHearing, decision.ActionPlan);
            }
            else if (candidateId.StartsWith("nonviolent_remedy:", StringComparison.Ordinal) || candidateId.StartsWith("nonviolent_relay:", StringComparison.Ordinal))
            {
                if (!decision.Admission.FellBack && decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    foreach (var town in towns.ToArray())
                        if (ApplyNonLandRemedyAction(town, decision.AgentId, candidateId, decision.ActionPlan)) break;
            }
            else if (IsTownProjectDonationCandidate(candidateId))
            {
                if (!decision.Admission.FellBack && decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    ApplyTownProjectDonation(decision.AgentId, candidateId, decision.ActionPlan as TownProjectDonationActionPlan);
            }
            else if (IsMarketCandidate(candidateId))
            {
                if (!decision.Admission.FellBack && decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel)
                    ApplyMarketCandidate(decision.AgentId, state, candidateId, decision.ActionPlan);
            }
            else if (!ApplyToolMakingRequestDecision(decision))
                ApplyRegisteredCandidate(decision.AgentId, state, candidateId, reportIdle: true, decision.ActionPlan);
        }

    }

    private List<string> ApplyObserverGuidanceResult(
        string agentId,
        CognitionAdmissionResult admission)
    {
        if (!admission.Accepted || admission.FellBack ||
            admission.Intention?.Provider != DecisionProviderKind.LargeLanguageModel ||
            admission.ObserverGuidance is not { } result ||
            result.WorldId != society.Checkpoint.WorldId ||
            result.AgentId != agentId ||
            result.RunEpoch != admission.Intention.RunEpoch ||
            result.DecisionGeneration != admission.Intention.DecisionGeneration ||
            result.ObservationDigest != admission.Intention.ObservationDigest ||
            !agents.ContainsKey(agentId))
            return [];

        var replies = result.Replies.ToDictionary(reply => reply.InstructionId, StringComparer.Ordinal);
        var completedSuggestions = new List<string>();
        foreach (var message in result.Messages)
        {
            var instruction = instructionsByIdempotency.Values.SingleOrDefault(item =>
                item.InstructionId == message.InstructionId);
            if (instruction is null || instruction.IssuerId != message.IssuerId ||
                instruction.TargetAgentId != agentId ||
                instruction.TargetAgentId != message.TargetAgentId ||
                ToWireValue(instruction.Kind) != message.Kind || instruction.Text != message.Text ||
                instruction.SubmittedTick != message.SubmittedTick || instruction.RunEpoch != message.RunEpoch ||
                instruction.SubmissionSequence != message.SubmissionSequence ||
                (instruction.Kind == OwnerInstructionKind.Order &&
                 UnderstoodTaskFor(instruction.Task?.Action) != message.UnderstoodTask) ||
                (instruction.Kind == OwnerInstructionKind.Suggestion && message.UnderstoodTask is not null) ||
                (instruction.ObserverReply is null) != message.ReplyAllowed ||
                completedInstructionIds.Contains(instruction.InstructionId) &&
                    !(instruction.Kind == OwnerInstructionKind.Order && instruction.Task?.Status == "finished"))
                continue;

            var observerReply = replies.TryGetValue(instruction.InstructionId, out var reply)
                ? reply.Text
                : instruction.ObserverReply;
            instructionsByIdempotency[instruction.IdempotencyKey] = instruction with
            {
                ObservedTick = instruction.ObservedTick ?? admission.Intention.WorldTick,
                ObserverReply = observerReply,
            };
            checkpointSchemaVersion = StateSchemaVersion;
            // A reply for an order that has since finished is set aside as stale,
            // so a suggestion it carried stays open for the next fresh request.
            if (instruction.Kind == OwnerInstructionKind.Suggestion &&
                admission.Intention.OperativeOrderInstructionId == PendingInstructionFor(agentId)?.InstructionId &&
                completedInstructionIds.Add(instruction.InstructionId))
                completedSuggestions.Add(instruction.InstructionId);
        }

        return completedSuggestions;
    }

    private void ApplyCandidate(string agentId, AgentState state, string candidateId, bool reportIdle) =>
        ApplyRegisteredCandidate(agentId, state, candidateId, reportIdle);

    private void ApplyRegisteredCandidate(string agentId, AgentState state, string candidateId, bool reportIdle, ActionPlan? plan = null)
    {
        if (withheldActions?.Contains(agentId) == true || IsRefusedAction(agentId, candidateId)) return;
        if (attemptedActions is not null) attemptedActions[agentId] = candidateId;
        if (candidateId != "safe_idle") guardianPlacementActions.Add(agentId);
        if (TryRegisteredAction(ActionPhase.BeforeAge, new(agentId, state, candidateId, reportIdle, plan))) return;
        if (!AgePermitsCandidate(agentId, candidateId))
        {
            AppendEvent("age_action_rejected", $"{agentId}:{candidateId}");
            return;
        }
        if (agents[agentId].Equipment?.Repair is not null && candidateId != "repair_equipment")
        {
            CancelEquipmentRepair(agentId);
            state = agents[agentId];
        }
        if (PendingInstructionFor(agentId) is null && ContinueFarmWork(agentId)) return;
        if (TryRegisteredAction(ActionPhase.Ordinary, new(agentId, state, candidateId, reportIdle, plan))) return;
        if (reportIdle) AppendEvent("agent_idle", agentId);
    }

    private void ApplyBuildDecision(
        string agentId,
        AgentState state,
        string candidateId)
    {
        if (!TownConstructionCandidateIds.TryParse(candidateId, out var selection))
        {
            AppendEvent("build_rejected", $"{agentId}:{candidateId}:unknown_target");
            return;
        }

        if (selection.IsBuilding)
        {
            var definition = worldContent.Buildings.SingleOrDefault(item => item.CanonicalId == selection.DefinitionId);
            var layout = CreateTownLayoutContext(agentId, selection.SitePosition, definition);
            TownConstructionSiteCandidate? rankedSite = null;
            if (definition is not null)
            {
                if (selection.SitePosition is { } selectedSite)
                    TownLayoutService.TryEvaluateConstructionSite(layout, definition, selectedSite, out rankedSite);
                else
                {
                    var sites = TownLayoutService.RankConstructionSites(layout, definition);
                    rankedSite = sites.Count == 0 ? null : sites[0];
                }
            }

            if (definition is null || rankedSite is null)
            {
                AppendEvent("build_rejected", $"{agentId}:{candidateId}:selected_site_no_longer_legal");
                if (selection.SitePosition is { } rejectedSite && definition is not null)
                    AppendEvent("town_layout_site_rejected",
                        $"{TownForResident(agentId) ?? "none"}|{agentId}|{definition.CanonicalId}|{rejectedSite.X}|{rejectedSite.Y}|site_unavailable");
                return;
            }
            var householdProperty = definition.Tags.Any(IsHouseholdBuildingTag);
            var houseOwner = householdProperty
                ? society.Checkpoint.GetAgent(agentId).HouseholdId : null;
            if (householdProperty && houseOwner is null)
            {
                AppendEvent("build_rejected", $"{agentId}:{candidateId}:household_required");
                return;
            }
            if (definition.Tags.Contains("house", StringComparer.Ordinal) && houseOwner is not null &&
                HouseForHousehold(houseOwner) is not null)
            {
                AppendEvent("build_rejected", $"{agentId}:{candidateId}:existing_house");
                return;
            }

            var position = rankedSite.Position;
            if (state.Position != position)
            {
                MoveToward(agentId, state, position, "build");
                return;
            }

            var constructionOrder = ConstructionInstructionForProject(agentId, state.Project);
            if (state.Project?.OrderInstructionId is not null && (constructionOrder is null ||
                state.Project.WorkDone < ProjectWorkTicks ||
                !TryConstructionOrderSite(constructionOrder, state, out _, out _, out _, out _)))
            {
                AppendEvent("build_rejected", $"{agentId}:{candidateId}:ordered_project_changed");
                return;
            }
            var materialOwner = BuildingConstructionOwner(agentId, definition);
            var placement = PlaceBuildingCore(
                constructionOrder?.Task!.ConstructionInstanceId ?? BuildInstanceId(agentId, definition),
                definition.CanonicalId,
                position,
                "build_completed",
                TownForResident(agentId),
                houseOwner,
                materialOwner,
                constructionOrder?.InstructionId);
            if (!placement.Applied)
            {
                AppendEvent("build_rejected", $"{agentId}:{candidateId}:{placement.Failure}");
            }
            else
            {
                CreditCompletedWork(agentId, "building");
                if (constructionOrder is not null)
                    RecordConstructionOrderCompletion(constructionOrder, materialOwner);
            }

            return;
        }

        var recipeId = selection.DefinitionId;
        var recipe = worldContent.Recipes.SingleOrDefault(item => item.CanonicalId == recipeId);
        if (recipe is null || !TryFindRecipeSite(recipe, out var siteId, out var sitePosition, agentId))
        {
            AppendEvent("build_rejected", $"{agentId}:{candidateId}:no_valid_site");
            return;
        }

        if (state.Position != sitePosition)
        {
            MoveToward(agentId, state, sitePosition, "build");
            return;
        }

        var productionInstruction = PendingInstructionFor(agentId);
        var orderInstructionId = productionInstruction is not null &&
            IsProductionOrderProject(productionInstruction, state.Project) &&
            state.Project?.WorkDone == ProjectWorkTicks && productionInstruction.Task!.ProductionBuildingId == siteId
                ? productionInstruction.InstructionId : null;
        var started = StartProductionCore(recipe.CanonicalId, siteId, agentId, "build_started", orderInstructionId);
        if (!started.Applied)
        {
            AppendEvent("build_rejected", $"{agentId}:{candidateId}:{started.Failure}");
        }
    }

    private List<CognitionCandidate> CreateCandidates(
        string agentId,
        AgentState state,
        bool restrictForOrder = true)
        => CreateRegisteredCandidates(agentId, state, restrictForOrder);

    private List<CognitionCandidate> CreateRegisteredCandidates(string agentId, AgentState state, bool restrictForOrder)
    {
        var candidates = CreateUnfilteredCandidates(agentId, state, restrictForOrder);
        if (actionRefusals.Count > 0)
            candidates.RemoveAll(candidate => IsRefusedAction(agentId, candidate.Id));
        if (candidates.Count == 0) candidates.Add(new("safe_idle", "Wait safely.", 100));
        return candidates;
    }

    private List<CognitionCandidate> CreateUnfilteredCandidates(
        string agentId, AgentState state, bool restrictForOrder)
    {
        if (PassengerBoat(agentId) is not null)
        {
            var aboard = new List<CognitionCandidate> { new("safe_idle", "Stay aboard while the boat travels or waits for a safe Port.", 100) };
            if (state.FullnessBasisPoints < ComfortableFullness)
                AddFoodPlanCandidate(aboard, PlanFoodConsumption(agentId), "Eat one carried food item aboard the boat.", 0);
            if (state.FullnessBasisPoints < ComfortableFullness) AddMilkCandidate(aboard, agentId);
            return aboard;
        }
        var currentConversation = ConversationFor(agentId);
        var candidates = currentConversation is null
            ? new List<CognitionCandidate>()
            : ConversationCandidates(agentId).ToList();
        if (currentConversation is not null &&
            (currentConversation.Status is AgentConversationStatus.Ready or AgentConversationStatus.AwaitingSpeaker or AgentConversationStatus.WrapUp ||
             currentConversation.Status == AgentConversationStatus.Proposed && currentConversation.InitiatorId == agentId))
        {
            if (candidates.Count == 0)
                candidates.Add(new CognitionCandidate("safe_idle", "Continue safely without starting a new task.", 100));
            return candidates;
        }
        var context = new ChoiceContext(candidates, agentId, state, currentConversation is not null);
        AddRegisteredChoices(ChoiceGroup.Prelude, context);
        if (IsAdult(agentId)) AddRegisteredChoices(ChoiceGroup.Adult, context);
        if (!NeedsUrgentWarmth(state) && IsChild(agentId)) AddRegisteredChoices(ChoiceGroup.Child, context);
        if (!NeedsUrgentFood(state) && IsAdult(agentId))
        {
            var agent = society.Checkpoint.GetAgent(agentId);
            var reachableToolCache = new Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>();
            var accessibleWarehouses = new Lazy<PlacedBuilding[]>(() => WarehousesAccessibleTo(agentId).ToArray(),
                LazyThreadSafetyMode.None);
            var buildingsWithoutSites = new List<string>();
            AddRegisteredChoices(ChoiceGroup.UnhurriedAdult, context with
            {
                Agent = agent,
                ReachableTools = reachableToolCache,
                AccessibleWarehouses = accessibleWarehouses,
                BuildingsWithoutSites = buildingsWithoutSites,
            });
        }
        if (NeedsUrgentFood(state) && IsAdult(agentId)) AddRegisteredChoices(ChoiceGroup.UrgentAdult, context);
        AddRegisteredChoices(ChoiceGroup.Final, context);
        if (restrictForOrder && PendingInstructionFor(agentId) is { } order)
            return RestrictCandidatesForOrder(candidates, order, state);
        return candidates;
    }

    private List<CognitionCandidate> RestrictCandidatesForOrder(
        List<CognitionCandidate> candidates,
        OwnerQueuedInstruction order,
        AgentState state)
    {
        var safeIdle = candidates.Single(item => item.Id == "safe_idle");
        var urgent = NeedsUrgentFood(state) || NeedsUrgentWarmth(state);
        var urgentCandidate = urgent
            ? SelectOrderSurvivalCandidate(candidates, state, order)
            : null;
        if (TalkOrderHasConversation(order))
            return candidates.Where(item => item.Id == "safe_idle" || item.Id.StartsWith("conversation_", StringComparison.Ordinal) ||
                urgent && IsSurvivalCandidate(state.AgentId, item.Id)).ToList();
        var taskCandidate = OrderCandidateFor(order, state);
        if (ShouldInterruptOrder(state, order, taskCandidate, urgentCandidate))
            taskCandidate = null;
        if (taskCandidate is null)
        {
            return candidates.Where(item => item.Id == "safe_idle" || urgent && IsSurvivalCandidate(state.AgentId, item.Id))
                .ToList();
        }

        var selected = candidates.Where(item => item.Id == taskCandidate.Id ||
            urgent && IsSurvivalCandidate(state.AgentId, item.Id)).ToList();
        if (IsShelterOrder(order.Task!.Action))
            selected.RemoveAll(item => item.Id == taskCandidate.Id);
        if (selected.All(item => item.Id != taskCandidate.Id)) selected.Add(taskCandidate);
        if (selected.All(item => item.Id != safeIdle.Id)) selected.Add(safeIdle);
        return selected;
    }

    private void AddBuildCandidates(
        List<CognitionCandidate> candidates,
        SocietyAgent agent,
        AgentState state,
        Dictionary<(ToolFamily Family, int Tier), ToolDefinition?> reachableToolCache,
        Lazy<PlacedBuilding[]> accessibleWarehouses,
        List<string> buildingsWithoutSites)
    {
        if (agent.HouseholdId is { } planningHousehold)
            AddHouseholdBuildingPlans(candidates, agent, state, planningHousehold, buildingsWithoutSites);

        // Work follows what the household holds, not a role: crops need the
        // household's Farmhouse, and workstation recipes need a building the
        // household holds or a communal one (see TryFindRecipeSite).
        foreach (var recipe in worldContent.Recipes.Where(item =>
                     !item.Outputs.Any(output => output.ResourceId == "bedding") &&
                     !item.IsCrop && !IsGenericFoodRecipe(item)))
        {
            if (NeedsUrgentWarmth(state) && !recipe.Outputs.Any(output => PersonalEquipmentRules.IsGarment(output.ResourceId)))
            {
                continue;
            }
            var householdWorkstation = recipe.WorkstationBuildingId is { } workstationId &&
                worldContent.Buildings.Any(definition => definition.CanonicalId == workstationId &&
                    definition.Tags.Any(IsHouseholdBuildingTag));
            var personalCart = IsHandcartRecipe(recipe);
            var recipeOwner = personalCart ? agent.Id : ProductionOwnerFor(null, agent.Id);
            if (personalCart && (!HasCarriedUnreservedQuantities(agent.Id, recipe.Inputs) ||
                society.Checkpoint.Inventory.Lots.Any(lot => lot.ItemKind == InventoryContainerRules.Handcart && lot.OwnerId == agent.Id)))
                continue;
            if (!NeedsRecipeOutput(recipe, recipeOwner, agent.Id) || AnotherAgentWaitsForWorkSite(agent.Id, recipe) ||
                !CanAcquireProjectInputs(recipe.Inputs, recipeOwner, agent.Id, reachableToolCache, accessibleWarehouses) ||
                !TryFindRecipeSite(recipe, out var siteId, out var position, agent.Id) ||
                householdWorkstation && !personalCart &&
                (recipeOwner is null || !HasIngredientsAtBuilding(recipe.Inputs, recipeOwner, siteId)))
            {
                continue;
            }

            candidates.Add(new CognitionCandidate(
                $"build:recipe:{recipe.CanonicalId}",
                $"{recipe.DisplayName} at the household work site.",
                recipe.Tags.Contains("named-meal", StringComparer.Ordinal) ? 20 : recipe.IsCrop ? 20 :
                    HouseToolsContent.IsCrudeToolRecipe(recipe) ? 23 :
                    OutdoorExposure(state.Position) > 0 && recipe.Outputs.Any(output => PersonalEquipmentRules.IsGarment(output.ResourceId)) ? 25 : 30,
                $"build-site:{position.X},{position.Y}"));
        }
    }

    private string? DestinationNameForModel(string? id)
    {
        if (id is null) return null;
        return society.Checkpoint.Households.SingleOrDefault(item => item.Id == id)?.Name ??
            Animal(id)?.Name ??
            towns.SingleOrDefault(item => item.Id == id)?.Name ??
            society.Checkpoint.Agents.SingleOrDefault(item => item.Id == id)?.Name ??
            map.Resources.SingleOrDefault(item => item.Id == id)?.Kind.Replace('_', ' ');
    }

    private int PriorityFor(AgentState state) =>
        NeedsUrgentFood(state) || NeedsUrgentWarmth(state) ? 20 : 0;

    private static string ObservationDigest(
        string agentId,
        string worldId,
        AgentState state,
        IReadOnlyList<CognitionCandidate> candidates,
        IReadOnlyList<CognitionMemoryExcerpt> memories,
        IReadOnlyList<CognitionMemoryCompactionCandidate> compactionCandidates,
        IReadOnlyList<CognitionKnowledgeFact> knownMapFacts,
        CognitionSelfContext self,
        IReadOnlyList<CognitionObserverGuidance> observerGuidance,
        IReadOnlyList<CognitionMemorySummaryOption>? summaryOptions = null)
    {
        var text = new StringBuilder()
            .Append("clankerworld.world-observation/v2|")
            .Append(agentId).Append('|')
            .Append(worldId.Length).Append(':').Append(worldId).Append('|')
            .Append(state.Position.X).Append(',').Append(state.Position.Y).Append('|')
            .Append(state.FullnessBasisPoints).Append('|')
            .Append(string.Join(',', candidates.Select(candidate => candidate.Id)));
        foreach (var memory in memories)
            text.Append('|').Append(memory.Id.Length).Append(':').Append(memory.Id)
                .Append('|').Append(memory.SourceTick)
                .Append('|').Append(memory.SubjectId.Length).Append(':').Append(memory.SubjectId)
                .Append('|').Append(memory.Summary.Length).Append(':').Append(memory.Summary)
                .Append('|').Append(memory.Kind)
                .Append('|').Append(memory.Visibility ?? "none")
                .Append('|').Append(memory.Provenance ?? "none")
                .Append('|').Append(memory.ConfidenceBasisPoints ?? -1)
                .Append('|').Append(memory.SourceAgentId ?? "none")
                .Append('|').Append(memory.SourceEventId ?? -1)
                .Append('|').Append(memory.IsCorrected)
                .Append('|').Append(memory.ImportanceBasisPoints)
                .Append('|').Append(memory.ImportanceConfidenceBasisPoints);
        text.Append("|summaries=").Append(JsonSerializer.Serialize(summaryOptions ?? []));
        text.Append("|summary_sources=").Append(JsonSerializer.Serialize(memories.Select(memory => memory.SummarySources)));
        foreach (var memory in compactionCandidates)
            text.Append('|').Append(memory.Kind)
                .Append('|').Append(memory.Id.Length).Append(':').Append(memory.Id)
                .Append('|').Append(memory.SourceTick)
                .Append('|').Append(memory.SubjectId.Length).Append(':').Append(memory.SubjectId)
                .Append('|').Append(memory.Summary.Length).Append(':').Append(memory.Summary)
                .Append('|').Append(memory.Visibility ?? "none")
                .Append('|').Append(memory.Provenance ?? "none")
                .Append('|').Append(memory.ConfidenceBasisPoints ?? -1)
                .Append('|').Append(memory.SourceAgentId ?? "none")
                .Append('|').Append(memory.SourceEventId ?? -1)
                .Append('|').Append(memory.IsCorrected);
        foreach (var fact in knownMapFacts)
            text.Append("|map=").Append(fact.X).Append(',').Append(fact.Y).Append('|')
                .Append(fact.Terrain).Append('|').Append(string.Join(',', fact.ResourceKinds))
                .Append('|').Append(fact.DiscovererId).Append('|').Append(fact.Acquisition)
                .Append('|').Append(fact.LearnedTick);
        foreach (var message in observerGuidance)
            text.Append("|observer=").Append(message.InstructionId.Length).Append(':').Append(message.InstructionId)
                .Append('|').Append(message.IssuerId.Length).Append(':').Append(message.IssuerId)
                .Append('|').Append(message.TargetAgentId.Length).Append(':').Append(message.TargetAgentId)
                .Append('|').Append(message.Kind)
                .Append('|').Append(message.Text.Length).Append(':').Append(message.Text)
                .Append('|').Append(message.SubmittedTick)
                .Append('|').Append(message.RunEpoch)
                .Append('|').Append(message.SubmissionSequence)
                .Append('|').Append(message.UnderstoodTask ?? "none")
                .Append('|').Append(message.ReplyAllowed);
        text.Append("|self=").Append(JsonSerializer.Serialize(self))
            .Append("|identity_pending=").Append(state.IdentityChoicePending);
        return $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))}";
    }

}
