using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal static void EnqueueDueCognition(ICognitionObservationsWorld world, IReadOnlySet<string> activeModelCallIds)
    {
        var cognitionState = world.Capture().Cognition;
        var runtimes = cognitionState.Runtimes
            .ToDictionary(item => item.AgentId, StringComparer.Ordinal);
        var namingRetries = cognitionState.Queue
            .Where(entry => entry.TriggerIds.Contains(
                SocietyCognitionScheduler.NameRetryTriggerId, StringComparer.Ordinal))
            .Select(entry => entry.AgentId)
            .ToHashSet(StringComparer.Ordinal);
        var staleRunDecisions = cognitionState.Queue
            .Where(entry => entry.Observation.RunEpoch != world.Society.RunEpoch)
            .Select(entry => entry.AgentId)
            .ToHashSet(StringComparer.Ordinal);
        // Retained work has already advanced LastDecisionContext. Once its
        // earlier call ends, rebuild it even if the ordinary cadence is quiet.
        var awaitingDispatch = cognitionState.Queue
            .Where(entry => !activeModelCallIds.Contains(entry.AgentId))
            .Select(entry => entry.AgentId)
            .ToHashSet(StringComparer.Ordinal);
        var waitingForModelIds = world.PendingModelAgentIds();
        var waitingOrderDecisions = cognitionState.Queue
            .Where(entry => waitingForModelIds.Contains(entry.AgentId) &&
                entry.Observation.OperativeOrderInstructionId is not null &&
                world.IsOrderDecisionObservationCurrent(entry.Observation) &&
                (!awaitingDispatch.Contains(entry.AgentId) ||
                 world.IsUndeliveredFinishedOrderDecision(entry.Observation)))
            .Select(entry => entry.AgentId)
            .ToHashSet(StringComparer.Ordinal);
        var staleOrderDecisions = cognitionState.Queue
            .Where(entry => waitingForModelIds.Contains(entry.AgentId) &&
                entry.Observation.OperativeOrderInstructionId is not null &&
                !world.IsOrderDecisionObservationCurrent(entry.Observation))
            .Select(entry => entry.AgentId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var agent in world.Society.Agents
                     .Where(item => item.Status == SocietyAgentStatus.Active)
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (LifeStageRules.IsInfant(agent.AgeBand))
            {
                continue;
            }
            // Submission already closes these. Recognition can change between
            // versions, so orders that are already waiting follow the same rule.
            world.CloseOrdersNotUnderstood(agent.Id);
            var physical = world.Agents[agent.Id];
            var operativeOrder = world.PendingInstructionFor(agent.Id);
            if (waitingOrderDecisions.Contains(agent.Id) && !world.HasNewObserverGuidanceFor(agent.Id))
                continue;
            if (HasWaitingIdentityMoment(physical) && !world.NeedsUrgentFood(physical) && !world.NeedsUrgentWarmth(physical) &&
                !world.IsConversationBusy(agent.Id))
                continue;
            if (world.FarmWorkFor(agent.Id) is not null && operativeOrder is null &&
                !world.NeedsUrgentFood(physical) && !world.NeedsUrgentWarmth(physical) &&
                !staleRunDecisions.Contains(agent.Id) &&
                !awaitingDispatch.Contains(agent.Id) &&
                !world.ShouldDispatchConversationChoice(agent.Id) && world.GuardianPlacementCandidate(agent.Id) is null)
                continue;
            if (operativeOrder is not null && physical.Project is { Stage: not ("completed" or "cancelled") } orderedProject &&
                !world.IsProductionOrderProject(operativeOrder, orderedProject) &&
                !world.IsConstructionOrderProject(operativeOrder, orderedProject))
            {
                world.SetProject(agent.Id, orderedProject with { Stage = "paused", Blocker = "Following an owner order." });
                physical = world.Agents[agent.Id];
            }
            if (physical.Project is { Stage: not ("completed" or "cancelled") } project &&
                operativeOrder is null &&
                (world.NeedsUrgentFood(physical) || world.NeedsUrgentWarmth(physical) && !world.IsProtectiveProject(project)))
            {
                world.SetProject(agent.Id, project with
                {
                    Stage = "paused",
                    Blocker = world.NeedsUrgentWarmth(physical) ? "Seeking warmth" : "Meeting food needs",
                });
                physical = world.Agents[agent.Id];
            }
            if (world.IsConversationBusy(agent.Id) && !world.ShouldDispatchConversationChoice(agent.Id))
                continue;
            var conversationChoiceContext = world.ConversationChoiceContextFor(agent.Id);
            var candidates = world.CreateCandidates(agent.Id, physical)
                .Select(candidate => candidate with { DestinationName = world.DestinationNameForModel(candidate.DestinationId) })
                .ToList();
            if ((operativeOrder is null || !world.IsProductionOrderProject(operativeOrder, physical.Project)) &&
                world.PauseExpiredUnfillableHouseholdRecipeProject(agent.Id))
            {
                physical = world.Agents[agent.Id];
                candidates = world.CreateCandidates(agent.Id, physical)
                    .Select(candidate => candidate with { DestinationName = world.DestinationNameForModel(candidate.DestinationId) })
                    .ToList();
            }
            var current = runtimes[agent.Id].CurrentIntention;
            if (!namingRetries.Contains(agent.Id) && !staleOrderDecisions.Contains(agent.Id) &&
                !staleRunDecisions.Contains(agent.Id) &&
                !awaitingDispatch.Contains(agent.Id) &&
                !world.NeedsCognition(agent.Id, current, candidates, conversationChoiceContext))
            {
                continue;
            }

            var generation = checked((int)(world.WorldTick + 1));
            var checkpoint = world.Society;
            var compaction = (checkpoint.MemoryCompactions ?? [])
                .SingleOrDefault(item => item.OwnerId == agent.Id);
            var retrievedMemories = WorldMemoryRetrieval.Retrieve(
                checkpoint.Memories,
                checkpoint.Beliefs ?? [],
                compaction,
                agent.Id,
                world.WorldTick,
                candidates, checkpoint);
            var observerGuidance = world.ObserverGuidanceFor(agent.Id);
            var requiresPersonalProvider = checkpoint.Births.Any(birth => birth.ChildId == agent.Id);
            var knownMapFacts = world.KnownMapFactsForCognition(agent.Id);
            var self = new CognitionSelfContext(agent.Id, agent.Name, agent.AgeBand.ToString(),
                physical.Personality, physical.Aspiration, CognitionHouseholdId(agent.HouseholdId),
                physical.Survival?.WarmthBasisPoints, physical.Survival?.IllnessBasisPoints,
                physical.RecentThoughts is { Count: > 0 } thoughts ? thoughts[^1].Text : null,
                checkpoint.Households.SingleOrDefault(item => item.Id == agent.HouseholdId)?.Name,
                world.Towns.SingleOrDefault(item => item.ResidentIds.Contains(agent.Id, StringComparer.Ordinal))?.Name,
                world.GuardianPlacementModelNote(agent.Id), world.EquipmentNote(agent.Id), world.ContinuityNote(agent.Id),
                DepartureNote: world.DepartureNote(agent.Id), CivicNote: world.CivicNote(agent.Id),
                MedicalCareNote: world.MedicalCareNoteCore(agent.Id), TownMembershipNote: world.TownMembershipNote(agent.Id),
                ToolMakingRequestNote: world.ToolMakingRequestNoteCore(agent.Id),
                AllowedChildSurnames: agent.NeedsName && AgentNameRules.RequiresParentSurname(checkpoint, agent.Id)
                    ? AgentNameRules.AllowedChildSurnames(checkpoint, agent.Id) : null,
                MarriageNote: world.Marriages.Where(item => AgentMarriageRules.HasParticipant(item, agent.Id))
                    .OrderBy(item => item.EndReceipt is not null).ThenByDescending(item => item.AcceptedTick)
                    .ThenBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault() is { } marriage
                    ? AgentMarriageRules.Note(marriage, agent.Id, checkpoint) : null,
                FamilyBackground: world.InitialChildFamilyBackground(agent.Id, physical),
                KnownRecipes: world.Knowledge.Recipes.Where(item => item.OwnerId == agent.Id)
                    .OrderByDescending(item => item.LearnedTick).ThenBy(item => item.RecipeId, StringComparer.Ordinal).Take(16)
                    .Select(item => world.Content.Recipes.Single(recipe => recipe.CanonicalId == item.RecipeId).DisplayName).ToArray(),
                Skills: (physical.Skills ?? []).OrderBy(skill => skill.Kind)
                    .Select(skill => skill.Kind.ToString().ToLowerInvariant()).ToArray());
            var observation = new AgentObservation(
                agent.Id,
                world.WorldTick,
                world.Society.RunEpoch,
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
            if (world.JevEnabled && world.IsAdult(agent.Id) && world.Host.ProviderFactory is not null)
            {
                try
                {
                    var provider = world.Host.ProviderFactory(agent.Id);
                    if (provider.KindFor(observation) is DecisionProviderKind.Jev or DecisionProviderKind.OpenAiDecisions)
                    {
                        var memoryCandidates = WorldMemoryRetrieval.Unassessed(
                            checkpoint.Memories,
                            checkpoint.Beliefs ?? [],
                            compaction,
                            agent.Id,
                            world.WorldTick);
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
            var accepted = world.EnqueueCognition(new SocietyCognitionScheduleEntry(
                IdentityRules.PrefixedReference("tick:", $"{world.WorldTick}:{agent.Id}"),
                agent.Id,
                world.PriorityFor(physical),
                world.WorldTick,
                ["routine_tick"],
                observation));
            if (!accepted)
            {
                world.AppendEvent(new CognitionBackpressureEventPayload(agent.Id, agent.Id));
            }
            else
            {
                foreach (var message in observerGuidance)
                {
                    var instruction = world.Orders.Instructions.Values.SingleOrDefault(item =>
                        item.InstructionId == message.InstructionId);
                    if (instruction is not null && instruction.GuidancePromptedTick is null)
                    {
                        world.Orders.RecordGuidancePrompt(instruction.IdempotencyKey, OrderStore.PrepareEnqueueDueCognitionOwnerQueuedInstruction(instruction, world.WorldTick));
                    }
                }
                if (observerGuidance.Count > 0)
                    world.MarkCheckpointCurrent();
                world.PhysicalAgents.RecordDecisionContext(agent.Id, world.Agents[agent.Id], world.DecisionContext(physical, candidates, conversationChoiceContext));
            }
        }
    }

}
