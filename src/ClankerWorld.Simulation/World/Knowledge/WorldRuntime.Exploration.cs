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
    private void RetireClosedOrderExplorationGoals(HashSet<string> retired)
    {
        foreach (var person in agents.Values.Where(person =>
                     retired.Contains(person.Exploration?.Goal?.OrderInstructionId ?? "")).ToArray())
            agents[person.AgentId] = person with { Exploration = person.Exploration! with { Goal = null } };
        foreach (var person in deceasedAgents.Values.Where(person =>
                     retired.Contains(person.LastPhysical.Exploration?.Goal?.OrderInstructionId ?? "")).ToArray())
            deceasedAgents[person.AgentId] = person with
            {
                LastPhysical = person.LastPhysical with
                { Exploration = person.LastPhysical.Exploration! with { Goal = null } },
            };
    }

    private const int StartingExplorationWarmthBudgetSteps = 8;
    private const int ExplorationCooldownTicks = 180;
    private const int ExplorationMemoryLimit = 256;

    private AgentState CompleteExplorationGoal(string actor, AgentState person)
    {
        if (person.Exploration?.Goal is not { } goal) return person;
        var completed = person with
        {
            Exploration = person.Exploration with
            {
                OutingPath = [],
                Returning = false,
                Goal = null,
                LastOutingTick = WorldTick,
            },
        };
        agents[actor] = completed;
        AppendEvent(new ExplorationGoalFoundEventPayload(actor, goal.Kind, goal.Target));
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
                candidates.Add(new(exploration.Goal is { OrderInstructionId: null } goal && !exploration.Returning
                    ? ExplorationGoalCandidateId(goal) : "explore", exploration.Returning
                    ? "Keep returning to where your scouting trip started."
                    : exploration.Goal is { } purpose ? $"Keep looking for {purpose.Target.Replace('_', ' ')}; return when you choose."
                    : "Continue scouting nearby terrain and resources; return when you choose.", 75));
            candidates.Add(new("explore_return", "Return to where your scouting trip started.", 76));
            return;
        }

        if (!HasWarmthForOuting(person) || person.FullnessBasisPoints < OutingFullnessReserve ||
            exploration is not null && WorldTick - exploration.LastOutingTick < ExplorationCooldownTicks ||
            !map.FootNeighbors(person.Position).Any(map.IsPassable))
            return;

        if (NeededExplorationResource(actor, person) is { } needed)
        {
            var goal = new ExplorationGoal("resource", needed);
            candidates.Add(new(ExplorationGoalCandidateId(goal), $"Look for {needed.Replace('_', ' ')} needed for food or current work.", 74));
            if (needed is "wood" or "iron_ore" or "gold_ore" or "diamond")
            {
                var terrain = new ExplorationGoal("terrain", needed == "wood" ? "Forest" : "Mountain");
                candidates.Add(new(ExplorationGoalCandidateId(terrain), $"Look for {terrain.Target.ToLowerInvariant()} terrain for current material needs.", 75));
            }
        }
        if (person.Project is not { Stage: not ("completed" or "cancelled") })
            candidates.Add(new("explore", "Scout nearby terrain and resources out of curiosity; choose when to return.", 75));
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

    private void Explore(string actor, AgentState person, ExplorationGoal? goal = null, bool replaceGoal = false)
    {
        var exploration = person.Exploration ?? new ExplorationState([], [], WorldTick, false);
        // A return path holds remaining waypoints, not each intermediate detour.
        // Only the outward path must end at the actor's current position.
        if (!exploration.Returning && exploration.OutingPath.Count > 0 && exploration.OutingPath[^1] != person.Position)
        {
            // Another legal intention moved the actor. Never splice that move
            // into a stale scouting path or pretend its intermediate tiles were visited.
            exploration = exploration with { OutingPath = [], OutingDiscoveries = [], Returning = false, Goal = null };
            AppendEvent(new ExplorationAbortedEventPayload(actor, "interrupted_movement"));
        }
        // An order can supply a purpose to an already running curiosity outing.
        // Replacement/cancellation never makes its old purpose authoritative.
        if (replaceGoal || goal is not null || exploration.Goal?.OrderInstructionId is { } orderId &&
            PendingInstructionFor(actor)?.InstructionId != orderId)
            exploration = exploration with { Goal = goal };
        if (exploration.OutingPath.Count == 0)
        {
            var learnedStartingTile = RecordKnowledgeFact(actor, person.Position);
            exploration = exploration with
            {
                VisitedTiles = exploration.VisitedTiles.Contains(person.Position)
                    ? exploration.VisitedTiles
                    : exploration.VisitedTiles.Append(person.Position).TakeLast(ExplorationMemoryLimit).ToArray(),
                OutingPath = [person.Position],
                LastOutingTick = WorldTick,
                Returning = false,
                OutingDiscoveries = learnedStartingTile ? [person.Position] : [],
                Goal = goal,
            };
            AppendEvent(new ExplorationStartedEventPayload(actor, person.Position.X, person.Position.Y));
        }

        if (exploration.Returning)
        {
            ReturnFromExploration(actor, person, exploration with { Returning = true });
            return;
        }

        if (exploration.Goal is { } currentGoal && ExplorationGoalReached(actor, person, currentGoal))
        {
            CompleteExplorationGoal(actor, person with { Exploration = exploration });
            return;
        }

        var occupied = agents.Values.Where(item => item.AgentId != actor)
            .Select(item => item.Position).ToHashSet();
        var pullingCart = AttachedHandcart(actor) is not null;
        var next = map.FootNeighbors(person.Position)
            .Where(point => map.IsPassable(point) && !occupied.Contains(point) &&
                !exploration.OutingPath.Contains(point))
            .Where(point => !pullingCart || LegalHandcartStep(person.Position, point))
            // Match movement's corner occupancy rules before ranking exits.
            .Where(point => !map.IsDiagonalFootStep(person.Position, point) ||
                !occupied.Contains(new GridPoint(point.X, person.Position.Y)) &&
                !occupied.Contains(new GridPoint(person.Position.X, point.Y)))
            .OrderBy(point => exploration.VisitedTiles.Contains(point) ? 1 : 0)
            .ThenByDescending(point => map.FootDistance(point, exploration.OutingPath[0]))
            .ThenBy(point => point.Y).ThenBy(point => point.X)
            .Select(point => (GridPoint?)point).FirstOrDefault();
        if (next is null)
        {
            ReturnFromExploration(actor, person, exploration with { Returning = true });
            return;
        }

        // Movement still obeys the ordinary travel delay, weather, occupancy,
        // river-wading and mountain rules. Only a completed step joins memory.
        agents[actor] = person with { Exploration = exploration };
        MoveToward(actor, agents[actor], next.Value, "explore");
        var moved = agents[actor];
        if (moved.Position == person.Position)
            return;
        var visited = exploration.VisitedTiles.Contains(moved.Position)
            ? exploration.VisitedTiles
            : exploration.VisitedTiles.Append(moved.Position).TakeLast(ExplorationMemoryLimit).ToArray();
        var learned = RecordKnowledgeFact(actor, moved.Position);
        agents[actor] = moved with
        {
            Exploration = exploration with
            {
                VisitedTiles = visited,
                OutingPath = exploration.OutingPath.Append(moved.Position).ToArray(),
                OutingDiscoveries = learned
                    ? (exploration.OutingDiscoveries ?? []).Append(moved.Position).TakeLast(ExplorationMemoryLimit).ToArray()
                    : exploration.OutingDiscoveries ?? [],
            }
        };
        if (!exploration.VisitedTiles.Contains(moved.Position))
        {
            var terrain = map.Tiles.First(tile => tile.Position == moved.Position).Terrain;
            var resourcesHere = map.Resources.Where(site => site.Position == moved.Position)
                .Select(site => site.Kind).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
            AppendEvent(new ExplorationDiscoveredEventPayload(actor, moved.Position.X, moved.Position.Y, Convert.ToString(terrain, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, string.Join(',', resourcesHere)));
        }
        if (exploration.Goal is { } reachedGoal && ExplorationGoalReached(actor, agents[actor], reachedGoal))
            CompleteExplorationGoal(actor, agents[actor]);
    }

    private void ChooseExplorationReturn(string actor, AgentState person)
    {
        if (person.Exploration is not { OutingPath.Count: > 0 } exploration) return;
        if (!exploration.Returning)
            AppendEvent(new ExplorationReturnStartedEventPayload(actor));
        ReturnFromExploration(actor, person, exploration with { Returning = true });
    }

    private void ReturnFromExploration(string actor, AgentState person, ExplorationState exploration)
    {
        if (exploration.OutingPath.Count == 1 && person.Position == exploration.OutingPath[0])
        {
            agents[actor] = person with
            {
                Exploration = exploration with
                {
                    OutingPath = [],
                    Returning = false,
                    LastOutingTick = WorldTick,
                    Goal = null,
                }
            };
            AppendEvent(new ExplorationCompletedEventPayload(actor, exploration.VisitedTiles.Count));
            return;
        }

        var destination = exploration.OutingPath.Count == 1 ? exploration.OutingPath[0] : exploration.OutingPath[^2];
        agents[actor] = person with { Exploration = exploration };
        MoveToward(actor, agents[actor], destination, "explore_return");
        var moved = agents[actor];
        if (moved.Position != person.Position)
            RecordKnowledgeFact(actor, moved.Position);
        if (moved.Position == destination && exploration.OutingPath.Count > 1)
            agents[actor] = moved with
            {
                Exploration = exploration with
                {
                    OutingPath = exploration.OutingPath.Take(exploration.OutingPath.Count - 1).ToArray(),
                }
            };
        else if (moved.MoveWaitTicks >= 30)
        {
            agents[actor] = moved with
            {
                Exploration = exploration with
                {
                    OutingPath = [],
                    Returning = false,
                    LastOutingTick = WorldTick,
                    Goal = null,
                }
            };
            AppendEvent(new ExplorationAbortedEventPayload(actor, "return_blocked"));
        }
    }

    private static void ValidateExploration(ExplorationState? exploration, WorldMap map,
        IEnumerable<BridgeState> bridges, long worldTick)
    {
        if (exploration is null) return;
        if (!ValidExplorationGoal(exploration.Goal) ||
            exploration.VisitedTiles is null || exploration.OutingPath is null ||
            exploration.Goal is not null && exploration.OutingPath.Count == 0 ||
            exploration.VisitedTiles.Count > ExplorationMemoryLimit ||
            // An outward route never revisits a tile, so the map itself bounds
            // its length without imposing a distance limit on the scout.
            exploration.OutingPath.Count > map.Tiles.Count ||
            exploration.OutingPath.Distinct().Count() != exploration.OutingPath.Count ||
            (exploration.OutingDiscoveries?.Count ?? 0) > ExplorationMemoryLimit ||
            exploration.LastOutingTick < 0 || exploration.LastOutingTick > worldTick ||
            exploration.VisitedTiles.Any(point => !map.IsPassable(point)) ||
            exploration.OutingPath.Any(point => !map.IsPassable(point)) ||
            (exploration.OutingDiscoveries ?? []).Any(point => !map.IsPassable(point)) ||
            (exploration.OutingDiscoveries ?? []).Distinct().Count() != (exploration.OutingDiscoveries?.Count ?? 0))
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
