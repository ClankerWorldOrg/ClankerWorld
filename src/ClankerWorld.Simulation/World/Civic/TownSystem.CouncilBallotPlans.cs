using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    private static void OfferCouncilBallot(ITownWorld world, List<CognitionCandidate> candidates, string actor,
        TownRuntimeState town, string? candidate, string text, int priority)
    {
        if (town.Council?.Election is not { } election) return;
        var id = CivicAction(world, town.Id, candidate is null ? "ballot" : "single", CivicRoundToken(election), candidate ?? "");
        if (CouncilBallotChoice.Decode(id) is { } choice && PlanCouncilBallot(world, actor, choice) is { } plan &&
            CheckCouncilBallot(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }

    internal static CouncilBallotActionPlan? PlanCouncilBallot(ITownWorld world, string actor, CouncilBallotChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId)?.Council?.Election is not { } election) return null;
        return new(actor, choice, election.Id, election.Stage, election.OpenedTick, election.DeadlineTick, election.Seats,
            election.Candidates.ToArray(), election.Voters.ToArray(),
            choice.CandidateToken is { } candidate ? [ResolveCivicAgentToken(world, candidate)] : null);
    }

    internal static CouncilBallotActionPlan BindCouncilBallot(ITownWorld world, CouncilBallotActionPlan plan, IReadOnlyList<string>? choices) =>
        plan.Choice.CandidateToken is not null || plan.Selected is not null ? plan : plan with
        { Selected = choices?.Select(token => ResolveCivicAgentToken(world, token)).ToArray() };

    internal static ActionCheck CheckCouncilBallot(ITownWorld world, CouncilBallotActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? CouncilElectionCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: { Election: { } election } } town ||
            election.Id != plan.ElectionId || election.Stage != plan.Stage || election.Stage is not ("main" or "runoff") ||
            election.OpenedTick != plan.OpenedTick || election.DeadlineTick != plan.Deadline || world.WorldTick >= plan.Deadline ||
            CivicRoundToken(election) != plan.Choice.RoundToken || election.Seats != plan.Seats ||
            !plan.Voters.Contains(plan.Actor, StringComparer.Ordinal) || !election.Voters.Contains(plan.Actor, StringComparer.Ordinal) ||
            !CivicHistory(town).Knows(plan.Actor, election.Stage == "main" ? "election" : "runoff", election.Id))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected council election round is no longer open to them");
        return plan.Selected is not { } selected || selected.Count <= plan.Seats &&
            selected.Distinct(StringComparer.Ordinal).Count() == selected.Count &&
            selected.All(id => plan.Candidates.Contains(id, StringComparer.Ordinal) && election.Candidates.Contains(id, StringComparer.Ordinal))
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected ballot contains candidates no longer eligible in that round");
    }
}
