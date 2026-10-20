using System.Text.Json;
using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.Server.Observation;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TypedEventPresentationRegressionTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnimalDescriptionsNameThePrimaryAnimalRegardlessOfSnapshotOrder(bool primaryFirst)
    {
        var mother = Animal("animal:mother", "Moss");
        var child = Animal("animal:child", "Fern");
        var male = Animal("animal:male", "Ash", "male");
        var snapshot = Snapshot() with { Animals = primaryFirst ? [child, mother] : [mother, child] };
        Assert.Equal("A foal, Fern, was born.", Describe(new AnimalBornEventPayload(null, child.Id, mother.Id), snapshot));
        snapshot = snapshot with { Animals = primaryFirst ? [mother, male] : [male, mother] };
        Assert.Equal("Moss is expecting a young one; a place is kept for it.",
            Describe(new AnimalBreedingStartedEventPayload(null, mother.Id, male.Id), snapshot));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TownDescriptionsNameTheTargetTownRegardlessOfSnapshotOrder(bool targetFirst)
    {
        var previous = Town("town:previous", "Willow Town");
        var target = Town("town:target", "Birch Town");
        var actor = Person("world:agent:birth:founder:0001:agent:0002:1", "Mira");
        var snapshot = Snapshot() with { Towns = targetFirst ? [target, previous] : [previous, target], Agents = [actor] };
        Assert.Equal("Mira founded Birch Town.",
            Describe(new TownFoundedEventPayload(actor.Id, target.Id, previous.Id, 1, false), snapshot));
        Assert.Equal("Mira moved into abandoned Birch Town. Its laws and everyone's property stay as they were.",
            Describe(new TownResettledEventPayload(actor.Id, target.Id, previous.Id, 1), snapshot));
    }

    [Theory]
    [InlineData("world:agent:buyer")]
    [InlineData(null)]
    public void MarketCancellationNamesBothTradersRegardlessOfWhoCancelled(string? actor)
    {
        var snapshot = Snapshot() with { Agents = [Person("world:agent:seller", "Mira"), Person("world:agent:buyer", "Orin")] };
        var payload = new MarketTradeCancelledEventPayload(actor, "town:market", "market:001", "building:stall",
            "world:agent:seller", "world:agent:buyer", "market-offer:001");
        Assert.Equal("Mira and Orin's Market trade was called off. Nothing changed hands.", Describe(payload, snapshot));
    }

    [Fact]
    public void TextOnlyHostIsRefusedWithoutReplacingTheAcceptedViewOrPairing()
    {
        using var world = new WorldRuntime("typed-event-capability");
        var store = new OwnerWorldObservationStore(world);
        var session = new OwnerWorldObservationSession();
        var registration = new OwnerDeviceRegistration(new("host", "authority"), "device", "fixture-key", "http://127.0.0.1:5188/");
        session.ReplaceRegistration(registration);
        var current = Reconnect(store, 0);
        Assert.True(session.TryAccept(current, 0, out var initialFailure), initialFailure);
        var accepted = session.Current;
        var cursor = session.EventCursor;
        var legacy = Reconnect(store, cursor);
        legacy = legacy with
        {
            Handshake = legacy.Handshake with
            {
                ServerCapabilities = [.. legacy.Handshake.ServerCapabilities.Where(capability =>
                    capability is not ("owner-event-presentation.v1" or "owner-event-presentation.v2")), "owner-event-presentation.v1"],
            },
        };
        Assert.False(session.TryAccept(legacy, cursor, out var failure));
        Assert.Contains("Update the world server", failure, StringComparison.Ordinal);
        Assert.Same(accepted, session.Current);
        Assert.Same(registration, session.Registration);
        Assert.Equal(cursor, session.EventCursor);
    }

    [Fact]
    public void TypedEventHostAdvertisesOnlyTheTypedPresentationContract()
    {
        using var world = new WorldRuntime("typed-event-host-capability");
        var handshake = RoundTrip<OwnerWorldHandshake>(new OwnerWorldObservationStore(world).GetOwnerHandshake());
        Assert.Contains("owner-event-presentation.v2", handshake.ServerCapabilities);
        Assert.DoesNotContain("owner-event-presentation.v1", handshake.ServerCapabilities);
    }

    private static string Describe(WorldEventPayload payload, WorldSnapshotView snapshot) =>
        WorldEventFormatter.Describe(new(1, 25, payload.Kind, payload.DiagnosticDetail) { Payload = payload }, snapshot);

    private static OwnerWorldReconnect Reconnect(OwnerWorldObservationStore store, long cursor) =>
        new(RoundTrip<OwnerWorldHandshake>(store.GetOwnerHandshake()), RoundTrip<OwnerWorldReconnectBaseline>(store.GetReconnectBaseline(cursor)));

    private static T RoundTrip<T>(object value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Wire), Wire)!;
    private static WorldSnapshotView Snapshot() => new("typed-event-goldens", 25, "map", [], [], [], null, 1);
    private static TownView Town(string id, string name) => new(id, name, "founded", 0, [], [], []);
    private static AgentView Person(string id, string name) => new(id, name, "active", new(0, 0), 8000, [], [], new("idle", null, null, [], ""), new(new(0, 0), [], []), false);
    private static AnimalView Animal(string id, string name, string sex = "female") => new(id, name, "horse", sex, 300, "adult", new(0, 0), "household:first", "Mira's household", "cared", null, 0, null, null, null, null, false, [], []);
}
