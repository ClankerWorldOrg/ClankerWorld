using System.Text;
using System.Text.Json.Nodes;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class WorldConversationTests
{
    [Theory]
    [InlineData("conversations", false)]
    [InlineData("conversations", true)]
    [InlineData("conversationBudgets", false)]
    [InlineData("conversationBudgets", true)]
    public void CurrentSchemaRequiresExplicitConversationHistoryAndDailyBudgets(string member, bool explicitNull)
    {
        using var world = new WorldRuntime("required-current-conversation-state");
        var current = JsonNode.Parse(WorldRuntimeCodec.Encode(world.ExportState()))!;
        if (explicitNull) current["state"]![member] = null;
        else Assert.True(current["state"]!.AsObject().Remove(member));
        var invalid = Encoding.UTF8.GetBytes(current.ToJsonString());
        var error = Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(invalid));
        Assert.Contains($"schema {WorldRuntime.ConversationSchemaVersion}", error.Message, StringComparison.Ordinal);
        Assert.Contains("conversation state and daily budgets", error.Message, StringComparison.Ordinal);
    }
}
