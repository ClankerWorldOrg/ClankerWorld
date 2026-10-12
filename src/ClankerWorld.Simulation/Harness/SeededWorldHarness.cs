using System.Globalization;
using System.Text;
using ClankerWorld.Simulation.Persistence;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Harness;

public sealed record HarnessActor(
    string Id,
    GridPoint Position,
    int HungerBasisPoints,
    int FoodItems,
    int WoodItems);

public sealed record RuntimeResource(string Id, ResourceState State);

/// <summary>
/// The complete small state for the #88 fixture. The map manifest stays
/// immutable; resource availability and the actor live in the tick state.
/// </summary>
public sealed record HarnessWorld(
    WorldIdentity Identity,
    WorldMap Map,
    HarnessActor Actor,
    IReadOnlyList<RuntimeResource> Resources,
    IReadOnlyList<PersistenceEvent> Events)
{
    public RuntimeResource GetResource(string id) =>
        Resources.Single(resource => string.Equals(resource.Id, id, StringComparison.Ordinal));
}

/// <summary>
/// One deliberately small ordered kernel path: needs first, then exactly one
/// movement/work action, then a durable event for the completed tick.
/// </summary>
public static class ScriptedHarness
{
    private const int NeedDrainPerTick = 100;
    private const int FoodRecovery = 2_000;
    private const string ActorId = "actor-scout";

    public static HarnessWorld CreateGenesis(string worldSeed)
    {
        var map = SmallTemperateMapGenerator.Generate(worldSeed);
        var identity = CreateIdentity(worldSeed, map);
        return CreateGenesis(identity);
    }

    public static HarnessWorld CreateGenesis(WorldIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var map = SmallTemperateMapGenerator.Generate(identity.WorldSeed);
        if (map.GenerationAttempt != identity.GenerationAttempt ||
            !string.Equals(map.ManifestDigest, identity.InitialMapManifestDigest, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The saved map identity does not reproduce the selected manifest.");
        }

        var founder = map.GetObject("founder-scout");
        var actor = new HarnessActor(ActorId, founder.Position, 5_000, 0, 0);
        var resources = map.Resources
            .OrderBy(resource => resource.Id, StringComparer.Ordinal)
            .Select(resource => new RuntimeResource(resource.Id, ResourceState.Available))
            .ToArray();
        return new HarnessWorld(identity with { WorldTick = 0 }, map, actor, resources, []);
    }

    public static HarnessWorld RunToFoodConsumed(HarnessWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var berry = world.Map.GetResource("berry-patch");
        var current = MoveUntilAt(world, berry.Position);
        current = Harvest(current, berry.Id);
        return Consume(current);
    }

    public static HarnessWorld RunEntireSequence(string worldSeed) =>
        RunToFoodConsumed(CreateGenesis(worldSeed));

    /// <summary>
    /// Advances exactly one committed action from the small scripted fixture.
    /// This lets a host own the world clock without teaching a client how to
    /// mutate or replay simulation state.
    /// </summary>
    public static bool TryAdvanceOneAction(HarnessWorld world, out HarnessWorld advanced)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (world.Actor.FoodItems > 0)
        {
            advanced = Consume(world);
            return true;
        }

        var berry = world.Map.GetResource("berry-patch");
        if (world.GetResource(berry.Id).State == ResourceState.Available)
        {
            advanced = world.Actor.Position == berry.Position
                ? Harvest(world, berry.Id)
                : MoveOneStep(world, berry.Position);
            return true;
        }

        advanced = world;
        return false;
    }

