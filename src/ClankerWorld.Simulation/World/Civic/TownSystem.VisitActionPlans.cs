using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static CivicVisitActionPlan? PlanCivicVisit(ITownWorld world, string actor, CivicVisitChoice choice) =>
        world.Agents.ContainsKey(actor) && world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is { } town &&
        CivicBoard(world, town) is { } board
            ? new(actor, choice, board, world.ReachCivicNoticePlace(actor, board)) : null;

    internal ActionCheck CheckCivicVisit(ITownWorld world, CivicVisitActionPlan plan)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        var town = world.Towns.SingleOrDefault(item => item.Id == plan.Choice.TownId);
        return town?.Council is not null && CivicBoard(world, town) == plan.NoticePlace &&
            !world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, plan.NoticePlace, WorldRuntime.CivicSharedRules.ResourceInteractionRange) &&
            (TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) || MayVisitAsNewcomer(world, plan.Actor, town) ||
                MayVisitLandHearing(world, plan.Actor, town) || MayVisitLandTransfer(world, plan.Actor, town) || MayVisitNonLandCase(world, plan.Actor, town))
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected notice place or reason for visiting has changed");
    }

    internal void AddTownCivicCandidates(ITownWorld world, List<CognitionCandidate> candidates, string actor,
        IReadOnlyList<string>? buildingsWithoutSites = null)
    {
        var first = candidates.Count;
        AddTownCivicCandidateOptions(world, candidates, actor, buildingsWithoutSites);
        for (var index = candidates.Count - 1; index >= first; index--)
        {
            var candidate = candidates[index];
            if (CouncilBallotChoice.Decode(candidate.Id) is { } ballot)
            {
                if ((candidate.ActionPlan as CouncilBallotActionPlan ?? PlanCouncilBallot(world, actor, ballot)) is { } voting &&
                    CheckCouncilBallot(world, voting).Allowed)
                    candidates[index] = candidate with { ActionPlan = voting };
                else candidates.RemoveAt(index);
                continue;
            }
            if (CouncilConsentChoice.Decode(candidate.Id) is { } consent)
            {
                if ((candidate.ActionPlan as CouncilConsentActionPlan ?? PlanCouncilConsent(world, actor, consent)) is { } agreement &&
                    CheckCouncilConsent(world, agreement).Allowed)
                    candidates[index] = candidate with { ActionPlan = agreement };
                else candidates.RemoveAt(index);
                continue;
            }
            if (CivicVisitChoice.Decode(candidate.Id) is not { } choice) continue;
            if (PlanCivicVisit(world, actor, choice) is { } plan && CheckCivicVisit(world, plan).Allowed)
                candidates[index] = candidate with { ActionPlan = plan };
            else candidates.RemoveAt(index);
        }
    }
}
