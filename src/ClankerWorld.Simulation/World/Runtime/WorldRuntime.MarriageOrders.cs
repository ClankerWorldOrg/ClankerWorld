using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string MarriageOrderTask = "attempt marriage with your current partner; both people keep their consent and surname choices";
    private const string MarriageAdultBlocker = OrderSystem.MarriageAdultBlocker;
    private const string MarriagePartnerBlocker = OrderSystem.MarriagePartnerBlocker;
    private const string MarriageChangedPartnerBlocker = OrderSystem.MarriageChangedPartnerBlocker;
    private const string MarriageEligibilityBlocker = OrderSystem.MarriageEligibilityBlocker;

    private OrderTask? ParseMarriageOrder(string text, string actor) =>
        OrderSystem.ParseMarriageOrder(MarriageOrdersWorld, text, actor);

    private string? CurrentMarriageOrderPartner(string actor) =>
        OrderSystem.CurrentMarriageOrderPartner(MarriageOrdersWorld, actor);

    private string? MarriageOrderBlocker(OwnerQueuedInstruction instruction) =>
        OrderSystem.MarriageOrderBlocker(MarriageOrdersWorld, instruction);

    private string? UnacceptedMarriageOrderBlocker(OwnerQueuedInstruction instruction) =>
        OrderSystem.UnacceptedMarriageOrderBlocker(MarriageOrdersWorld, instruction);

    private void RefreshMarriageOrderEligibilityStatus(OwnerQueuedInstruction instruction) =>
        OrderSystem.RefreshMarriageOrderEligibilityStatus(MarriageOrdersWorld, instruction);

    private AgentConversation? ConversationForOrder(OwnerQueuedInstruction instruction) =>
        OrderSystem.ConversationForOrder(MarriageOrdersWorld, instruction);

    private string? RequestedConversationActivity(AgentConversation conversation, string speaker) =>
        OrderSystem.RequestedConversationActivity(MarriageOrdersWorld, conversation, speaker);

    private static string? MarriageOrderOutcome(AgentConversation conversation, IEnumerable<AgentMarriage> marriages)
    {
        if (conversation.Status != AgentConversationStatus.Closed) return null;
        if (conversation.Outcome == "agreed" && conversation.WrapUpEffect == AgentConversationEffect.Marriage)
            return marriages.FirstOrDefault(item => item.Consent.Id == conversation.Id)?.CompletedTick is not null ? "married" : null;
        if (conversation.Outcome == "disagreed" && conversation.WrapUpEffect == AgentConversationEffect.Marriage) return "proposal_declined";
        return conversation.Outcome is "agreed" or "disagreed" ? "not_proposed" : conversation.Outcome;
    }

    internal static bool IsValidMarriageOrder(OwnerQueuedInstruction instruction, HashSet<string> people)
    {
        var order = instruction.Task!;
        return (order.TargetAgentId is null ? order.TalkConversationId is null && order.CompletedUnits == 0 :
                people.Contains(order.TargetAgentId) && order.TargetAgentId != instruction.TargetAgentId) &&
            order.RequestedUnits == 1 && order.CompletedUnits is >= 0 and <= 1 && !order.RepeatUntilCancelled && !order.QuantityIsExplicit &&
            order.ProgressUnit == "marriage_attempts" && order.TargetFoodKind is null && order.TargetResourceId is null && order.TargetPosition is null &&
            order.Status != "not_understood" && (order.Status == "finished") == (order.CompletedUnits == 1) &&
            (order.TalkConversationId is null || order.TalkConversationId.Length <= DerivedId.MaximumLength && IsValidProductionBindingId(order.TalkConversationId)) &&
            (order.CompletedUnits == 0 ? order.TalkOutcome is null && order.LastEffectId is null :
                order.TalkConversationId is not null && order.TalkOutcome is ("married" or "proposal_declined" or "not_proposed" or
                    "refused" or "deadline" or "daily_limit" or "withdrawn" or "participant_unavailable") && order.LastEffectId == TalkOrderEffectId(instruction));
    }
}
