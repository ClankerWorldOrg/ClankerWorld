using System.Globalization;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    private static void OfferCouncilConsent(ITownWorld world, List<CognitionCandidate> candidates, string actor,
        TownRuntimeState town, CouncilConsentKind kind, string subject, string text, int priority)
    {
        var id = CivicAction(world, town.Id, CouncilConsentChoice.Verb(kind), subject);
        if (CouncilConsentChoice.Decode(id) is { } choice && PlanCouncilConsent(world, actor, choice) is { } plan &&
            CheckCouncilConsent(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }

    internal static CouncilConsentActionPlan? PlanCouncilConsent(ITownWorld world, string actor, CouncilConsentChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId)?.Council is not { } council) return null;
        var nominee = choice.Kind == CouncilConsentKind.Nominate ? ResolveCivicAgentToken(world, choice.SubjectToken) : null;
        return new(actor, choice, council.Candidates.SingleOrDefault(candidate => candidate.AgentId == actor),
            council.TermEndTick, nominee);
    }

    internal static ActionCheck CheckCouncilConsent(ITownWorld world, CouncilConsentActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if (world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId) is not { Council: { } council } town ||
            !TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Town council is no longer available to them");
        var agreement = council.Candidates.SingleOrDefault(candidate => candidate.AgentId == plan.Actor);
        var allowed = plan.Choice.Kind switch
        {
            CouncilConsentKind.Register => agreement?.FullTerm != true,
            CouncilConsentKind.Remainder => council.Form == "representative" && council.TermEndTick == plan.RemainderDeadline &&
                plan.RemainderDeadline is { } deadline && plan.Choice.SubjectToken == deadline.ToString(CultureInfo.InvariantCulture) &&
                agreement?.RemainderTermEndTick != deadline,
            CouncilConsentKind.WithdrawCandidate => agreement is not null && agreement == plan.Agreement,
            _ => plan.Nominee is { } nominee && nominee != plan.Actor && world.Agents.TryGetValue(nominee, out var person) &&
                TownAdults(world, town).Contains(nominee, StringComparer.Ordinal) &&
                world.IsWithinInteractionRange(person.Position, world.Agents[plan.Actor].Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange) &&
                !council.Candidates.Any(candidate => candidate.AgentId == nominee && candidate.FullTerm) &&
                !council.Notices.Any(notice => notice.Kind == "nomination" && notice.SubjectId == TownRecordIds.Nomination(plan.Actor, nominee)),
        };
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected candidacy, term or nominee has changed");
    }
}
