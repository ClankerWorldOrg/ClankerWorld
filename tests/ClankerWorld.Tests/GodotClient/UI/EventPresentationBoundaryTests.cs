using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;

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
        var path = Path.Combine(RuntimeWriterScan.RepositoryRoot,
            "src/ClankerWorld.GodotClient/UI/Text/WorldEventText.cs");
        Assert.DoesNotContain(".Detail", File.ReadAllText(path), StringComparison.Ordinal);
    }
}
