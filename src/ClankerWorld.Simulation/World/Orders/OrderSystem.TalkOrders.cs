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
    internal static OrderTask? ParseMarriageOrder(IMarriageOrdersWorld world, string text, string actor)
    {
        text = text.Trim();
        if (text.StartsWith("please ", StringComparison.OrdinalIgnoreCase)) text = text[7..].TrimStart();
        if (!world.IsAdult(actor) || !text.Equals("propose marriage", StringComparison.OrdinalIgnoreCase) &&
            !text.Equals("propose marriage to my partner", StringComparison.OrdinalIgnoreCase)) return null;
        return OrderStore.CreateOrderTask(OrderStore.CreateProposeMarriageOrderDefinition(TargetAgentId: world.CurrentMarriageOrderPartner(actor)), "queued", 1, 0, "marriage_attempts", false);
    }


    internal static string? CurrentMarriageOrderPartner(IMarriageOrdersWorld world, string actor)
    {
        var partnership = world.Society.Relationships.SingleOrDefault(item => item.Type == SocietyRelationshipType.Partnership &&
            item.State == SocietyRelationshipState.Accepted && (item.ProposerId == actor || item.TargetId == actor));
        return partnership is null ? null : partnership.ProposerId == actor ? partnership.TargetId : partnership.ProposerId;
    }


    internal static string? MarriageOrderBlocker(IMarriageOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        if (!world.IsAdult(actor)) return MarriageAdultBlocker;
        var target = instruction.Task!.TargetAgentId ?? world.CurrentMarriageOrderPartner(actor);
        if (target is null) return MarriagePartnerBlocker;
        if (AgentMarriageRules.Partnership(world.Society, actor, target) is null)
            return MarriageChangedPartnerBlocker;
        return AgentMarriageRules.CanPropose(world.Society, world.Marriages, actor, target) ? null :
            MarriageEligibilityBlocker;
    }


    internal static string? UnacceptedMarriageOrderBlocker(IMarriageOrdersWorld world, OwnerQueuedInstruction instruction) => instruction.Task is { Action: "propose_marriage" } order && !world.Marriages.Any(item => item.Consent.Id == order.TalkConversationId)
            ? world.MarriageOrderBlocker(instruction) : null;

    internal static void RefreshMarriageOrderEligibilityStatus(IMarriageOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        if (current.Task is not { Action: "propose_marriage", TalkConversationId: not null } order || !world.IsActiveOrder(order.Status)) return;
        if (world.UnacceptedMarriageOrderBlocker(current) is { } blocker)
        {
            world.SetOrderStatus(current, OrderStatus.Blocked, blocker);
            return;
        }
        // Clear only a recovered eligibility blocker, preserving independently owned interruptions and failures.
        if (order.Status != "blocked" || order.BlockedReason is not
            (MarriageAdultBlocker or MarriagePartnerBlocker or MarriageChangedPartnerBlocker or MarriageEligibilityBlocker)) return;
        if (world.ConversationForOrder(current)?.Status == AgentConversationStatus.Suspended)
            world.SetOrderStatus(current, OrderStatus.Blocked, "Conversation stopped; both people must choose to resume, or you may end it.");
        else if (world.ConversationForOrder(current)?.Outcome == "participant_unavailable")
            world.SetOrderStatus(current, OrderStatus.Blocked, "Marriage was accepted, but the shared surname remains unfinished because a participant is unavailable.");
        else
            world.SetOrderStatus(current, OrderStatus.Doing, null);
    }


    internal static AgentConversation? ConversationForOrder(IMarriageOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        var id = instruction.Task!.TalkConversationId;
        if (instruction.Task.Action == "propose_marriage" && world.Marriages.FirstOrDefault(item => item.Consent.Id == id) is { } marriage)
            id = marriage.SurnameConversationId;
        return world.Conversations.FirstOrDefault(item => item.Id == id);
    }


    internal static string? RequestedConversationActivity(IMarriageOrdersWorld world, AgentConversation conversation, string speaker) => conversation.Kind == AgentConversationKind.Ordinary && world.Orders.Instructions.Values.Any(instruction =>
            instruction.TargetAgentId == speaker && instruction.Task is { Action: "propose_marriage" } order &&
            world.IsActiveOrder(order.Status) && order.TalkConversationId == conversation.Id && world.MarriageOrderBlocker(instruction) is null)
                ? "propose_marriage" : null;
    internal const string MarriageAdultBlocker = "Only an adult can propose marriage.";
    internal const string MarriagePartnerBlocker = "Waiting for an eligible current partner before attempting marriage.";
    internal const string MarriageChangedPartnerBlocker = "The selected person is no longer your current partner; this order will not choose someone else.";
    internal const string MarriageEligibilityBlocker = "This partnership is not currently eligible for marriage; both adults need chosen full names and must be free to marry.";

    internal const string MarriageOrderTask = "attempt marriage with your current partner; both people keep their consent and surname choices";
    internal static OrderTask? ParseGuardianOrder(IGuardianOrdersWorld world, string text, string actor)
    {
        const string prefix = "become guardian for ";
        if (!world.IsAdult(actor) || !text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var target = text[prefix.Length..].Trim();
        if (target.Length == 0) return null;
        var active = world.Society.Agents.Where(person => person.Status == SocietyAgentStatus.Active);
        // Bind the complete, unique name (or exact ID) once. Renames cannot
        // redirect a queued order, and leftover words are never guessed away.
        var named = active.Where(person => person.Id == target).ToArray();
        if (named.Length == 0 && AgentNameRules.CanonicalKey(target) is { } targetName)
            named = active.Where(person => string.Equals(AgentNameRules.CanonicalKey(person.Name), targetName,
                StringComparison.OrdinalIgnoreCase)).ToArray();
        if (named.Length != 1 || !world.NeedsCaregiver(named[0].Id) || world.Agents[named[0].Id].GuardianSearch is null)
            return null;
        return OrderStore.CreateOrderTask(OrderStore.CreateAcceptGuardianshipOrderDefinition(TargetAgentId: (named[0].Id)!), "queued", 1, 0, "guardianships", false);
    }


    internal static CognitionCandidate? GuardianOrderCandidate(IGuardianOrdersWorld world, string adult, string child) => world.PlanDependentCare(adult, new(DependentCareChoiceKind.Accept, child)) is { } plan && world.CheckDependentCarePlan(plan).Allowed
            ? new CognitionCandidate(plan.CandidateId, "Accept primary care of the child named in this order.", 0) { ActionPlan = plan }
            : null;

    internal static void ExecuteGuardianOrder(IGuardianOrdersWorld world, OwnerQueuedInstruction instruction, CognitionCandidate candidate)
    {
        var adult = instruction.TargetAgentId;
        var child = instruction.Task!.TargetAgentId!;
        if (candidate.Id != "guardian_accept:" + child || !world.CanAcceptGuardian(adult, child))
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.GuardianOrderBlockedReason(adult, child));
            return;
        }
        world.ApplyDependentCareCandidate(adult, candidate.Id, candidate.ActionPlan);
        if (world.Society.GetAgent(child).PrimaryCaregiverId == adult && !world.NeedsCaregiver(child))
            world.CreditOrderEffect(instruction, Identity.Parse(world.GuardianOrderEffectId(adult, child)), 1);
        else
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.GuardianOrderBlockedReason(adult, child));
    }


    internal static string GuardianOrderBlockedReason(IGuardianOrdersWorld world, string adult, string child)
    {
        if (!world.IsAdult(adult)) return "Only an adult can become a guardian.";
        if (!world.NeedsCaregiver(child) || world.Agents[child].GuardianSearch is null)
            return "The named child is no longer looking for a guardian.";
        if (!world.ReadyForBriefInteraction(adult)) return "The adult needs to finish their current activity before accepting care.";
        return "Waiting for this adult's turn in the search for the child's guardian.";
    }

}
