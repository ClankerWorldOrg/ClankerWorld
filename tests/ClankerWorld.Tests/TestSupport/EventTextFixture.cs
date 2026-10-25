using System.Text.Json;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

internal static class EventTextFixture
{
    public static string Describe(OwnerWorldEvent worldEvent, OwnerWorldSnapshot? snapshot)
    {
        var serverEvent = new OwnerWorldEvent(worldEvent.EventId, worldEvent.WorldTick, worldEvent.Kind, worldEvent.Detail)
        { Payload = worldEvent.Payload ?? EventTextFixturePayload.Create(worldEvent.Kind, worldEvent.Detail, snapshot) };
        var serverSnapshot = snapshot is null ? null : JsonSerializer.Deserialize<OwnerWorldSnapshot>(JsonSerializer.Serialize(snapshot));
        return WorldEventFormatter.Describe(serverEvent, serverSnapshot);
    }
}
