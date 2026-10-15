using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

/// <summary>
/// A per-person record of places actually visited. An outing only picks
/// its next visible neighbour; it does not give the person the player's map.
/// </summary>
public sealed record ExplorationState(
    IReadOnlyList<GridPoint> VisitedTiles,
    IReadOnlyList<GridPoint> OutingPath,
    long LastOutingTick,
    bool Returning,
    IReadOnlyList<GridPoint>? OutingDiscoveries = null,
    [property: JsonRequired] ExplorationGoal? Goal = null);

/// <summary>A bounded purpose, without a destination inferred from the hidden map.</summary>
public sealed record ExplorationGoal(string Kind, string Target, string? OrderInstructionId = null);

public sealed partial class WorldRuntime
{
    private const int StartingExplorationWarmthBudgetSteps = 8;
    private const int ExplorationCooldownTicks = 180;
    private const int ExplorationMemoryLimit = CommunitySystem.ExplorationMemoryLimit;

    private AgentState CompleteExplorationGoal(string actor, AgentState person)
    {
        if (person.Exploration?.Goal is not { } goal) return person;
        var completed = AgentStore.PrepareExploration(person, person.Exploration with
        {
            OutingPath = [],
            Returning = false,
            Goal = null,
            LastOutingTick = WorldTick,
        });
        agentStore.CompleteExploration(actor, completed);
        AppendEvent("exploration_goal_found", $"{actor}:{goal.Kind}:{goal.Target}");
        return completed;
    }

    private void AddExplorationCandidate(List<CognitionCandidate> candidates, string actor, AgentState person)
    {
        if (NeedsUrgentFood(person) || NeedsUrgentWarmth(person))
            return;

        var exploration = person.Exploration;
        if (exploration?.OutingPath.Count > 0)
        {
            if (exploration.Returning || HasWarmthForOuting(person))
                AddPlannedExplorationCandidate(candidates, actor, exploration.Goal is { OrderInstructionId: null } goal && !exploration.Returning
                    ? ExplorationGoalCandidateId(goal) : "explore", exploration.Returning
                    ? "Keep returning to where your scouting trip started."
                    : exploration.Goal is { } purpose ? $"Keep looking for {purpose.Target.Replace('_', ' ')}; return when you choose."
                    : "Continue scouting nearby terrain and resources; return when you choose.", 75);
            AddPlannedExplorationCandidate(candidates, actor, "explore_return", "Return to where your scouting trip started.", 76);
            return;
        }

        if (!HasWarmthForOuting(person) || person.FullnessBasisPoints < OutingFullnessReserve ||
            exploration is not null && WorldTick - exploration.LastOutingTick < ExplorationCooldownTicks ||
            !map.FootNeighbors(person.Position).Any(map.IsPassable))
            return;

        if (NeededExplorationResource(actor, person) is { } needed)
        {
            var goal = new ExplorationGoal("resource", needed);
            AddPlannedExplorationCandidate(candidates, actor, ExplorationGoalCandidateId(goal), $"Look for {needed.Replace('_', ' ')} needed for food or current work.", 74);
            if (needed is "wood" or "iron_ore" or "gold_ore" or "diamond")
            {
                var terrain = new ExplorationGoal("terrain", needed == "wood" ? "Forest" : "Mountain");
                AddPlannedExplorationCandidate(candidates, actor, ExplorationGoalCandidateId(terrain), $"Look for {terrain.Target.ToLowerInvariant()} terrain for current material needs.", 75);
            }
        }
        if (person.Project is not { Stage: not ("completed" or "cancelled") })
            AddPlannedExplorationCandidate(candidates, actor, "explore", "Scout nearby terrain and resources out of curiosity; choose when to return.", 75);
    }

    private bool HasWarmthForOuting(AgentState person)
    {
        if (person.Survival is not { } condition) return true;
        // Keep the starting trial budget, then account for a longer recorded return.
        // Shelter at the starting tile is not protection carried along on the outing.
        var steps = Math.Max(StartingExplorationWarmthBudgetSteps, person.Exploration?.OutingPath.Count ?? 0);
        return map.FootNeighbors(person.Position).Where(map.IsPassable).All(next =>
        {
            var loss = Math.Max(0, OutdoorExposure(next) - ClothingProtection(person.AgentId, next));
            var stepTicks = (RoadStepCost(person.Position, next) + 99) / 100 +
                IllnessRules.TravelDelayTicks(condition.IllnessBasisPoints);
            return loss == 0 || condition.WarmthBasisPoints - (long)loss * stepTicks * steps * 2 >= UrgentWarmth;
        });
    }

