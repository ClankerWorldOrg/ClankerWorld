using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using System.Collections.ObjectModel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
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
}
