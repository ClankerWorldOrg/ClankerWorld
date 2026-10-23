using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void AddPlannedExplorationCandidate(List<CognitionCandidate> candidates, string actor, string candidate, string text, int rank)
    {
        if (ExplorationChoice.Decode(candidate) is not { } choice) return;
        var plan = global::ClankerWorld.Simulation.World.WorldRuntime.ExplorationActions.Definition.Offer(this, actor, choice);
        if (plan is not null) candidates.Add(new(candidate, text, rank) { ActionPlan = plan });
    }

    private (ExplorationState Prepared, bool Interrupted, bool Starting) ProjectExploration(string actor, AgentState person, ExplorationGoal? goal, bool replaceGoal)
    {
        var exploration = person.Exploration ?? new ExplorationState([], [], WorldTick, false);
        var interrupted = !exploration.Returning && exploration.OutingPath.Count > 0 && exploration.OutingPath[^1] != person.Position;
        if (interrupted)
            exploration = exploration with { OutingPath = [], OutingDiscoveries = [], Returning = false, Goal = null };
        if (replaceGoal || goal is not null || exploration.Goal?.OrderInstructionId is { } id && PendingInstructionFor(actor)?.InstructionId != id)
            exploration = exploration with { Goal = goal };
        var starting = exploration.OutingPath.Count == 0;
        if (starting)
            exploration = exploration with
            {
                VisitedTiles = exploration.VisitedTiles.Contains(person.Position) ? exploration.VisitedTiles : exploration.VisitedTiles.Append(person.Position).TakeLast(global::ClankerWorld.Simulation.World.CommunitySystem.ExplorationMemoryLimit).ToArray(),
                OutingPath = [person.Position],
                LastOutingTick = WorldTick,
                Returning = false,
                Goal = goal,
            };
        return (exploration, interrupted, starting);
    }

    private GridPoint? ExplorationNextStep(string actor, AgentState person, ExplorationState exploration)
    {
        var occupied = agents.Values.Where(item => item.AgentId != actor).Select(item => item.Position).ToHashSet();
        var pullingCart = TransportSystem.AttachedHandcart(HandcartWorld, actor) is not null;
        return FootMovementNeighbors(person.Position)
            .Where(point => map.IsPassable(point) && !occupied.Contains(point) && !exploration.OutingPath.Contains(point))
            .Where(point => !pullingCart || TransportSystem.LegalHandcartStep(HandcartWorld, person.Position, point))
            .Where(point => !map.IsDiagonalFootStep(person.Position, point) ||
                !occupied.Contains(new GridPoint(point.X, person.Position.Y)) && !occupied.Contains(new GridPoint(person.Position.X, point.Y)))
            .OrderBy(point => exploration.VisitedTiles.Contains(point) ? 1 : 0)
            .ThenByDescending(point => map.FootDistance(point, exploration.OutingPath[0]))
            .ThenBy(point => point.Y).ThenBy(point => point.X).Select(point => (GridPoint?)point).FirstOrDefault();
    }

    private ExplorationActionPlan? PlanExploration(string actor, ExplorationChoice choice, bool native = false, bool replaceGoal = false)
    {
        if (!agents.TryGetValue(actor, out var person) || !ValidExplorationGoal(choice.Goal)) return null;
        if (choice.Return && person.Exploration is not { OutingPath.Count: > 0 }) return null;
        var projection = choice.Return ? (Prepared: person.Exploration! with { Returning = true }, Interrupted: false, Starting: false)
            : ProjectExploration(actor, person, choice.Goal, replaceGoal);
        var projected = projection.Prepared;
        ExplorationStep step;
        GridPoint? target = null;
        if (!projected.Returning && projected.Goal is { } goal && ExplorationGoalReached(actor, person, goal))
            step = ExplorationStep.CompleteGoal;
        else if (!projected.Returning && ExplorationNextStep(actor, person, projected) is { } next)
        {
            step = ExplorationStep.Outward;
            target = next;
        }
        else
        {
            target = projected.OutingPath.Count == 1 ? projected.OutingPath[0] : projected.OutingPath[^2];
            step = projected.OutingPath.Count == 1 && person.Position == target ? ExplorationStep.CompleteReturn : ExplorationStep.Return;
            projected = projected with { Returning = true };
        }
        var route = target is { } point ? Reach(actor, point, ReachPurpose.Exploration, 0, 0, SwimmingPolicy.WhenCapable) : Array.AsReadOnly<GridPoint>([]);
        if (step == ExplorationStep.Return && route.Count == 0) step = ExplorationStep.WaitReturn;
        return new(actor, choice, person.Position, person.Exploration, projected, step, target, native, replaceGoal, projection.Interrupted, projection.Starting, route);
    }

    private static bool SameExplorationScope(ExplorationState? current, ExplorationState? original) =>
        current is null || original is null ? current is null && original is null :
        current.LastOutingTick == original.LastOutingTick && current.Returning == original.Returning && current.Goal == original.Goal &&
        current.VisitedTiles.SequenceEqual(original.VisitedTiles) && current.OutingPath.SequenceEqual(original.OutingPath) &&
        (current.OutingDiscoveries is null || original.OutingDiscoveries is null ? current.OutingDiscoveries is null && original.OutingDiscoveries is null
            : current.OutingDiscoveries.SequenceEqual(original.OutingDiscoveries));

    private ActionCheck CheckExploration(ExplorationActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ExplorationCapabilities(plan.Native));
        if (!capability.Allowed) return capability;
        var person = agents[plan.Actor];
        if (person.Position != plan.Origin || !SameExplorationScope(person.Exploration, plan.Original) ||
            !plan.Prepared.Returning && plan.Goal?.OrderInstructionId is { } id && PendingInstructionFor(plan.Actor)?.InstructionId != id)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected scouting origin, trail or purpose has changed");
        if (!plan.Native && !plan.Choice.Return)
        {
            if (!(person.Exploration?.Returning ?? false) && !HasWarmthForOuting(person) ||
                person.Exploration is not { OutingPath.Count: > 0 } && (person.FullnessBasisPoints < OutingFullnessReserve ||
                    person.Exploration is { } previous && WorldTick - previous.LastOutingTick < ExplorationCooldownTicks))
                return ActionCheck.Refuse(ActionRefusalReason.UrgentNeed, "they cannot safely start or continue this scouting trip");
        }
        if (plan.Step == ExplorationStep.CompleteGoal)
            return plan.Goal is { } goal && ExplorationGoalReached(plan.Actor, person, goal)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected scouting purpose is no longer fulfilled");
        if (plan.Step == ExplorationStep.Outward)
        {
            if (plan.Goal is { } goal && ExplorationGoalReached(plan.Actor, person, goal))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected scouting purpose is already fulfilled");
            if (plan.Target is not { } target || !FootMovementNeighbors(plan.Origin).Contains(target) || !map.IsPassable(target) ||
                agents.Values.Any(other => other.AgentId != plan.Actor && (other.Position == target || map.IsDiagonalFootStep(plan.Origin, target) &&
                    (other.Position == new GridPoint(target.X, plan.Origin.Y) || other.Position == new GridPoint(plan.Origin.X, target.Y)))) ||
                TransportSystem.AttachedHandcart(HandcartWorld, plan.Actor) is not null && !TransportSystem.LegalHandcartStep(HandcartWorld, plan.Origin, target))
                return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected visible scouting step is blocked");
        }
        return plan.Native || plan.Step is ExplorationStep.CompleteReturn or ExplorationStep.WaitReturn || plan.Target is { } destination &&
            (person.Position == destination || Reach(plan.Actor, destination, ReachPurpose.Exploration, 0, 0, SwimmingPolicy.WhenCapable).Count > 0)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected scouting step is out of reach");
    }

    private void ApplyCheckedExploration(ExplorationActionPlan plan)
    {
        if (plan.Choice.Return) ChooseExplorationReturn(plan.Actor, agents[plan.Actor], plan);
        else Explore(plan.Actor, agents[plan.Actor], plan.Choice.Goal, plan.ReplaceGoal, plan);
    }
}