    /// <summary>
    /// Phase 3 executor entry points. They reuse the fixture's needs/resource
    /// commit path while allowing cognition to select the action instead of a
    /// hard-coded script selecting it.
    /// </summary>
    public static HarnessWorld ApplyMovement(HarnessWorld world, GridPoint destination)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Actor.Position == destination)
        {
            throw new InvalidOperationException("A Phase 3 movement action must advance to a different tile.");
        }

        return Commit(
            world,
            actor => actor with { Position = destination },
            resources => resources,
            1,
            $"move:{ActorId}:{destination.X},{destination.Y}");
    }

    public static HarnessWorld ApplyHarvest(HarnessWorld world, string resourceId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Harvest(world, resourceId);
    }

    public static HarnessWorld ApplyConsume(HarnessWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Consume(world);
    }

    public static HarnessWorld ApplyIdle(HarnessWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Commit(
            world,
            actor => actor,
            resources => resources,
            0,
            $"idle:{ActorId}");
    }

    public static HarnessWorld ReplayFromGenesis(WorldIdentity identity, IReadOnlyList<PersistenceEvent> events)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(events);
        var replay = CreateGenesis(identity with { WorldTick = 0 });
        foreach (var expected in events.OrderBy(worldEvent => worldEvent.EventId))
        {
            replay = ReplayOne(replay, expected);
        }

        return replay;
    }

    private static HarnessWorld ReplayOne(HarnessWorld world, PersistenceEvent expected)
    {
        if (expected.Detail is null)
        {
            throw new InvalidDataException("Harness events must include an action detail.");
        }

        HarnessWorld replayed;
        if (expected.Detail.StartsWith("move:", StringComparison.Ordinal))
        {
            var components = expected.Detail.Split(':', StringSplitOptions.None);
            if (components.Length != 3 || !string.Equals(components[1], ActorId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The move event detail is invalid.");
            }

            replayed = MoveOneStep(world, ParsePoint(components[2]));
        }
        else if (expected.Detail.StartsWith("harvest:", StringComparison.Ordinal))
        {
            replayed = Harvest(world, expected.Detail["harvest:".Length..]);
        }
        else if (string.Equals(expected.Detail, $"consume:{ActorId}", StringComparison.Ordinal))
        {
            replayed = Consume(world);
        }
        else
        {
            throw new InvalidDataException("The harness event action is not recognized.");
        }

        var actual = replayed.Events[^1];
        if (actual != expected)
        {
            throw new InvalidDataException("Replaying the action log produced a different committed event.");
        }

        return replayed;
    }

    private static HarnessWorld MoveUntilAt(HarnessWorld world, GridPoint destination)
    {
        var current = world;
        while (current.Actor.Position != destination)
        {
            current = MoveOneStep(current, destination);
        }

        return current;
    }

    private static HarnessWorld MoveOneStep(HarnessWorld world, GridPoint destination)
    {
        var route = DeterministicRouteFinder.Find(world.Map, world.Actor.Position, destination);
        if (route.Count < 2)
        {
            throw new InvalidOperationException("A movement event must advance to a different tile.");
        }

        var next = route[1];
        return Commit(
            world,
            actor => actor with { Position = next },
            resources => resources,
            1,
            $"move:{ActorId}:{next.X},{next.Y}");
    }

    private static HarnessWorld Harvest(HarnessWorld world, string resourceId)
    {
        var source = world.Map.GetResource(resourceId);
        if (world.Actor.Position != source.Position || world.GetResource(resourceId).State != ResourceState.Available)
        {
            throw new InvalidOperationException("Harvesting requires an available source at the actor's position.");
        }

        return Commit(
            world,
            actor => source.Kind switch
            {
                "food" => actor with { FoodItems = checked(actor.FoodItems + 1) },
                "construction" => actor with { WoodItems = checked(actor.WoodItems + 1) },
                _ => throw new InvalidOperationException("The resource kind is not harvestable by this fixture."),
            },
            resources => resources
                .Select(resource => string.Equals(resource.Id, resourceId, StringComparison.Ordinal)
                    ? resource with { State = ResourceState.Depleted }
                    : resource)
                .ToArray(),
            2,
            $"harvest:{resourceId}");
    }

    private static HarnessWorld Consume(HarnessWorld world)
    {
        if (world.Actor.FoodItems <= 0)
        {
            throw new InvalidOperationException("Consuming requires an available food item.");
        }

        return Commit(
            world,
            actor => actor with
            {
                FoodItems = actor.FoodItems - 1,
                HungerBasisPoints = ClampBasisPoints(actor.HungerBasisPoints + FoodRecovery),
            },
            resources => resources,
            3,
            $"consume:{ActorId}");
    }

    private static HarnessWorld Commit(
        HarnessWorld world,
        Func<HarnessActor, HarnessActor> action,
        Func<IReadOnlyList<RuntimeResource>, IReadOnlyList<RuntimeResource>> resourceAction,
        int counterDelta,
        string detail)
    {
        var nextTick = checked(world.Identity.WorldTick + 1);
        var needsApplied = world.Actor with
        {
            HungerBasisPoints = ClampBasisPoints(world.Actor.HungerBasisPoints - NeedDrainPerTick),
        };
        var actor = action(needsApplied);
        var worldEvent = new PersistenceEvent(
            checked(world.Events.Count + 1L),
            nextTick,
            PersistenceEventKind.CounterAdjusted,
            counterDelta,
            null,
            detail);
        return world with
        {
            Identity = world.Identity with { WorldTick = nextTick },
            Actor = actor,
            Resources = resourceAction(world.Resources),
            Events = world.Events.Append(worldEvent).ToArray(),
        };
    }

    private static WorldIdentity CreateIdentity(string worldSeed, WorldMap map) => new(
        $"harness-{worldSeed}",
        "deterministic-kernel-contract/phase-1",
        "phase-1-harness/v1",
        "1",
        "one-tick-per-minute/v1",
        0,
        worldSeed,
        SmallTemperateMapGenerator.GeneratorId,
        SmallTemperateMapGenerator.GeneratorVersion,
        SmallTemperateMapGenerator.GeneratorConfigDigest,
        map.GenerationAttempt,
        map.ManifestDigest,
        "content-lock/empty-v1",
        "asset-lock/empty-v1");

    private static int ClampBasisPoints(int value) => Math.Clamp(value, 0, 10_000);

    private static GridPoint ParsePoint(string value)
    {
        var parts = value.Split(',', StringSplitOptions.None);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var y))
        {
            throw new InvalidDataException("A grid coordinate must use invariant x,y integers.");
        }

        return new GridPoint(x, y);
    }
}

