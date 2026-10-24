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

}
