using System.Net.Http.Json;
using System.Text.Json;

namespace ClankerWorld.GodotClient.Protocol;

public sealed record WorldProtocolVersion(int Major, int Minor);

public sealed record WorldHandshake(
    WorldProtocolVersion Protocol,
    IReadOnlyList<string> ServerCapabilities,
    IReadOnlyList<string> ClientCapabilities);

public sealed record WorldPosition(int X, int Y);

public sealed record WorldTile(int X, int Y, string Terrain);

public sealed record WorldMapObject(string Id, string Kind, WorldPosition Position);

public sealed record WorldResource(
    string Id,
    string Kind,
    WorldPosition Position,
    bool IsRenewable,
    string State,
    int? Quantity = null,
    int? Capacity = null,
    int? RegenerationAmount = null,
    int? RegenerationIntervalDays = null,
    string? RegenerationSeason = null);

public sealed record WorldActor(
    string Id,
    WorldPosition Position,
    int FullnessBasisPoints,
    int FoodItems,
    int WoodItems);

public sealed record WorldEvent(long EventId, long WorldTick, string Kind, string Detail);

public sealed record WorldSnapshot(
    string WorldId,
    long WorldTick,
    string MapManifestDigest,
    IReadOnlyList<WorldTile> Tiles,
    IReadOnlyList<WorldMapObject> Objects,
    IReadOnlyList<WorldResource> Resources,
    WorldActor Actor,
    long LatestEventId);

public sealed record WorldEventSlice(long SnapshotTick, long AfterEventId, IReadOnlyList<WorldEvent> Events);

public sealed record WorldReconnectBaseline(WorldSnapshot Snapshot, WorldEventSlice Events);

public sealed record WorldObservation(WorldHandshake Handshake, WorldReconnectBaseline Baseline);

/// <summary>
/// Validates a received observation before rendering it. A failed refresh leaves
/// the last accepted state intact, because the client must never invent a world
/// state during network loss.
/// </summary>
public sealed class WorldObservationSession
{
    public const int SupportedProtocolMajor = 1;

    private static readonly string[] RequiredServerCapabilities =
    [
        "event-replay.read.v1",
        "reconnect-baseline.read.v1",
        "snapshot.read.v1",
    ];

    public WorldObservation? Current { get; private set; }

    public long EventCursor => Current?.Baseline.Snapshot.LatestEventId ?? 0;

    public bool TryAccept(WorldObservation observation, long expectedAfterEventId, out string failure)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedAfterEventId);
        if (observation.Handshake.Protocol.Major != SupportedProtocolMajor)
        {
            failure = $"Protocol major {observation.Handshake.Protocol.Major} is not supported.";
            return false;
        }

        var capabilities = observation.Handshake.ServerCapabilities.ToHashSet(StringComparer.Ordinal);
        if (RequiredServerCapabilities.Any(capability => !capabilities.Contains(capability)))
        {
            failure = "Server does not advertise the required read-only observation capabilities.";
            return false;
        }

        var baseline = observation.Baseline;
        if (baseline.Events.SnapshotTick != baseline.Snapshot.WorldTick ||
            baseline.Events.AfterEventId != expectedAfterEventId ||
            baseline.Snapshot.LatestEventId < baseline.Events.AfterEventId)
        {
            failure = "Reconnect baseline does not describe one coherent snapshot.";
            return false;
        }

        var expectedMinimumEventId = checked(baseline.Events.AfterEventId + 1);
        foreach (var worldEvent in baseline.Events.Events)
        {
            if (worldEvent.EventId != expectedMinimumEventId ||
                worldEvent.EventId > baseline.Snapshot.LatestEventId ||
                worldEvent.WorldTick > baseline.Snapshot.WorldTick)
            {
                failure = "Reconnect event suffix is not ordered against its snapshot.";
                return false;
            }

            expectedMinimumEventId = checked(worldEvent.EventId + 1);
        }

        if (expectedMinimumEventId - 1 != baseline.Snapshot.LatestEventId)
        {
            failure = "Reconnect event suffix is incomplete for its snapshot.";
            return false;
        }

        if (Current is { } current &&
            (baseline.Snapshot.WorldTick < current.Baseline.Snapshot.WorldTick ||
             baseline.Snapshot.LatestEventId < current.Baseline.Snapshot.LatestEventId))
        {
            failure = "Reconnect baseline regresses the last accepted authoritative world.";
            return false;
        }

        Current = observation;
        failure = string.Empty;
        return true;
    }

    public bool TryAccept(WorldObservation observation, out string failure) =>
        TryAccept(
            observation,
            Current?.Baseline.Snapshot.LatestEventId ?? observation.Baseline.Events.AfterEventId,
            out failure);
}

/// <summary>
/// Small HTTP adapter for the read-only observation boundary. It performs no
/// action submission and exposes no client-side mutation route.
/// </summary>
public sealed class WorldObservationClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<WorldObservation> ReconnectAsync(Uri worldBaseUri, long afterEventId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worldBaseUri);
        ArgumentOutOfRangeException.ThrowIfNegative(afterEventId);

        var handshakeUri = new Uri(worldBaseUri, "/api/v1/handshake");
        var reconnectUri = new Uri(worldBaseUri, $"/api/v1/reconnect?afterEventId={afterEventId}");
        var handshakeTask = httpClient.GetFromJsonAsync<WorldHandshake>(handshakeUri, JsonOptions, cancellationToken);
        var reconnectTask = httpClient.GetFromJsonAsync<WorldReconnectBaseline>(reconnectUri, JsonOptions, cancellationToken);
        await Task.WhenAll(handshakeTask, reconnectTask);

        var handshake = await handshakeTask ?? throw new InvalidDataException("Server returned an empty handshake.");
        var baseline = await reconnectTask ?? throw new InvalidDataException("Server returned an empty reconnect baseline.");
        return new WorldObservation(handshake, baseline);
    }
}