/// <summary>
/// Persists #88 through the #87 envelope. The payload is canonical state inside
/// the existing snapshot; the outer event log remains the append-only authority.
/// </summary>
public static class HarnessPersistence
{
    public static PersistedWorld Save(HarnessWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return new PersistedWorld(
            CanonicalPersistenceCodec.EncodeSnapshot(new WorldSnapshot(ToMiniatureState(world))),
            CanonicalPersistenceCodec.EncodeEventLog(world.Events));
    }

    public static HarnessWorld Load(PersistedWorld save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var snapshot = CanonicalPersistenceCodec.DecodeSnapshot(save.SnapshotBytes);
        var events = CanonicalPersistenceCodec.DecodeEventLog(save.EventLogBytes);
        var genericReplay = WorldReplay.ReplayGenesis(
            snapshot.State.Identity with { WorldTick = 0 },
            events);
        if (genericReplay.Counter != snapshot.State.Counter ||
            genericReplay.LastEventId != snapshot.State.LastEventId ||
            genericReplay.Identity.WorldTick != snapshot.State.Identity.WorldTick)
        {
            throw new InvalidDataException("The snapshot does not agree with its ordered event suffix.");
        }

        var decoded = HarnessStateCodec.Decode(snapshot.State.Identity, snapshot.State.CanonicalStatePayload, events);
        var physicalReplay = ScriptedHarness.ReplayFromGenesis(snapshot.State.Identity, events);
        if (!string.Equals(StateDigest(decoded), StateDigest(physicalReplay), StringComparison.Ordinal))
        {
            throw new InvalidDataException("The canonical state payload does not match replayed actions.");
        }

        return decoded;
    }

    public static string StateDigest(HarnessWorld world) =>
        CanonicalPersistenceCodec.StateDigest(ToMiniatureState(world));

    public static string EventDigest(HarnessWorld world) =>
        CanonicalPersistenceCodec.EventDigest(world.Events);

    private static MiniatureWorldState ToMiniatureState(HarnessWorld world) => new(
        world.Identity,
        checked(world.Events.Sum(worldEvent => worldEvent.CounterDelta)),
        world.Events.Count == 0 ? 0 : world.Events[^1].EventId,
        HarnessStateCodec.Encode(world));
}

public static class HarnessStateCodec
{
    private const string Header = "clankerworld.seeded-harness-state/v2";

