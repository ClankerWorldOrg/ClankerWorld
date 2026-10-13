using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;
using System.Text.Json;

namespace ClankerWorld.Tests;

public sealed class EventPresentationBoundaryTests
{
    [Fact]
    public void GameShowsTheServerTextWithoutInterpretingTheDetail()
    {
        var worldEvent = new OwnerWorldEvent(1, 2, "tree_planted", "unrelated:detail:with:colons")
        { Text = "Zoë 🌿 planted a tree." };
        Assert.Equal(worldEvent.Text, WorldEventText.Describe(worldEvent, null));
    }

    [Fact]
    public void EventTextDoesNotParseDetailInTheGame()
    {
        var root = Path.Combine(RuntimeWriterScan.RepositoryRoot, "src/ClankerWorld.GodotClient");
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("/obj/", StringComparison.Ordinal) && !path.Contains("/.godot/", StringComparison.Ordinal)))
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("Detail.Split", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Detail.LastIndexOf", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Detail.IndexOf", source, StringComparison.Ordinal);
        }
    }
    [Fact]
    public async Task ObservationsRefreshExistingTextAfterRenameWithoutSavingDescriptions()
    {
        var state = await GrownAgentHelperMemoryTests.CancellationAdultStateAsync();
        var child = state.Society.Society.Births.Single().ChildId;
        using var world = WorldRuntime.Restore(state);
        world.Pause();
        world.SubmitInstruction(new OwnerInstructionRequest("event-text-refresh", "owner", child,
            OwnerInstructionKind.Order, "dance"));
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        var store = new OwnerWorldObservationStore(world);
        var initial = store.GetReconnectBaseline(0);
        var original = initial.Events.Events.Last(item => item.Kind == "instruction_not_understood");
        Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
        world.RenameAgent(child, "Zoë 🌿 Vale");
        var current = store.GetReconnectBaseline(original.EventId).Snapshot;
        Assert.Contains("Zoë 🌿 Vale", current.EventTexts[original.EventId], StringComparison.Ordinal);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var client = JsonSerializer.Deserialize<OwnerWorldSnapshot>(JsonSerializer.Serialize(current, options), options)!;
        var clientEvent = JsonSerializer.Deserialize<OwnerWorldEvent>(JsonSerializer.Serialize(original, options), options)!;
        Assert.Equal(current.EventTexts[original.EventId], WorldEventText.Describe(clientEvent, client));
        Assert.DoesNotContain("eventTexts", System.Text.Encoding.UTF8.GetString(saved), StringComparison.Ordinal);
    }
}
