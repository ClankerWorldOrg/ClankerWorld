using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    // Unpaid ordinary work may yield. Keep bound work and live paid jobs intact;
    // completed/cancelled jobs already retain their own payment and output history.
    private bool OrdinaryProjectCanYieldToOrder(AgentProject project) =>
        OrderSystem.OrdinaryProjectCanYieldToOrder(OrderSelectionWorld, project);

    private CognitionCandidate? OrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.OrderCandidateFor(OrderSelectionWorld, instruction, person);

    private static string FoodArrivalReceipt(string resourceId, GridPoint position) =>
        DerivedId.Create($"arrival:{resourceId}:{position.X}:{position.Y}");

    private bool MatchesFoodArrivalReceipt(string? receipt, MapResource resource) =>
        OrderSystem.MatchesFoodArrivalReceipt(OrderSelectionWorld, receipt, resource);

    private MapResource? KnownFoodSourceForOrder(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.KnownFoodSourceForOrder(OrderSelectionWorld, instruction, person);

    private bool KnownTargetIsUnavailable(OwnerQueuedInstruction instruction) =>
        OrderSystem.KnownTargetIsUnavailable(OrderSelectionWorld, instruction);

    private bool CanExploreForOrder(AgentState person) =>
        OrderSystem.CanExploreForOrder(OrderSelectionWorld, person);

    private bool ShouldInterruptOrder(
        AgentState person,
        OwnerQueuedInstruction instruction,
        CognitionCandidate? orderCandidate,
        CognitionCandidate? urgentCandidate) =>
        OrderSystem.ShouldInterruptOrder(OrderSurvivalWorld, person, instruction, orderCandidate, urgentCandidate);

    private CognitionCandidate? SelectOrderSurvivalCandidate(
        IEnumerable<CognitionCandidate> candidates, AgentState person, OwnerQueuedInstruction? instruction) =>
        OrderSystem.SelectOrderSurvivalCandidate(OrderSurvivalWorld, candidates, person, instruction);

    private bool IsSurvivalCandidate(string actor, string candidateId) =>
        OrderSystem.IsSurvivalCandidate(OrderSurvivalWorld, actor, candidateId);

    private void CreditOrderEffect(OwnerQueuedInstruction instruction, Identity effectId, int units) =>
        OrderSystem.CreditOrderEffect(OrderLifecycleWorld, instruction, effectId, units);

    private void SetOrderStatus(
        OwnerQueuedInstruction instruction,
        OrderStatus status,
        string? reason,
        bool waitForDecision = false) =>
        OrderSystem.SetOrderStatus(OrderLifecycleWorld, instruction, status, reason, waitForDecision);

    private string OrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.OrderBlockedReason(OrderBlockersWorld, instruction, person);

    internal static string MovementOrderEffectId(GridPoint destination) =>
        FormattableString.Invariant($"arrival:tile:{destination.X}:{destination.Y}");

    private bool MovementOrderNeedsHouseInvitation(string actor, GridPoint destination) =>
        OrderSystem.MovementOrderNeedsHouseInvitation(OrderBlockersWorld, actor, destination);
}