    public static string Encode(HarnessWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var builder = new StringBuilder();
        builder.Append(Header).Append('\n');
        builder.Append("map_manifest_base64=")
            .Append(Convert.ToBase64String(MapManifestCodec.Encode(world.Map))).Append('\n');
        builder.Append("actor=")
            .Append(world.Actor.Id).Append('|')
            .Append(world.Actor.Position.X).Append('|').Append(world.Actor.Position.Y).Append('|')
            .Append(world.Actor.HungerBasisPoints).Append('|')
            .Append(world.Actor.FoodItems).Append('|').Append(world.Actor.WoodItems).Append('\n');
        foreach (var resource in world.Resources.OrderBy(resource => resource.Id, StringComparer.Ordinal))
        {
            builder.Append("resource=").Append(resource.Id).Append('|')
                .Append(resource.State == ResourceState.Available ? "available" : "depleted").Append('\n');
        }

        return builder.ToString();
    }

    public static HarnessWorld Decode(
        WorldIdentity identity,
        string? payload,
        IReadOnlyList<PersistenceEvent> events)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(events);
        if (string.IsNullOrEmpty(payload))
        {
            throw new InvalidDataException("The harness snapshot does not contain canonical state payload.");
        }

        var lines = payload.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 3 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The harness state format is not supported.");
        }

        var encodedManifest = ValueAfterPrefix(lines[1], "map_manifest_base64=");
        byte[] manifest;
        try
        {
            manifest = Convert.FromBase64String(encodedManifest);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The harness map manifest is not base64.", exception);
        }

        var genesis = ScriptedHarness.CreateGenesis(identity);
        var expectedManifest = MapManifestCodec.Encode(genesis.Map);
        if (!manifest.SequenceEqual(expectedManifest) ||
            !string.Equals(MapManifestCodec.Digest(genesis.Map), identity.InitialMapManifestDigest, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The saved map manifest does not match the world identity.");
        }

        var actorParts = ValueAfterPrefix(lines[2], "actor=").Split('|', StringSplitOptions.None);
        if (actorParts.Length != 6 || !string.Equals(actorParts[0], genesis.Actor.Id, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The saved actor state is invalid.");
        }

        var actor = new HarnessActor(
            actorParts[0],
            new GridPoint(ParseInteger(actorParts[1]), ParseInteger(actorParts[2])),
            ParseBasisPoints(actorParts[3]),
            ParseNonNegativeInteger(actorParts[4]),
            ParseNonNegativeInteger(actorParts[5]));
        if (!genesis.Map.IsPassable(actor.Position))
        {
            throw new InvalidDataException("The saved actor is not on passable ground.");
        }

        var savedResources = lines.Skip(3)
            .Select(ParseResource)
            .OrderBy(resource => resource.Id, StringComparer.Ordinal)
            .ToArray();
        if (savedResources.Length != genesis.Resources.Count ||
            !savedResources.Select(resource => resource.Id).SequenceEqual(
                genesis.Resources.Select(resource => resource.Id),
                StringComparer.Ordinal))
        {
            throw new InvalidDataException("The saved resource state does not match the manifest.");
        }

        return new HarnessWorld(identity, genesis.Map, actor, savedResources, events.ToArray());
    }

    private static RuntimeResource ParseResource(string line)
    {
        var parts = ValueAfterPrefix(line, "resource=").Split('|', StringSplitOptions.None);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
        {
            throw new InvalidDataException("The saved resource line is invalid.");
        }

        return parts[1] switch
        {
            "available" => new RuntimeResource(parts[0], ResourceState.Available),
            "depleted" => new RuntimeResource(parts[0], ResourceState.Depleted),
            _ => throw new InvalidDataException("The saved resource state is invalid."),
        };
    }

    private static string ValueAfterPrefix(string line, string prefix) =>
        line.StartsWith(prefix, StringComparison.Ordinal)
            ? line[prefix.Length..]
            : throw new InvalidDataException($"Expected canonical payload line '{prefix}'.");

    private static int ParseInteger(string value)
    {
        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidDataException("The saved integer value is invalid.");
        }

        return result;
    }

    private static int ParseNonNegativeInteger(string value)
    {
        var result = ParseInteger(value);
        return result >= 0 ? result : throw new InvalidDataException("The saved quantity must not be negative.");
    }

    private static int ParseBasisPoints(string value)
    {
        var result = ParseNonNegativeInteger(value);
        return result <= 10_000 ? result : throw new InvalidDataException("The saved need must be basis points.");
    }
}
