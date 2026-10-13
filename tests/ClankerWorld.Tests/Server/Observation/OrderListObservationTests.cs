using System.Text.Json;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;
using OwnerWorldInstruction = ClankerWorld.GodotClient.Protocol.OwnerWorldInstruction;

namespace ClankerWorld.Tests;

/// <summary>
/// The agent card draws the orders the host reports and keeps no queue of its
/// own, so every open order, with its status and reason, has to reach the game
/// as the host holds it, and the same list has to come back after a reload.
/// </summary>
public sealed class OrderListObservationTests
{
    private const string Agent = "founder-ilya";
    private static readonly JsonSerializerOptions HostJson = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions GameJson = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task EveryOrderReachesTheGameWithItsHostStatusAndSurvivesReload()
    {
        using var genesis = new WorldRuntime("order-list-projection");
        // A full agent cannot eat yet, so the eating order stays open and says why.
        var state = genesis.ExportState();
        state = state with
        {
            Agents = state.Agents.Select(item => item.AgentId == Agent
                ? item with { FullnessBasisPoints = 9_000 }
                : item).ToArray(),
        };
        using var world = WorldRuntime.Restore(state);
        var replaced = Submit(world, "replaced", "gather berries");
        var current = Submit(world, "current", "eat 3 berries");
        var queued = Enumerable.Range(1, 5)
            .Select(index => Submit(world, $"queued-{index}", "go to the berry patch", queue: true))
            .ToArray();
        var typo = Submit(world, "typo", "build a house");
        var suggestion = world.SubmitInstruction(new OwnerInstructionRequest("suggestion", "owner:test", Agent,
            OwnerInstructionKind.Suggestion, "Rest by the fire tonight."));
        var cancelled = world.CancelOrder(new OwnerOrderCancelRequest("cancel-last", "owner:test",
            world.ExportState().Society.Society.WorldId, Agent, queued[^1].InstructionId));
        Assert.True(cancelled.Changed);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        var received = Received(world);
        var orders = received.Where(item => item.Kind == "order").ToArray();
        // More orders than the card's four-message history holds all reach the game.
        Assert.Equal(
            new[] { replaced, current }.Concat(queued).Append(typo).Select(item => item.InstructionId.Value).ToArray(),
            orders.Select(item => item.InstructionId).ToArray());
        Assert.Contains(received, item => item.InstructionId == suggestion.InstructionId && item.Kind == "suggestion");

        var held = Assert.Single(world.ExportState().Instructions!, item => item.InstructionId == current.InstructionId).Task!;
        var shown = orders[1].Task!;
        Assert.Equal("blocked", shown.Status);
        Assert.Equal(held.Status, shown.Status);
        Assert.Equal("Waiting until hungry enough to eat.", shown.BlockedReason);
        Assert.Equal(held.BlockedReason, shown.BlockedReason);
        Assert.Equal((3, 0, "food_items"), (shown.RequestedUnits, shown.CompletedUnits, shown.ProgressUnit));
        Assert.All(orders.Skip(2).Take(4), item =>
        {
            Assert.Equal("queued", item.State);
            Assert.Equal("queued", item.Task!.Status);
        });
        Assert.Equal(("completed", "cancelled", "Replaced by a newer order."),
            (orders[0].State, orders[0].Task!.Status, orders[0].Task!.BlockedReason));
        Assert.Equal(("completed", "cancelled"), (orders[6].State, orders[6].Task!.Status));
        Assert.Equal(("completed", "not_understood"), (orders[7].State, orders[7].Task!.Status));

        // A reload shows the same orders in the same states; nothing on the card depends on the old session.
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
            WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(received, Received(reloaded));
    }

    private static OwnerInstructionReceipt Submit(WorldRuntime world, string key, string text, bool queue = false) =>
        world.SubmitInstruction(new OwnerInstructionRequest(key, "owner:test", Agent, OwnerInstructionKind.Order, text, queue));

    /// <summary>The agent's messages as the game reads them from the host's snapshot.</summary>
    private static OwnerWorldInstruction[] Received(WorldRuntime world) =>
        JsonSerializer.Deserialize<OwnerWorldInstruction[]>(
                JsonSerializer.Serialize(new OwnerWorldObservationStore(world).GetSnapshot().Instructions, HostJson), GameJson)!
            .Where(item => item.TargetAgentId == Agent)
            .ToArray();
}
