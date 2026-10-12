using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Kernel;

/// <summary>
/// The authoritative movement-relevant state of one actor at a tick boundary.
/// </summary>
public sealed record MovementActor(string Id, GridPoint Position, int MoveWaitTicks);

/// <summary>
/// A request to move exactly one legal cardinal or diagonal tile in the current movement phase.
/// </summary>
public sealed record MovementIntent(string ActorId, GridPoint Destination);

public sealed record MovementEvent(
    string ActorId,
    string Kind,
    GridPoint Origin,
    GridPoint Destination,
    string Reason);

public sealed record MovementResolution(
    IReadOnlyList<MovementActor> Actors,
    IReadOnlyList<MovementEvent> Events)
{
    public MovementActor GetActor(string id) =>
        Actors.Single(actor => string.Equals(actor.Id, id, StringComparison.Ordinal));
}

/// <summary>
/// Resolves one atomic movement batch. Claims are ordered by descending wait
/// ticks then immutable actor ID. A two-actor reciprocal swap is the only move
/// through an occupied tile permitted in this first-world fixture.
/// </summary>
public static class DeterministicMovementResolver
{
    public static MovementResolution Resolve(
        WorldMap map,
        IEnumerable<MovementActor> actors,
        IEnumerable<MovementIntent> intents)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(intents);

        var orderedActors = actors.OrderBy(actor => actor.Id, StringComparer.Ordinal).ToArray();
        ValidateActors(map, orderedActors);
        var actorsById = orderedActors.ToDictionary(actor => actor.Id, StringComparer.Ordinal);
        var occupants = orderedActors.ToDictionary(actor => actor.Position, actor => actor.Id);
        var requested = intents.OrderBy(intent => intent.ActorId, StringComparer.Ordinal).ToArray();
        if (requested.Select(intent => intent.ActorId).Distinct(StringComparer.Ordinal).Count() != requested.Length)
        {
            throw new InvalidDataException("An actor may submit no more than one movement intent per tick.");
        }

        var candidates = new List<MovementCandidate>();
        var reasons = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var intent in requested)
        {
            if (!actorsById.TryGetValue(intent.ActorId, out var actor))
            {
                throw new InvalidDataException("A movement intent names an unknown actor.");
            }

            if (!map.CanFootStep(actor.Position, intent.Destination) ||
                map.IsDiagonalFootStep(actor.Position, intent.Destination) &&
                (occupants.ContainsKey(new GridPoint(intent.Destination.X, actor.Position.Y)) ||
                 occupants.ContainsKey(new GridPoint(actor.Position.X, intent.Destination.Y))))
            {
                reasons.Add(actor.Id, "invalid_destination");
                continue;
            }

            candidates.Add(new MovementCandidate(actor, intent));
        }

        var winners = SelectDestinationWinners(candidates, reasons);
        var accepted = SelectLegalMoves(winners, occupants, reasons);
        return Commit(orderedActors, candidates, accepted, reasons);
    }

    private static Dictionary<string, MovementCandidate> SelectDestinationWinners(
        List<MovementCandidate> candidates,
        Dictionary<string, string> reasons)
    {
        var orderedClaims = candidates
            .OrderBy(candidate => candidate.Intent.Destination.Y)
            .ThenBy(candidate => candidate.Intent.Destination.X)
            .ThenByDescending(candidate => candidate.Actor.MoveWaitTicks)
            .ThenBy(candidate => candidate.Actor.Id, StringComparer.Ordinal)
            .ToArray();
        var winners = new Dictionary<string, MovementCandidate>(StringComparer.Ordinal);

        for (var index = 0; index < orderedClaims.Length;)
        {
            var destination = orderedClaims[index].Intent.Destination;
            var winner = orderedClaims[index];
            winners.Add(winner.Actor.Id, winner);
            index++;
            while (index < orderedClaims.Length && orderedClaims[index].Intent.Destination == destination)
            {
                reasons.Add(orderedClaims[index].Actor.Id, "destination_reserved");
                index++;
            }
        }

        return winners;
    }

    private static HashSet<string> SelectLegalMoves(
        Dictionary<string, MovementCandidate> winners,
        Dictionary<GridPoint, string> occupants,
        Dictionary<string, string> reasons)
    {
        var accepted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var winner in winners.Values.OrderBy(candidate => candidate.Actor.Id, StringComparer.Ordinal))
        {
            if (accepted.Contains(winner.Actor.Id))
            {
                continue;
            }

            if (!occupants.TryGetValue(winner.Intent.Destination, out var occupantId))
            {
                accepted.Add(winner.Actor.Id);
                continue;
            }

            if (!winners.TryGetValue(occupantId, out var occupantMove) ||
                occupantMove.Intent.Destination != winner.Actor.Position ||
                winner.Intent.Destination != occupantMove.Actor.Position)
            {
                reasons.TryAdd(winner.Actor.Id, "occupied_destination");
                continue;
            }

            accepted.Add(winner.Actor.Id);
            accepted.Add(occupantId);
        }

        foreach (var winner in winners.Values)
        {
            if (!accepted.Contains(winner.Actor.Id))
            {
                reasons.TryAdd(winner.Actor.Id, "occupied_destination");
            }
        }

        return accepted;
    }

    private static MovementResolution Commit(
        MovementActor[] orderedActors,
        List<MovementCandidate> candidates,
        HashSet<string> accepted,
        Dictionary<string, string> reasons)
    {
        var candidatesById = candidates.ToDictionary(candidate => candidate.Actor.Id, StringComparer.Ordinal);
        var nextActors = new List<MovementActor>(orderedActors.Length);
        var events = new List<MovementEvent>();
        foreach (var actor in orderedActors)
        {
            if (!candidatesById.TryGetValue(actor.Id, out var candidate))
            {
                nextActors.Add(actor);
                continue;
            }

            if (accepted.Contains(actor.Id))
            {
                nextActors.Add(actor with { Position = candidate.Intent.Destination, MoveWaitTicks = 0 });
                events.Add(new MovementEvent(actor.Id, "moved", actor.Position, candidate.Intent.Destination, "accepted"));
                continue;
            }

            var reason = reasons[actor.Id];
            nextActors.Add(actor with { MoveWaitTicks = checked(actor.MoveWaitTicks + 1) });
            events.Add(new MovementEvent(actor.Id, "movement_blocked", actor.Position, candidate.Intent.Destination, reason));
        }

        return new MovementResolution(nextActors, events);
    }

    private static void ValidateActors(WorldMap map, MovementActor[] actors)
    {
        if (actors.Any(actor => string.IsNullOrWhiteSpace(actor.Id) || actor.MoveWaitTicks < 0) ||
            actors.Select(actor => actor.Id).Distinct(StringComparer.Ordinal).Count() != actors.Length ||
            actors.Select(actor => actor.Position).Distinct().Count() != actors.Length ||
            actors.Any(actor => !map.IsPassable(actor.Position)))
        {
            throw new InvalidDataException("Movement actors must have valid unique IDs, nonnegative wait ticks, and unique passable positions.");
        }
    }

    private sealed record MovementCandidate(MovementActor Actor, MovementIntent Intent);
}

