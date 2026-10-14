using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class OrderHistoryTests
{
    [Fact]
    public async Task ClosedHistoryIsBoundedWhileQueuedWorkAndForgottenSubmissionRetriesSurviveReload()
    {
        var (world, actor) = await CreateWorldAsync();
        using var ownedWorld = world;
        var firstRequest = Request(actor, "original-work", "gather berries");
        var first = world.SubmitInstruction(firstRequest);
        world.CancelOrder(Cancel(world, actor, first, "cancel-original"));
        for (var index = 0; index < 40; index++) world.SubmitInstruction(Request(actor, $"unrecognized-{index:D2}", "dance"));
        for (var index = 0; index < 12; index++) world.SubmitInstruction(Request(actor, $"queued-{index:D2}", "gather berries") with { Queue = true });

        var state = world.ExportState();
        var orders = state.Instructions!.Where(item => item.TargetAgentId == actor).ToArray();
        Assert.Equal(12, orders.Count(item => !item.Task!.IsTerminal));
        Assert.Equal(OwnerQueuedInstruction.RecentClosedOrderLimitPerAgent, orders.Count(item => item.Task!.IsTerminal));
        Assert.DoesNotContain(orders, item => item.InstructionId == first.InstructionId);
        Assert.DoesNotContain(first.InstructionId.Value, state.CompletedInstructionIds!);
        Assert.Equal(OwnerQueuedInstruction.RecentClosedOrderLimitPerAgent, state.CompletedInstructionIds!.Count);
        var encoded = WorldRuntimeCodec.Encode(state);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(encoded));
        Assert.Equal(encoded, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.Equal(first, restored.SubmitInstruction(firstRequest));
        Assert.Throws<InvalidOperationException>(() => restored.SubmitInstruction(firstRequest with { Text = "eat berries" }));
        Assert.Equal(encoded, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Fact]
    public async Task CancellationRetriesAreBoundedAndTheNewestReceiptStaysIdempotentAcrossReload()
    {
        var (world, actor) = await CreateWorldAsync();
        using var ownedWorld = world;
        var orders = Enumerable.Range(0, 12).Select(index =>
            world.SubmitInstruction(Request(actor, $"work-{index:D2}", "gather berries") with { Queue = true })).ToArray();
        var cancellations = orders.Select((order, index) => Cancel(world, actor, order, $"cancel-{index:D2}")).ToArray();
        OwnerOrderControlReceipt? latest = null;
        foreach (var cancellation in cancellations) latest = world.CancelOrder(cancellation);
        var state = world.ExportState();
        Assert.Equal(OwnerQueuedInstruction.RecentClosedOrderLimitPerAgent, state.Instructions!.Count);
        Assert.Equal(OwnerQueuedInstruction.RecentClosedOrderLimitPerAgent, state.CompletedInstructionIds!.Count);
        Assert.Equal(OwnerQueuedInstruction.RecentClosedOrderLimitPerAgent, state.OrderCancellations!.Count);
        Assert.All(state.Instructions, item => Assert.Equal(OrderStatus.Cancelled, item.Task!.LifecycleStatus));
        var encoded = WorldRuntimeCodec.Encode(state);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(encoded));
        Assert.Equal(latest, restored.CancelOrder(cancellations[^1]));
        Assert.Throws<ArgumentException>(() => restored.CancelOrder(cancellations[0]));
        Assert.Equal(encoded, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Fact]
    public async Task SubmissionReceiptsMustMatchRetainedMessages()
    {
        var (world, actor) = await CreateWorldAsync();
        using var ownedWorld = world;
        world.SubmitInstruction(Request(actor, "validate-receipt", "gather berries"));
        var state = world.ExportState();
        var recorded = Assert.Single(state.InstructionReceipts);
        var forged = recorded with { Receipt = recorded.Receipt with { RunEpoch = recorded.Receipt.RunEpoch + 1 } };
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(state with { InstructionReceipts = [forged] }));
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(state with { InstructionReceipts = [] }));
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(state with
        { InstructionReceipts = [recorded with { RequestDigest = "order-request-" + new string('0', 64) }] }));
    }

    private static OwnerInstructionRequest Request(string actor, string key, string text) =>
        new(key, "owner:test", actor, OwnerInstructionKind.Order, text);

    private static OwnerOrderCancelRequest Cancel(WorldRuntime world, string actor, OwnerInstructionReceipt receipt, string key) =>
        new(key, "owner:test", world.Society.WorldId, actor, receipt.InstructionId);

    private static async Task<(WorldRuntime World, string Actor)> CreateWorldAsync()
    {
        var state = await GrownAgentHelperMemoryTests.CancellationAdultStateAsync();
        var actor = Assert.Single(state.Society.Society.Births).ChildId;
        var world = WorldRuntime.Restore(state, _ => new DeterministicDecisionProvider());
        world.RenameAgent(actor, "Zoë 🌿 Vale");
        world.Pause();
        return (world, actor);
    }
}
