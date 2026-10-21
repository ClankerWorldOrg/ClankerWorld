using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static bool IsConversationOrder(string action) => OrderSystem.IsConversationOrder(action);

    private const string TalkOrderTask = "attempt to talk with the named person; agreement and resumption remain each participant's choice";

    private OrderTask? ParseTalkOrder(string text, string actor) =>
        OrderSystem.ParseTalkOrder(TalkOrdersWorld, text, actor);

    private static bool IsLinkedTalkOrder(OwnerQueuedInstruction instruction) => instruction.Task is { TalkConversationId: not null } order && IsConversationOrder(order.Action);

    private bool TalkOrderHasConversation(OwnerQueuedInstruction instruction) =>
        OrderSystem.TalkOrderHasConversation(TalkOrdersWorld, instruction);

    private CognitionCandidate? TalkOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.TalkOrderCandidateFor(TalkOrdersWorld, instruction, person);

    private string? TalkOrderBlocker(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.TalkOrderBlocker(TalkOrdersWorld, instruction, person);

    private void ExecuteTalkOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteTalkOrderStep(TalkOrdersWorld, instruction, person);

    private void ApplyTalkOrderConversationDecision(OwnerQueuedInstruction instruction, SocietyCognitionDispatchResult decision) =>
        OrderSystem.ApplyTalkOrderConversationDecision(TalkOrdersWorld, instruction, decision);

    private bool ActiveTalkOrderUses(string conversationId) =>
        OrderSystem.ActiveTalkOrderUses(TalkOrdersWorld, conversationId);

    private void CompleteClosedTalkOrders() =>
        OrderSystem.CompleteClosedTalkOrders(TalkOrdersWorld);

    internal static string TalkOrderEffectId(OwnerQueuedInstruction instruction) =>
        (instruction.Task!.Action == "propose_marriage" ? "marriage-order:" : "talk-order:") +
        DerivedId.Hash(
            $"{instruction.InstructionId}|{instruction.TargetAgentId}|{instruction.Task!.TargetAgentId}|{instruction.Task.TalkConversationId}|{instruction.Task.TalkOutcome}");

    internal static bool IsTalkOutcome(string? outcome) => outcome is "agreed" or "refused" or "deadline" or "daily_limit" or
        "disagreed" or "withdrawn" or "participant_unavailable";

    private static void ValidateTalkOrderBindings(IEnumerable<OwnerQueuedInstruction> instructions, IEnumerable<AgentConversation> conversations, IEnumerable<AgentMarriage> marriageRecords)
    {
        var marriages = marriageRecords.ToArray();
        var records = conversations.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var linked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var instruction in instructions)
        {
            if (instruction.Task is not { TalkConversationId: { } id } order || !IsConversationOrder(order.Action)) continue;
            if (order.Action == "propose_marriage" && order.TalkOutcome == "married" &&
                !marriages.Any(marriage => marriage.Consent.Id == id && marriage.InitiatorId == instruction.TargetAgentId &&
                    marriage.InviteeId == order.TargetAgentId && marriage.CompletedTick is not null))
                throw new InvalidDataException("A finished marriage order lost its completed marriage record.");
            if (!linked.Add(id)) throw new InvalidDataException("Several talk orders share one invitation.");
            if (!records.TryGetValue(id, out var conversation))
            {
                if (IsActiveOrder(order.Status)) throw new InvalidDataException("An active talk order lost its conversation.");
                if (order.Action != "propose_marriage" || marriages.FirstOrDefault(item => item.Consent.Id == id) is not { } marriage)
                    continue; // Completed/cancelled tasks may outlive bounded closed conversation history.
                conversation = marriage.Consent; // Marriage keeps the actual consent after ordinary history is trimmed.
            }
            if (conversation.Kind != AgentConversationKind.Ordinary || conversation.InitiatorId != instruction.TargetAgentId ||
                conversation.InviteeId != order.TargetAgentId || conversation.CreatedTick < instruction.SubmittedTick ||
                order.CompletedUnits == 1 && (conversation.Status != AgentConversationStatus.Closed || (order.Action == "propose_marriage" ? MarriageOrderOutcome(conversation, marriages) : conversation.Outcome) != order.TalkOutcome))
                throw new InvalidDataException("A talk order does not match its actual invitation or outcome.");
        }
    }
}