/// <summary>
/// A complete cache key for derived routes. The cache is deliberately separate
/// from canonical state and never persists a route as authoritative truth.
/// </summary>
public sealed record RouteCacheKey(
    GridPoint Origin,
    GridPoint Destination,
    string MovementProfileId,
    int ActorCapabilityEpoch,
    int TransportEpoch,
    int TopologyEpoch,
    string RouteConfigurationVersion,
    string SimulationVersion);

public sealed class NonAuthoritativeRouteCache
{
    private readonly Dictionary<RouteCacheKey, GridPoint[]> routes = [];

    public int ComputationCount { get; private set; }

    public IReadOnlyList<GridPoint> GetOrCompute(WorldMap map, RouteCacheKey key)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(key);
        ValidateKey(key);
        if (routes.TryGetValue(key, out var cached))
        {
            return cached.ToArray();
        }

        var route = DeterministicRouteFinder.Find(map, key.Origin, key.Destination).ToArray();
        routes.Add(key, route);
        ComputationCount++;
        return route.ToArray();
    }

    private static void ValidateKey(RouteCacheKey key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.MovementProfileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.RouteConfigurationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.SimulationVersion);
        ArgumentOutOfRangeException.ThrowIfNegative(key.ActorCapabilityEpoch);
        ArgumentOutOfRangeException.ThrowIfNegative(key.TransportEpoch);
        ArgumentOutOfRangeException.ThrowIfNegative(key.TopologyEpoch);
    }
}

/// <summary>
/// Canonical state and event digests for movement fixtures.
/// </summary>
public static class MovementDigest
{
    public static string State(IEnumerable<MovementActor> actors)
    {
        ArgumentNullException.ThrowIfNull(actors);
        var builder = new StringBuilder("clankerworld.movement-state/v1\n");
        foreach (var actor in actors.OrderBy(actor => actor.Id, StringComparer.Ordinal))
        {
            builder.Append(actor.Id).Append('|')
                .Append(actor.Position.X).Append(',').Append(actor.Position.Y).Append('|')
                .Append(actor.MoveWaitTicks).Append('\n');
        }

        return Digest(builder.ToString());
    }

    public static string Events(IEnumerable<MovementEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var builder = new StringBuilder("clankerworld.movement-events/v1\n");
        foreach (var worldEvent in events.OrderBy(worldEvent => worldEvent.ActorId, StringComparer.Ordinal))
        {
            builder.Append(worldEvent.ActorId).Append('|')
                .Append(worldEvent.Kind).Append('|')
                .Append(worldEvent.Origin.X).Append(',').Append(worldEvent.Origin.Y).Append('|')
                .Append(worldEvent.Destination.X).Append(',').Append(worldEvent.Destination.Y).Append('|')
                .Append(worldEvent.Reason).Append('\n');
        }

        return Digest(builder.ToString());
    }

    private static string Digest(string canonical) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
}