    private void Explore(string actor, AgentState person, ExplorationGoal? goal = null, bool replaceGoal = false,
        ExplorationActionPlan? originalPlan = null)
    {
        var plan = originalPlan ?? PlanExploration(actor, new(false, goal), native: true, replaceGoal);
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        var exploration = plan.Prepared;
        if (plan.Interrupted) AppendEvent("exploration_aborted", $"{actor}:interrupted_movement");
        if (plan.Starting)
        {
            var learnedStartingTile = RecordKnowledgeFact(actor, person.Position);
            exploration = exploration with
            {
                LastOutingTick = WorldTick,
                OutingDiscoveries = learnedStartingTile ? [person.Position] : [],
            };
            AppendEvent("exploration_started", $"{actor}:{person.Position.X},{person.Position.Y}");
        }

        if (exploration.Returning)
        {
            ReturnFromExploration(actor, person, exploration with { Returning = true }, plan);
            return;
        }

        if (plan.Step == ExplorationStep.CompleteGoal)
        {
            CompleteExplorationGoal(actor, AgentStore.PrepareExploration(person, exploration));
            return;
        }

        var next = plan.Target!.Value;

        // Movement still obeys the ordinary travel delay, weather, occupancy,
        // river-wading and mountain rules. Only a completed step joins memory.
        agentStore.RecordExploration(actor, person, exploration);
        MoveToward(actor, agents[actor], next, "explore");
        var moved = agents[actor];
        if (moved.Position == person.Position)
            return;
        var visited = exploration.VisitedTiles.Contains(moved.Position)
            ? exploration.VisitedTiles
            : exploration.VisitedTiles.Append(moved.Position).TakeLast(ExplorationMemoryLimit).ToArray();
        var learned = RecordKnowledgeFact(actor, moved.Position);
        agentStore.RecordExploration(actor, moved, exploration with
        {
            VisitedTiles = visited,
            OutingPath = exploration.OutingPath.Append(moved.Position).ToArray(),
            OutingDiscoveries = learned
                    ? (exploration.OutingDiscoveries ?? []).Append(moved.Position).TakeLast(ExplorationMemoryLimit).ToArray()
                    : exploration.OutingDiscoveries ?? [],
        });
        if (!exploration.VisitedTiles.Contains(moved.Position))
        {
            var terrain = map.Tiles.First(tile => tile.Position == moved.Position).Terrain;
            var resourcesHere = map.Resources.Where(site => site.Position == moved.Position)
                .Select(site => site.Kind).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
            AppendEvent("exploration_discovered",
                $"{actor}:{moved.Position.X},{moved.Position.Y}:{terrain}:{string.Join(',', resourcesHere)}");
        }
        if (exploration.Goal is { } reachedGoal && ExplorationGoalReached(actor, agents[actor], reachedGoal))
            CompleteExplorationGoal(actor, agents[actor]);
    }

    private void ChooseExplorationReturn(string actor, AgentState person, ExplorationActionPlan? originalPlan = null)
    {
        if (person.Exploration is not { OutingPath.Count: > 0 } exploration) return;
        var plan = originalPlan ?? PlanExploration(actor, new(true), native: true);
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        if (plan.Original is { Returning: false })
            AppendEvent("exploration_return_started", actor);
        ReturnFromExploration(actor, person, plan.Prepared, plan);
    }

    private void ReturnFromExploration(string actor, AgentState person, ExplorationState exploration, ExplorationActionPlan plan)
    {
        if (plan.Step == ExplorationStep.CompleteReturn)
        {
            agentStore.RecordExploration(actor, person, exploration with
            {
                OutingPath = [],
                Returning = false,
                LastOutingTick = WorldTick,
                Goal = null,
            });
            AppendEvent("exploration_completed", $"{actor}:visited={exploration.VisitedTiles.Count}");
            return;
        }

        var destination = plan.Target!.Value;
        agentStore.RecordExploration(actor, person, exploration);
        MoveToward(actor, agents[actor], destination, "explore_return");
        var moved = agents[actor];
        if (moved.Position != person.Position)
            RecordKnowledgeFact(actor, moved.Position);
        if (moved.Position == destination && exploration.OutingPath.Count > 1)
            agentStore.RecordExploration(actor, moved, exploration with
            {
                OutingPath = exploration.OutingPath.Take(exploration.OutingPath.Count - 1).ToArray(),
            });
        else if (moved.MoveWaitTicks >= 30)
        {
            agentStore.RecordExploration(actor, moved, exploration with
            {
                OutingPath = [],
                Returning = false,
                LastOutingTick = WorldTick,
                Goal = null,
            });
            AppendEvent("exploration_aborted", $"{actor}:return_blocked");
        }
    }

    private static void ValidateExploration(ExplorationState? exploration, WorldMap map,
        IEnumerable<BridgeState> bridges, long worldTick)
    {
        if (exploration is null) return;
        CommunitySystem.ValidateLocalExploration(exploration);
        // Map bounds, passability and world time are links outside this component.
        if (exploration.OutingPath.Count > map.Tiles.Count || exploration.LastOutingTick > worldTick ||
            exploration.VisitedTiles.Any(point => !map.IsPassable(point)) || exploration.OutingPath.Any(point => !map.IsPassable(point)) ||
            (exploration.OutingDiscoveries ?? []).Any(point => !map.IsPassable(point)))
            throw new InvalidDataException("The saved local exploration record is invalid.");

        WorldMap? beforeOuting = null;
        for (var index = 1; index < exploration.OutingPath.Count; index++)
        {
            var first = exploration.OutingPath[index - 1];
            var second = exploration.OutingPath[index];
            if (map.CanFootStep(first, second)) continue;
            // These are committed outward steps, or remaining return waypoints.
            // A later deck can forbid an earlier wade through its tile. Bridges
            // built on the outing's first tick may also have appeared after a
            // step; only earlier ticks prove a deck existed throughout the trip.
            beforeOuting ??= MapWithBridges(map, bridges.Where(bridge => bridge.BuiltTick < exploration.LastOutingTick));
            if (!beforeOuting.CanFootStep(first, second))
                throw new InvalidDataException("The saved local exploration record is invalid.");
        }
    }

    private static WorldMap MapWithBridges(WorldMap map, IEnumerable<BridgeState> bridges)
    {
        var decks = RiverBridgeRules.Decks(bridges);
        return RiverBridgeRules.SameDecks(map.BridgeDecks, decks) ? map : map with { BridgeDecks = decks };
    }
}
