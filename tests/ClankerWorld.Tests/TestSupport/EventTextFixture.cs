using System.Text.Json;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

internal static class EventTextFixture
{
    public static string Describe(OwnerWorldEvent worldEvent, OwnerWorldSnapshot? snapshot)
    {
        var serverEvent = JsonSerializer.Deserialize<EventView>(JsonSerializer.Serialize(worldEvent))!;
        var serverSnapshot = snapshot is null ? null : JsonSerializer.Deserialize<WorldSnapshotView>(JsonSerializer.Serialize(snapshot));
        return WorldEventFormatter.Describe(serverEvent, serverSnapshot);
    }
}
