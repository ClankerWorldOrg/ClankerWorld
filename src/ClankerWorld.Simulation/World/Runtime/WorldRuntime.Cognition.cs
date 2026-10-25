using PendingConversationTurn = ClankerWorld.Simulation.World.CognitionSystem.PendingConversationTurn;
using ConversationTurnOutcome = ClankerWorld.Simulation.World.CognitionSystem.ConversationTurnOutcome;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    // Keep authoritative membership keys in saves; only model context needs a bounded alias.
    private static string? CognitionHouseholdId(string? id) => CognitionSystem.CognitionHouseholdId(id);

    private void EnqueueDueCognition(IReadOnlySet<string> activeModelCallIds) =>
        CognitionSystem.EnqueueDueCognition(CognitionObservationsWorld, activeModelCallIds);

    private bool IsOrderDecisionObservationCurrent(AgentObservation observation) =>
        CognitionSystem.IsOrderDecisionObservationCurrent(CognitionContextWorld, observation);

    private bool IsUndeliveredFinishedOrderDecision(AgentObservation observation) =>
        CognitionSystem.IsUndeliveredFinishedOrderDecision(CognitionContextWorld, observation);

    private bool IsFinishedOrderDecisionAwaitingReply(AgentObservation observation) =>
        CognitionSystem.IsFinishedOrderDecisionAwaitingReply(CognitionContextWorld, observation);

    private HashSet<string> ApplyContinuingIntentions(
        IEnumerable<string> dispatchedAgentIds,
        IEnumerable<string> pendingModelAgentIds) =>
        CognitionSystem.ApplyContinuingIntentions(CognitionContinuationWorld, dispatchedAgentIds, pendingModelAgentIds);

    private void ApplySafeRoutinesWhileWaiting(
        IEnumerable<string> waitingIds,
        IEnumerable<string> orderActorsHandledThisTick) =>
        CognitionSystem.ApplySafeRoutinesWhileWaiting(CognitionWaitingRoutinesWorld, waitingIds, orderActorsHandledThisTick);

    private static bool IsWaitingSurvivalCandidate(string id) => id is
        "consume_food" or "collect_shared_food" or "take_food_from_pot" or "make_room_for_food" or "recover_household_delivery" or
        "harvest_food" or "seek_food" or "wear_clothing" or "tend_fire" or "seek_warmth" ||
        id.StartsWith(TownProjectReturnPrefix, StringComparison.Ordinal);

    private void ApplyDecision(SocietyCognitionDispatchResult decision) =>
        CognitionSystem.ApplyDecision(CognitionAdmissionWorld, decision);

    private void ApplyCandidate(string agentId, AgentState state, string candidateId, bool reportIdle) =>
        CognitionSystem.ApplyCandidate(CognitionAdmissionWorld, agentId, state, candidateId, reportIdle);

    private void ApplyRegisteredCandidate(string agentId, AgentState state, string candidateId, bool reportIdle, ActionPlan? plan = null)
    {
        if (withheldActions?.Contains(agentId) == true || IsRefusedAction(agentId, candidateId)) return;
        if (attemptedActions is not null) attemptedActions[agentId] = candidateId;
        if (candidateId != "safe_idle") guardianPlacementActions.RecordAction(agentId);
        if (TryRegisteredAction(ActionPhase.BeforeAge, new(agentId, state, candidateId, reportIdle, plan))) return;
        plan ??= PlanRegisteredOrdinaryAction(agentId, candidateId);
        if (plan is not null && CheckActionPlan(plan).Refusal is { } refusal)
        {
            RefuseChoice(plan, refusal);
            return;
        }
        if (agents[agentId].Equipment?.Repair is not null && candidateId != "repair_equipment")
        {
            CancelEquipmentRepair(agentId);
            state = agents[agentId];
        }
        if (PendingInstructionFor(agentId) is null && this.farmingSystem.ContinueFarmWork(this.farmingWorld, agentId)) return;
        if (TryRegisteredAction(ActionPhase.Ordinary, new(agentId, state, candidateId, reportIdle, plan))) return;
        if (reportIdle) AppendEvent(new AgentIdleEventPayload(agentId));
    }

    private void ApplyBuildDecision(
        string agentId,
        AgentState state,
        string candidateId) =>
        CognitionSystem.ApplyBuildDecision(CognitionConstructionWorld, agentId, state, candidateId);

    private List<CognitionCandidate> CreateCandidates(
        string agentId,
        AgentState state,
        bool restrictForOrder = true) =>
        CognitionSystem.CreateCandidates(CognitionCandidatesWorld, agentId, state, restrictForOrder);

    private List<CognitionCandidate> CreateRegisteredCandidates(string agentId, AgentState state, bool restrictForOrder)
    {
        var candidates = CognitionSystem.CreateUnfilteredCandidates(CognitionCandidatesWorld, agentId, state, restrictForOrder);
        if (actionRefusals.Count > 0)
            candidates.RemoveAll(candidate => IsRefusedAction(agentId, candidate.Id));
        if (candidates.Count == 0) candidates.Add(WaitingCandidate(agentId, "Wait safely."));
        return candidates;
    }

    private List<CognitionCandidate> RestrictCandidatesForOrder(
        List<CognitionCandidate> candidates,
        OwnerQueuedInstruction order,
        AgentState state) =>
        CognitionSystem.RestrictCandidatesForOrder(CognitionCandidatesWorld, candidates, order, state);

    private static string ObservationDigest(string agentId, string worldId, AgentState state, IReadOnlyList<CognitionCandidate> candidates, IReadOnlyList<CognitionMemoryExcerpt> memories, IReadOnlyList<CognitionMemoryCompactionCandidate> compactionCandidates, IReadOnlyList<CognitionKnowledgeFact> knownMapFacts, CognitionSelfContext self, IReadOnlyList<CognitionObserverGuidance> observerGuidance, IReadOnlyList<CognitionMemorySummaryOption>? summaryOptions = null) => CognitionSystem.ObservationDigest(agentId, worldId, state, candidates, memories, compactionCandidates, knownMapFacts, self, observerGuidance, summaryOptions);

    public const string ChildModelRole = "personal";
    public const string ChildModelChoiceParentsAgreed = "parents_agreed";
    public const string ChildModelChoiceInitiatingParent = "initiating_parent";
    public const string ChildModelChoiceNoParentModel = "no_parent_model";

    public const string DeterministicModelEndpointIdentity = "built-in-rules-v1";
    public const string OpenAiModelEndpointIdentity = "openai-chat-completions-v1";
    public const string OllamaCloudModelEndpointIdentity = "ollama-cloud-chat-completions-v1";
    public const string AnthropicModelEndpointIdentity = "anthropic-messages-v1";

    /// <summary>Records the selected personal model after the birth tick commits.</summary>
    public void ApplyChildModelSelection(string childId, ChildPersonalModelSelection selection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childId);
        ArgumentNullException.ThrowIfNull(selection);
        gate.Wait();
        try
        {
            if (!agents.TryGetValue(childId, out var child) ||
                !society.Checkpoint.Births.Any(item => item.ChildId == childId))
            {
                throw new InvalidOperationException("A personal model can only be bound to a child born in this world.");
            }
            if (child.ChildModelSelection is { } existing)
            {
                if (existing != selection)
                    throw new InvalidOperationException("A child's birth model choice is already recorded.");
                return;
            }

            ValidateChildModelSelection(selection);
            agentStore.SelectChildModel(childId, child, selection);
            checkpointSchemaVersion = StateSchemaVersion;
            AppendEvent(new ChildModelSelectedEventPayload(childId, selection.ChoiceReason));
        }
        finally
        {
            gate.Release();
        }
    }

    private static void ValidateChildModelSelection(ChildPersonalModelSelection selection) =>
        CognitionSystem.ValidateChildModelSelection(selection);

    private const int MaximumConversationProviderCallsPerTick = 2;

    public IReadOnlyList<AgentConversation> Conversations => conversations
        .OrderByDescending(item => item.LastUpdatedTick)
        .ThenBy(item => item.Id, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<AgentConversationDailyBudget> ConversationBudgets => conversationBudgets
        .OrderBy(item => item.AgentId, StringComparer.Ordinal)
        .ToArray();

    private void SuspendRestoredConversations() =>
        conversationSystem.SuspendRestoredConversations(conversationWorld);

    private bool HasExplicitConversationProvider(string agentId) =>
        CognitionSystem.HasExplicitConversationProvider(CognitionConversationTasksWorld, agentId);

    private AgentConversation? ConversationFor(string agentId) =>
        conversationSystem.ConversationFor(agentId);

    private bool IsConversationBusy(string agentId) =>
        conversationSystem.IsConversationBusy(agentId);

    private bool ShouldDispatchConversationChoice(string agentId) =>
        conversationSystem.ShouldDispatchConversationChoice(agentId);

    private string? ConversationChoiceContextFor(string agentId) =>
        conversationSystem.ConversationChoiceContextFor(agentId);

    private bool HasConversationAllowance(string agentId) =>
        conversationSystem.HasConversationAllowance(conversationWorld, agentId);

    private List<CognitionCandidate> ConversationCandidates(string agentId) =>
        conversationSystem.ConversationCandidates(conversationWorld, agentId);

    private bool ApplyConversationCandidate(string agentId, string candidateId) =>
        conversationSystem.ApplyConversationCandidate(conversationWorld, agentId, candidateId);

    private bool ProposeConversation(string initiatorId, string inviteeId) =>
        conversationSystem.ProposeConversation(conversationWorld, initiatorId, inviteeId);

    private bool CanContinueConversation(AgentConversation conversation) =>
        conversationSystem.CanContinueConversation(conversationWorld, conversation);

    private void UpdateConversationsForTick(long worldTick) =>
        conversationSystem.UpdateConversationsForTick(conversationWorld, worldTick);

    private void CompleteConversationTurns(
        IReadOnlyList<PendingConversationTurn> completed,
        long worldTick,
        Func<PendingConversationTurn, bool> providerRouteIsCurrent) =>
        CognitionSystem.CompleteConversationTurns(CognitionConversationTasksWorld, completed, worldTick, providerRouteIsCurrent);

    private void StartConversationTurns() =>
        CognitionSystem.StartConversationTurns(CognitionConversationTasksWorld);

    private void SuspendConversation(string conversationId, AgentConversationInterruption interruption) =>
        conversationSystem.SuspendConversation(conversationWorld, conversationId, interruption);

    private void SuspendAllConversations(AgentConversationInterruption interruption) =>
        conversationSystem.SuspendAllConversations(conversationWorld, interruption);

    private void CancelPendingConversationTurn(
        string conversationId,
        AgentConversationInterruption interruption,
        bool suspendCurrent = true,
        bool underRuntimeGate = true) =>
        CognitionSystem.CancelPendingConversationTurn(CognitionConversationTasksWorld, conversationId, interruption, suspendCurrent, underRuntimeGate);

    private void ReconcilePendingConversationTurns() =>
        CognitionSystem.ReconcilePendingConversationTurns(CognitionConversationTasksWorld);

    private bool IsConversationTurnProviderCurrent(PendingConversationTurn pending) =>
        CognitionSystem.IsConversationTurnProviderCurrent(CognitionConversationTasksWorld, pending);

    private void CancelNoLongerAwaitingConversationTurns() =>
        CognitionSystem.CancelNoLongerAwaitingConversationTurns(CognitionConversationTasksWorld);

    private void RecordModelAttempt(string id, string status, string? acceptedCandidateId = null,
        string? setupBlocker = null) =>
        CognitionSystem.RecordModelAttempt(CognitionModelOutcomesWorld, id, status, acceptedCandidateId, setupBlocker);

    private void RecordModelCompletion(string id, CognitionAdmissionResult admission, string? failure) =>
        CognitionSystem.RecordModelCompletion(CognitionModelOutcomesWorld, id, admission, failure);

    private sealed class AgentWorldAdapter(WorldRuntime runtime) : IAgentWorld
    {
        public long WorldTick => runtime.WorldTick;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public GridPoint SharedStoragePosition => runtime.SharedStoragePosition;
        public GridPoint BuildingPosition(string id) => runtime.worldSimulation.Buildings.Single(building => building.InstanceId == id).Position;
        public int FootDistance(GridPoint origin, GridPoint destination) => runtime.map.FootDistance(origin, destination);
        public int WarmthChange(AgentState person) => runtime.WarmthChange(person);
        public GridPoint? LivingPosition(string id) => runtime.agents.TryGetValue(id, out var person) ? person.Position : null;
        public GridPoint? DeceasedPosition(string id) => runtime.deceasedAgents.TryGetValue(id, out var person) ? person.LastPhysical.Position : null;
    }
}
