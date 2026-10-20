using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static CivicNoticeActionPlan? PlanCivicNotice(ITownWorld world, string actor, CivicNoticeChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: { } council } town) return null;
        var reading = choice.RecipientToken is null;
        var known = CivicHistory(town).Known(actor);
        return new(actor, choice, reading ? actor : ResolveCivicAgentToken(world, choice.RecipientToken!),
            reading ? CivicBoard(world, town) : null, council.Notices.Where(notice => reading || known.Contains(notice.Id)).ToArray());
    }

    internal static ActionCheck CheckCivicNoticePlan(ITownWorld world, CivicNoticeActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? CivicMembershipCapabilities);
        if (!capability.Allowed) return capability;
        if (world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId) is not { Council: { } council } town ||
            !world.Agents.TryGetValue(plan.Recipient, out var recipient) ||
            !plan.ReadsCurrentBoard && plan.Notices.Any(notice => !council.Notices.Contains(notice)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected notices or recipient are no longer available");
        var history = CivicHistory(town);
        var reading = plan.Choice.RecipientToken is null;
        if (reading && (CivicBoard(world, town) != plan.NoticePlace || plan.NoticePlace is not { } board ||
                !world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, board, WorldRuntime.CivicSharedRules.ResourceInteractionRange)) ||
            !reading && (plan.Recipient == plan.Actor ||
                !world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, recipient.Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange) ||
                plan.Notices.Any(notice => !history.Known(plan.Actor).Contains(notice.Id))))
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected notices cannot be read or relayed here now");
        return (plan.ReadsCurrentBoard ? council.Notices : plan.Notices).Any(notice => !history.Known(plan.Recipient).Contains(notice.Id))
            ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the recipient already knows those selected notices");
    }

    private static void OfferCivicNotice(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        string? recipient, string text, int priority)
    {
        var id = CivicAction(world, town.Id, recipient is null ? "read" : "relay", recipient ?? "");
        if (CivicNoticeChoice.Decode(id) is { } choice && PlanCivicNotice(world, actor, choice) is { } plan &&
            CheckCivicNoticePlan(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }
}
