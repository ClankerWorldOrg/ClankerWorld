using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static OrderTask? ParseTalkOrder(ITalkOrdersWorld world, string text, string actor)
    {
        if (LifeStageRules.IsInfant(world.Society.GetAgent(actor).AgeBand)) return null;
        if (text.StartsWith("please ", StringComparison.OrdinalIgnoreCase)) text = text[7..].TrimStart();
        foreach (var prefix in new[] { "talk to ", "talk with ", "speak to ", "speak with " })
        {
            if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var name = text[prefix.Length..].Trim();
            var people = world.Society.Agents.Where(person => person.Id != actor).ToArray();
            var targets = people.Where(person => person.Id == name).ToArray();
            if (targets.Length == 0 && AgentNameRules.CanonicalKey(name) is { } key)
                targets = people.Where(person => string.Equals(AgentNameRules.CanonicalKey(person.Name), key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(AgentNameRules.FirstNameKey(person.Name), key, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (targets.Length != 1) return null;
            return OrderStore.CreateOrderTask(OrderStore.CreateTalkToOrderDefinition(TargetAgentId: (targets[0].Id)!), "queued", 1, 0, "conversations", false);
        }
        return null;
    }


    internal static bool TalkOrderHasConversation(ITalkOrdersWorld world, OwnerQueuedInstruction instruction) => instruction.Task is { } order && IsConversationOrder(order.Action) &&
        (world.IsLinkedTalkOrder(instruction) || world.ConversationFor(instruction.TargetAgentId) is not null);

    internal static CognitionCandidate? TalkOrderCandidateFor(ITalkOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person) => world.IsLinkedTalkOrder(instruction) || world.TalkOrderBlocker(instruction, person) is null
            ? new(instruction.Task!.Action, world.IsLinkedTalkOrder(instruction) ? "Wait for the actual conversation's outcome, preserving each person's choices." :
                "Reach the named person and attempt one face-to-face invitation.", 0, instruction.Task!.TargetAgentId ?? world.CurrentMarriageOrderPartner(instruction.TargetAgentId))
            : null;

    internal static string? TalkOrderBlocker(ITalkOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (instruction.Task!.Action == "propose_marriage" && world.MarriageOrderBlocker(instruction) is { } marriageBlocker)
            return marriageBlocker;
        var target = instruction.Task.TargetAgentId ?? world.CurrentMarriageOrderPartner(actor)!;
        if (!world.Agents.TryGetValue(target, out var other) || world.Society.GetAgent(target).Status != SocietyAgentStatus.Active)
            return "The named person is no longer available.";
        if (LifeStageRules.IsInfant(world.Society.GetAgent(target).AgeBand))
            return "The named person is too young for this conversation.";
        if (world.PassengerBoat(actor) is not null || world.PassengerBoat(target) is not null)
            return "Waiting for both people to be ashore for a face-to-face conversation.";
        if (world.ConversationFor(actor) is not null || world.ConversationFor(target) is not null)
            return "Waiting for another conversation to finish.";
        if (world.NeedsUrgentFood(other) || world.NeedsUrgentWarmth(other)) return "The named person's urgent food or warmth needs come first.";
        if (!world.HasExplicitConversationProvider(actor) || !world.HasExplicitConversationProvider(target))
            return "Both people need their own available conversation model.";
        if (!world.HasConversationAllowance(actor) || !world.HasConversationAllowance(target))
            return "Waiting until both people are under their daily conversation limit.";
        if (world.Conversations.Count >= AgentConversationRules.MaximumSavedConversations &&
            !world.Conversations.Any(conversation => conversation.Status == AgentConversationStatus.Closed && !world.ActiveTalkOrderUses(conversation.Id)))
            return "Waiting for space in the conversation history.";
        return world.IsWithinInteractionRange(person.Position, other.Position, world.ResourceInteractionRange) ||
            world.Reach(actor, other.Position, ReachPurpose.Movement, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
                ? null : "Waiting for an open route to the named person.";
    }


    internal static void ExecuteTalkOrderStep(ITalkOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        if (world.IsLinkedTalkOrder(instruction))
        {
            world.CompleteClosedTalkOrders();
            if (world.UnacceptedMarriageOrderBlocker(instruction) is { } eligibilityBlocker)
                world.SetOrderStatus(instruction, OrderStatus.Blocked, eligibilityBlocker);
            else if (world.ConversationForOrder(instruction)?.Status == AgentConversationStatus.Suspended)
                world.SetOrderStatus(instruction, OrderStatus.Blocked, "Conversation stopped; both people must choose to resume, or you may end it.");
            else if (instruction.Task!.Action == "propose_marriage" && world.Marriages.Any(item => item.Consent.Id == instruction.Task.TalkConversationId && item.CompletedTick is null) &&
                world.ConversationForOrder(instruction)?.Outcome == "participant_unavailable")
                world.SetOrderStatus(instruction, OrderStatus.Blocked, "Marriage was accepted, but the shared surname remains unfinished because a participant is unavailable.");
            return;
        }
        if (world.TalkOrderBlocker(instruction, person) is { } blocker)
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, blocker);
            return;
        }
        var actor = instruction.TargetAgentId;
        if (instruction.Task!.TargetAgentId is null)
        {
            var bound = world.Orders.Instructions[instruction.IdempotencyKey];
            instruction = OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(bound, bound.Task!.UpdateDefinition(definition => definition switch
{
    TalkToOrderDefinition task => OrderStore.PrepareExecuteTalkOrderStepTalkToOrderDefinition(task, world.CurrentMarriageOrderPartner(actor)!),
    ProposeMarriageOrderDefinition task => OrderStore.PrepareExecuteTalkOrderStepProposeMarriageOrderDefinition(task, world.CurrentMarriageOrderPartner(actor)),
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
})
);
            world.Orders.RecordTalkOrderStep(instruction.IdempotencyKey, instruction);
        }
        var target = instruction.Task!.TargetAgentId!;
        if (!world.IsWithinInteractionRange(person.Position, world.Agents[target].Position, world.ResourceInteractionRange))
        {
            world.MoveToward(actor, person, world.Agents[target].Position, "owner_order_talk", world.ResourceInteractionRange);
            return;
        }
        if (!world.ProposeConversation(actor, target))
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, "Waiting for a face-to-face invitation to become possible.");
            return;
        }
        var conversation = world.ConversationFor(actor)!;
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        world.Orders.RecordTalkOrderStep(current.IdempotencyKey, OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(current, (current.Task!.UpdateDefinition(definition => definition switch
{
    TalkToOrderDefinition task => OrderStore.PrepareExecuteTalkOrderStepTalkToOrderDefinitionTalkConversationId(task, conversation.Id),
    ProposeMarriageOrderDefinition task => OrderStore.PrepareExecuteTalkOrderStepProposeMarriageOrderDefinitionTalkConversationId(task, conversation.Id),
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
})).Transition(OrderStatus.Doing)
));
    }


    internal static void ApplyTalkOrderConversationDecision(ITalkOrdersWorld world, OwnerQueuedInstruction instruction, SocietyCognitionDispatchResult decision)
    {
        var actor = instruction.TargetAgentId;
        var candidate = decision.Admission.Intention!.CandidateId;
        var person = world.Agents[actor];
        if ((world.NeedsUrgentFood(person) || world.NeedsUrgentWarmth(person)) && world.IsSurvivalCandidate(actor, candidate))
        {
            world.SetOrderStatus(instruction, OrderStatus.Interrupted, "Food or warmth needs come first.");
            if (!decision.Admission.FellBack && decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel &&
                world.IsMarketFoodCandidate(actor, candidate))
                world.ApplyMarketCandidate(actor, person, candidate);
            else
                world.ApplyCandidate(actor, person, candidate, reportIdle: true);
            return;
        }
        var conversation = world.IsLinkedTalkOrder(instruction) ? world.ConversationForOrder(instruction) : world.ConversationFor(actor);
        if (conversation is null) return;
        // An order requires the attempt, never affirmative wrap-up or resumption consent.
        if (!decision.Admission.FellBack && decision.Admission.Intention.Provider == DecisionProviderKind.LargeLanguageModel &&
            world.ConversationCandidates(actor).Any(item => item.Id == candidate))
            world.ApplyConversationCandidate(actor, candidate);
        world.CompleteClosedTalkOrders();
        world.RefreshMarriageOrderEligibilityStatus(instruction);
    }


    internal static bool ActiveTalkOrderUses(ITalkOrdersWorld world, string conversationId) => world.Orders.Instructions.Values.Any(instruction =>
        instruction.Task is { } order && IsConversationOrder(order.Action) && world.IsActiveOrder(order.Status) && order.TalkConversationId == conversationId);

    internal static void CompleteClosedTalkOrders(ITalkOrdersWorld world)
    {
        foreach (var instruction in world.Orders.Instructions.Values.Where(item => world.IsLinkedTalkOrder(item) && world.IsActiveOrder(item.Task!.Status)).ToArray())
        {
            // Speech and surname turns can continue without a new planning choice; keep eligibility status current.
            world.RefreshMarriageOrderEligibilityStatus(instruction);
            var conversation = world.Conversations.FirstOrDefault(item => item.Id == instruction.Task!.TalkConversationId);
            if (conversation?.Status != AgentConversationStatus.Closed) continue;
            var outcome = instruction.Task!.Action == "propose_marriage" ? world.MarriageOrderOutcome(conversation, world.Marriages) : conversation.Outcome;
            if (outcome is null) continue; // Accepted marriage still has its own unfinished surname session.
            var current = OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(instruction, instruction.Task!.UpdateDefinition(definition => definition switch
{
    TalkToOrderDefinition task => OrderStore.PrepareCompleteClosedTalkOrdersTalkToOrderDefinition(task, outcome),
    ProposeMarriageOrderDefinition task => OrderStore.PrepareCompleteClosedTalkOrdersProposeMarriageOrderDefinition(task, outcome),
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
})
);
            world.Orders.RecordCompleteClosedTalkOrders(current.IdempotencyKey, current);
            world.CreditOrderEffect(current, Identity.Parse(world.TalkOrderEffectId(current)), 1);
        }
    }

}
