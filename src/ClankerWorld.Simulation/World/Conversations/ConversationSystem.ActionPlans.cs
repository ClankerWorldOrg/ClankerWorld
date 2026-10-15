using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class ConversationSystem
{
    public ConversationActionPlan PlanConversationAction(IConversationWorld world, string actor, ConversationChoice choice)
    {
        var current = choice.Kind == ConversationChoiceKind.Talk ? null : conversations.FirstOrDefault(item => item.Id == choice.TargetId);
        var partnership = choice.Kind == ConversationChoiceKind.AcceptWrapUp && current?.WrapUpEffect == AgentConversationEffect.Marriage
            ? AgentMarriageRules.Partnership(world.Society, current.InitiatorId, current.InviteeId) : null;
        return new(actor, choice, current?.Revision, current?.InitiatorId, current?.InviteeId, partnership);
    }

    public ActionCheck CheckConversationPlan(IConversationWorld world, ConversationActionPlan plan) =>
        PrepareConversationAction(world, plan, out _, out _);

    private ActionCheck PrepareConversationAction(IConversationWorld world, ConversationActionPlan plan,
        out AgentConversation? next, out bool agreed)
    {
        next = null;
        agreed = false;
        var actor = world.Society.Agents.FirstOrDefault(person => person.Id == plan.Actor && person.Status == SocietyAgentStatus.Active);
        var physical = world.Agents.GetValueOrDefault(plan.Actor);
        var capability = ActorCapabilities.Check(actor is null || physical is null ? null : new(actor.AgeBand,
                world.IsAboardBoat(plan.Actor), false, world.NeedsUrgentFood(physical), world.NeedsUrgentWarmth(physical), false),
            plan.CandidateId, new(ActionLifeStages.NonInfants, AllowBoat: plan.Choice.Kind != ConversationChoiceKind.Talk,
                AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if (plan.Choice.Kind == ConversationChoiceKind.Talk)
            return CanStartConversation(world, plan.Actor, plan.Choice.TargetId) ? new() :
                ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that person is no longer available to talk");
        var current = conversations.FirstOrDefault(item => item.Id == plan.Choice.TargetId);
        if (current is null || current.Revision != plan.Revision || current.InitiatorId != plan.InitiatorId || current.InviteeId != plan.InviteeId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected conversation has changed");
        if (plan.Choice.Kind == ConversationChoiceKind.AcceptWrapUp && current.WrapUpEffect == AgentConversationEffect.Marriage &&
            (plan.Partnership is null || !RelationshipPlan.Matches(AgentMarriageRules.Partnership(world.Society, current.InitiatorId, current.InviteeId), plan.Partnership)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the partnership for the selected marriage agreement has changed");
        var allowed = plan.Choice.Kind switch
        {
            ConversationChoiceKind.Accept => CanContinueConversation(world, current) && HasConversationAllowance(world, plan.Actor) &&
                world.HasExplicitConversationProvider(current.InitiatorId) && world.HasExplicitConversationProvider(current.InviteeId) &&
                AgentConversationRules.TryAcceptProposal(current with { RunEpoch = world.Society.RunEpoch }, plan.Actor,
                    world.WorldTick, world.Society.RunEpoch, out next),
            ConversationChoiceKind.Decline => AgentConversationRules.TryDeclineProposal(current, plan.Actor, world.WorldTick, out next),
            ConversationChoiceKind.Resume => CanContinueConversation(world, current) && AgentConversationRules.TryResume(current,
                plan.Actor, world.WorldTick, world.Society.RunEpoch, out next, out agreed),
            ConversationChoiceKind.End => AgentConversationRules.TryEndSuspended(current, plan.Actor, world.WorldTick, out next),
            ConversationChoiceKind.AcceptWrapUp => CanContinueConversation(world, current) &&
                (current.WrapUpEffect != AgentConversationEffect.Marriage || CanProposeMarriage(world, current)) &&
                AgentConversationRules.TryAcceptWrapUp(current, plan.Actor, world.WorldTick, out next, out agreed),
            ConversationChoiceKind.DeclineWrapUp => CanContinueConversation(world, current) &&
                AgentConversationRules.TryDeclineWrapUp(current, plan.Actor, world.WorldTick, out next),
            _ => false,
        };
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable,
            "the selected conversation choice is no longer available");
    }

    private bool CanStartConversation(IConversationWorld world, string initiatorId, string inviteeId) =>
        initiatorId != inviteeId && !world.IsAboardBoat(initiatorId) && !world.IsAboardBoat(inviteeId) &&
        world.Agents.TryGetValue(initiatorId, out var initiator) && world.Agents.TryGetValue(inviteeId, out var invitee) &&
        world.Society.Agents.Any(person => person.Id == initiatorId && person.Status == SocietyAgentStatus.Active) &&
        world.Society.Agents.Any(person => person.Id == inviteeId && person.Status == SocietyAgentStatus.Active && !LifeStageRules.IsInfant(person.AgeBand)) &&
        !world.NeedsUrgentFood(initiator) && !world.NeedsUrgentWarmth(initiator) &&
        !world.NeedsUrgentFood(invitee) && !world.NeedsUrgentWarmth(invitee) &&
        world.IsWithinInteractionRange(initiator.Position, invitee.Position, ResourceInteractionRange) &&
        ConversationFor(initiatorId) is null && ConversationFor(inviteeId) is null &&
        HasConversationAllowance(world, initiatorId) && HasConversationAllowance(world, inviteeId) &&
        world.HasExplicitConversationProvider(initiatorId) && world.HasExplicitConversationProvider(inviteeId) &&
        (conversations.Count < AgentConversationRules.MaximumSavedConversations ||
            conversations.Any(item => item.Status == AgentConversationStatus.Closed && !world.ActiveTalkOrderUses(item.Id)));

    private List<CognitionCandidate> CheckedConversationCandidates(IConversationWorld world, string actor, List<CognitionCandidate> candidates)
    {
        var result = new List<CognitionCandidate>();
        foreach (var candidate in candidates)
        {
            if (ConversationChoice.Decode(candidate.Id) is not { } choice) continue;
            var plan = PlanConversationAction(world, actor, choice);
            if (CheckConversationPlan(world, plan).Allowed) result.Add(candidate with { ActionPlan = plan });
        }
        return result;
    }
}
