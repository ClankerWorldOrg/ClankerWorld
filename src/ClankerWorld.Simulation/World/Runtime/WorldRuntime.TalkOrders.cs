using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static bool IsConversationOrder(string action) => action is "talk_to" or "propose_marriage";

    private const string TalkOrderTask = "attempt to talk with the named person; agreement and resumption remain each participant's choice";

    private OrderTask? ParseTalkOrder(string text, string actor)
    {
        if (society.Checkpoint.GetAgent(actor).AgeBand == SocietyAgeBand.Infant) return null;
        if (text.StartsWith("please ", StringComparison.OrdinalIgnoreCase)) text = text[7..].TrimStart();
        foreach (var prefix in new[] { "talk to ", "talk with ", "speak to ", "speak with " })
        {
            if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var name = text[prefix.Length..].Trim();
            var people = society.Checkpoint.Agents.Where(person => person.Id != actor).ToArray();
            var targets = people.Where(person => person.Id == name).ToArray();
            if (targets.Length == 0 && AgentNameRules.CanonicalKey(name) is { } key)
                targets = people.Where(person => string.Equals(AgentNameRules.CanonicalKey(person.Name), key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(AgentNameRules.FirstNameKey(person.Name), key, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (targets.Length != 1) return null;
            return new("talk_to", "queued", 1, 0, "conversations", false, TargetAgentId: targets[0].Id);
        }
        return null;
    }

    private static bool IsLinkedTalkOrder(OwnerQueuedInstruction instruction) => instruction.Task is { TalkConversationId: not null } order && IsConversationOrder(order.Action);

    private bool TalkOrderHasConversation(OwnerQueuedInstruction instruction) => instruction.Task is { } order && IsConversationOrder(order.Action) &&
        (IsLinkedTalkOrder(instruction) || ConversationFor(instruction.TargetAgentId) is not null);

    private CognitionCandidate? TalkOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        IsLinkedTalkOrder(instruction) || TalkOrderBlocker(instruction, person) is null
            ? new(instruction.Task!.Action, IsLinkedTalkOrder(instruction) ? "Wait for the actual conversation's outcome, preserving each person's choices." :
                "Reach the named person and attempt one face-to-face invitation.", 0, instruction.Task!.TargetAgentId ?? CurrentMarriageOrderPartner(instruction.TargetAgentId))
            : null;

    private string? TalkOrderBlocker(OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (instruction.Task!.Action == "propose_marriage" && MarriageOrderBlocker(instruction) is { } marriageBlocker)
            return marriageBlocker;
        var target = instruction.Task.TargetAgentId ?? CurrentMarriageOrderPartner(actor)!;
        if (!agents.TryGetValue(target, out var other) || society.Checkpoint.GetAgent(target).Status != SocietyAgentStatus.Active)
            return "The named person is no longer available.";
        if (society.Checkpoint.GetAgent(target).AgeBand == SocietyAgeBand.Infant)
            return "The named person is too young for this conversation.";
        if (PassengerBoat(actor) is not null || PassengerBoat(target) is not null)
            return "Waiting for both people to be ashore for a face-to-face conversation.";
        if (ConversationFor(actor) is not null || ConversationFor(target) is not null)
            return "Waiting for another conversation to finish.";
        if (NeedsUrgentFood(other) || NeedsUrgentWarmth(other)) return "The named person's urgent food or warmth needs come first.";
        if (!HasExplicitConversationProvider(actor) || !HasExplicitConversationProvider(target))
            return "Both people need their own available conversation model.";
        if (!HasConversationAllowance(actor) || !HasConversationAllowance(target))
            return "Waiting until both people are under their daily conversation limit.";
        if (conversations.Count >= AgentConversationRules.MaximumSavedConversations &&
            !conversations.Any(conversation => conversation.Status == AgentConversationStatus.Closed && !ActiveTalkOrderUses(conversation.Id)))
            return "Waiting for space in the conversation history.";
        return IsWithinInteractionRange(person.Position, other.Position, ResourceInteractionRange) ||
            FindUnoccupiedRoute(actor, person.Position, other.Position, ResourceInteractionRange).Count > 0
                ? null : "Waiting for an open route to the named person.";
    }

    private void ExecuteTalkOrderStep(OwnerQueuedInstruction instruction, AgentState person)
    {
        if (IsLinkedTalkOrder(instruction))
        {
            CompleteClosedTalkOrders();
            if (UnacceptedMarriageOrderBlocker(instruction) is { } eligibilityBlocker)
                SetOrderStatus(instruction, OrderStatus.Blocked, eligibilityBlocker);
            else if (ConversationForOrder(instruction)?.Status == AgentConversationStatus.Suspended)
                SetOrderStatus(instruction, OrderStatus.Blocked, "Conversation stopped; both people must choose to resume, or you may end it.");
            else if (instruction.Task!.Action == "propose_marriage" && marriages.Any(item => item.Consent.Id == instruction.Task.TalkConversationId && item.CompletedTick is null) &&
                ConversationForOrder(instruction)?.Outcome == "participant_unavailable")
                SetOrderStatus(instruction, OrderStatus.Blocked, "Marriage was accepted, but the shared surname remains unfinished because a participant is unavailable.");
            return;
        }
        if (TalkOrderBlocker(instruction, person) is { } blocker)
        {
            SetOrderStatus(instruction, OrderStatus.Blocked, blocker);
            return;
        }
        var actor = instruction.TargetAgentId;
        if (instruction.Task!.TargetAgentId is null)
        {
            var bound = instructionsByIdempotency[instruction.IdempotencyKey];
            instruction = bound with { Task = bound.Task! with { TargetAgentId = CurrentMarriageOrderPartner(actor) } };
            instructionsByIdempotency[instruction.IdempotencyKey] = instruction;
        }
        var target = instruction.Task!.TargetAgentId!;
        if (!IsWithinInteractionRange(person.Position, agents[target].Position, ResourceInteractionRange))
        {
            MoveToward(actor, person, agents[target].Position, "owner_order_talk", ResourceInteractionRange);
            return;
        }
        if (!ProposeConversation(actor, target))
        {
            SetOrderStatus(instruction, OrderStatus.Blocked, "Waiting for a face-to-face invitation to become possible.");
            return;
        }
        var conversation = ConversationFor(actor)!;
        var current = instructionsByIdempotency[instruction.IdempotencyKey];
        instructionsByIdempotency[current.IdempotencyKey] = current with
        { Task = (current.Task! with { TalkConversationId = conversation.Id }).Transition(OrderStatus.Doing) };
    }

    private void ApplyTalkOrderConversationDecision(OwnerQueuedInstruction instruction, SocietyCognitionDispatchResult decision)
    {
        var actor = instruction.TargetAgentId;
        var candidate = decision.Admission.Intention!.CandidateId;
        var person = agents[actor];
        if ((NeedsUrgentFood(person) || NeedsUrgentWarmth(person)) && IsSurvivalCandidate(actor, candidate))
        {
            SetOrderStatus(instruction, OrderStatus.Interrupted, "Food or warmth needs come first.");
            if (!decision.Admission.FellBack && decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel &&
                IsMarketFoodCandidate(actor, candidate))
                ApplyMarketCandidate(actor, person, candidate);
            else
                ApplyCandidate(actor, person, candidate, reportIdle: true);
            return;
        }
        var conversation = IsLinkedTalkOrder(instruction) ? ConversationForOrder(instruction) : ConversationFor(actor);
        if (conversation is null) return;
        // An order requires the attempt, never affirmative wrap-up or resumption consent.
        if (!decision.Admission.FellBack && decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel &&
            ConversationCandidates(actor).Any(item => item.Id == candidate))
            ApplyConversationCandidate(actor, candidate);
        CompleteClosedTalkOrders();
        RefreshMarriageOrderEligibilityStatus(instruction);
    }

    private bool ActiveTalkOrderUses(string conversationId) => instructionsByIdempotency.Values.Any(instruction =>
        instruction.Task is { } order && IsConversationOrder(order.Action) && IsActiveOrder(order.Status) && order.TalkConversationId == conversationId);

    private void CompleteClosedTalkOrders()
    {
        foreach (var instruction in instructionsByIdempotency.Values.Where(item => IsLinkedTalkOrder(item) && IsActiveOrder(item.Task!.Status)).ToArray())
        {
            // Speech and surname turns can continue without a new planning choice; keep eligibility status current.
            RefreshMarriageOrderEligibilityStatus(instruction);
            var conversation = conversations.FirstOrDefault(item => item.Id == instruction.Task!.TalkConversationId);
            if (conversation?.Status != AgentConversationStatus.Closed) continue;
            var outcome = instruction.Task!.Action == "propose_marriage" ? MarriageOrderOutcome(conversation, marriages) : conversation.Outcome;
            if (outcome is null) continue; // Accepted marriage still has its own unfinished surname session.
            var current = instruction with { Task = instruction.Task! with { TalkOutcome = outcome } };
            instructionsByIdempotency[current.IdempotencyKey] = current;
            CreditOrderEffect(current, Identity.Parse(TalkOrderEffectId(current)), 1);
        }
    }

    private static string TalkOrderEffectId(OwnerQueuedInstruction instruction) =>
        (instruction.Task!.Action == "propose_marriage" ? "marriage-order:" : "talk-order:") +
        DerivedId.Hash(
            $"{instruction.InstructionId}|{instruction.TargetAgentId}|{instruction.Task!.TargetAgentId}|{instruction.Task.TalkConversationId}|{instruction.Task.TalkOutcome}");

    private static bool IsTalkOutcome(string? outcome) => outcome is "agreed" or "refused" or "deadline" or "daily_limit" or
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
