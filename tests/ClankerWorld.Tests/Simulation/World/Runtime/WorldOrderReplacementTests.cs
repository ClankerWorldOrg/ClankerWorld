using OwnerInstructionReceipt = ClankerWorld.Simulation.World.OwnerInstructionReceipt;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class WorldOrderReplacementTests
{
    [Theory]
    [InlineData("doing", "keep gathering food until cancelled")]
    [InlineData("blocked", "gather wood at 999,999")]
    [InlineData("interrupted", "gather wood")]
    public async Task ReplacementPreservesTheQueueWhenTheCurrentOrderHasStarted(string status, string text)
    {
        using var setup = NormalPathWorld.CreateGenerated("alpha-1-order-audit", _ => new ActionCoverageRecorder(chooseIdle: true));
        var initial = setup.ExportState();
        var actor = initial.Agents[0];
        // Position, goods and orders come from ordinary setup. Only the hunger
        // boundary is positioned so urgent survival can interrupt on the first tick.
        if (status == "interrupted")
            initial = initial with
            {
                Agents = initial.Agents.Select(item => item.AgentId == actor.AgentId
                ? item with { FullnessBasisPoints = 0 } : item).ToArray()
            };
        using var world = WorldRuntime.Restore(initial, _ => new ActionCoverageRecorder(chooseIdle: true));
        var current = Submit(world, actor.AgentId, "current", text);
        var first = Submit(world, actor.AgentId, "queued-first", "gather five wood", queue: true);
        var second = Submit(world, actor.AgentId, "queued-second", "gather stone", queue: true);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var before = world.ExportState();
        Assert.Equal(status, Instruction(before, current).Task!.Status);

        var request = new OwnerInstructionRequest("replacement", "owner:test", actor.AgentId, OwnerInstructionKind.Order, "gather berries");
        var replacement = world.SubmitInstruction(request);
        var after = world.ExportState();
        Assert.Equal("cancelled", Instruction(after, current).Task!.Status);
        Assert.Equal(Instruction(before, first), Instruction(after, first));
        Assert.Equal(Instruction(before, second), Instruction(after, second));
        var bytes = WorldRuntimeCodec.Encode(after);
        using var restored = Restore(bytes);
        Assert.Equal(replacement, restored.SubmitInstruction(request));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Fact]
    public async Task ReplacementPreservesQueuedQuantitiesAndTargetsAndRunsFirstAfterReload()
    {
        using var world = NormalPathWorld.CreateGenerated("alpha-1-order-audit", _ => new ActionCoverageRecorder(chooseIdle: true));
        var actor = world.ExportState().Agents[0];
        var current = Submit(world, actor.AgentId, "current", "eat three berries");
        var queuedWood = Submit(world, actor.AgentId, "queued-wood", "gather five wood at 12,4", queue: true);
        var queuedStone = Submit(world, actor.AgentId, "queued-stone", "gather stone at 13,4", queue: true);
        var before = world.ExportState();
        var replacement = Submit(world, actor.AgentId, "replacement", $"go to {actor.Position.X},{actor.Position.Y}");
        var after = world.ExportState();

        Assert.Equal("cancelled", Instruction(after, current).Task!.Status);
        Assert.Equal("Replaced by a newer order.", Instruction(after, current).Task!.BlockedReason);
        Assert.Equal("waiting", Instruction(after, replacement).Task!.Status);
        Assert.Equal(Instruction(before, queuedWood), Instruction(after, queuedWood));
        Assert.Equal(Instruction(before, queuedStone), Instruction(after, queuedStone));
        Assert.Equal((5, 0, "wood", new GridPoint(12, 4)),
            (Instruction(after, queuedWood).Task!.RequestedUnits, Instruction(after, queuedWood).Task!.CompletedUnits,
                Instruction(after, queuedWood).Task!.TargetMaterialKind, Instruction(after, queuedWood).Task!.TargetPosition));
        Assert.DoesNotContain(queuedWood.InstructionId, after.CompletedInstructionIds!);
        Assert.DoesNotContain(queuedStone.InstructionId, after.CompletedInstructionIds!);

        var bytes = WorldRuntimeCodec.Encode(after);
        using var replay = Restore(bytes);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        var advanced = world.ExportState();
        Assert.Equal("finished", Instruction(advanced, replacement).Task!.Status);
        Assert.Equal(Instruction(before, queuedWood), Instruction(advanced, queuedWood));
        Assert.Equal(Instruction(before, queuedStone), Instruction(advanced, queuedStone));
        Assert.Equal(WorldRuntimeCodec.Encode(advanced), WorldRuntimeCodec.Encode(replay.ExportState()));
    }

    [Fact]
    public async Task PreservedQueueRunsInSubmissionOrderAfterTheReplacementFinishes()
    {
        using var world = NormalPathWorld.CreateGenerated("alpha-1-order-audit", _ => new ActionCoverageRecorder(chooseIdle: true));
        var actor = world.ExportState().Agents[0];
        Submit(world, actor.AgentId, "current", "eat three berries");
        var target = $"go to {actor.Position.X},{actor.Position.Y}";
        var first = Submit(world, actor.AgentId, "queued-first", target, queue: true);
        var second = Submit(world, actor.AgentId, "queued-second", target, queue: true);
        var replacement = Submit(world, actor.AgentId, "replacement", target);
        using var replay = Restore(WorldRuntimeCodec.Encode(world.ExportState()));
        foreach (var expected in new[] { replacement, first, second })
        {
            var oldEventId = world.ExportState().Events.Max(item => item.EventId) + 1;
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            var state = world.ExportState();
            var applied = Assert.Single(state.Events, item => item.EventId >= oldEventId && item.Kind == "instruction_applied");
            Assert.Equal(expected.InstructionId + ":move_to", applied.Detail);
            Assert.Equal("finished", Instruction(state, expected).Task!.Status);
            Assert.Equal(1, Instruction(state, expected).Task!.CompletedUnits);
            Assert.Equal(WorldRuntimeCodec.Encode(state), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
    }

    private static WorldRuntime Restore(byte[] bytes) => WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
        _ => new ActionCoverageRecorder(chooseIdle: true));

    [Fact]
    public void UnknownInstructionsAndExplicitCancellationPreserveOtherQueuedWork()
    {
        using var world = NormalPathWorld.CreateGenerated("alpha-1-order-audit", _ => new ActionCoverageRecorder(chooseIdle: true));
        var actor = world.ExportState().Agents[0].AgentId;
        var current = Submit(world, actor, "current", "eat three berries");
        var first = Submit(world, actor, "queued-first", "gather five wood", queue: true);
        var second = Submit(world, actor, "queued-second", "gather stone", queue: true);
        var before = world.ExportState();
        var unknown = Submit(world, actor, "unknown", "be good");
        Assert.Equal("not_understood", Instruction(world.ExportState(), unknown).Task!.Status);
        foreach (var order in new[] { current, first, second })
            Assert.Equal(Instruction(before, order), Instruction(world.ExportState(), order));

        var cancel = new OwnerOrderCancelRequest("cancel-first", "owner:test", world.Society.WorldId, actor, first.InstructionId);
        Assert.True(world.CancelOrder(cancel).Changed);
        Assert.Equal("cancelled", Instruction(world.ExportState(), first).Task!.Status);
        Assert.Equal(Instruction(before, current), Instruction(world.ExportState(), current));
        Assert.Equal(Instruction(before, second), Instruction(world.ExportState(), second));
        world.CancelOrder(cancel with { IdempotencyKey = "cancel-current", OrderId = current.InstructionId });
        var replacement = Submit(world, actor, "replacement", "gather berries");
        Assert.Equal("waiting", Instruction(world.ExportState(), replacement).Task!.Status);
        Assert.Equal(Instruction(before, second), Instruction(world.ExportState(), second));
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = Restore(bytes);
        Assert.False(restored.CancelOrder(cancel with { IdempotencyKey = "cancel-again" }).Changed);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
    }

    private static OwnerInstructionReceipt Submit(WorldRuntime world, string actor, string key, string text, bool queue = false) =>
        world.SubmitInstruction(new OwnerInstructionRequest(key, "owner:test", actor, OwnerInstructionKind.Order, text, Queue: queue));

    private static OwnerQueuedInstruction Instruction(WorldRuntimeState state, OwnerInstructionReceipt receipt) =>
        Assert.Single(state.Instructions!, item => item.InstructionId == receipt.InstructionId);
}

public sealed partial class WorldRuntimeTests
{
    [Fact]
    public async Task ReplacementWithQueuedWorkDiscardsAHeldTravelReply()
    {
        var provider = new CancellationPlanningProvider(holdFirstOrder: true);
        using var world = CreateCancellationTravelWorld(provider);
        var current = SubmitCancellationTravelOrder(world, "held-current");
        Task? worker = null;
        try
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
            worker = GrownAgentHelperMemoryTests.WaitForRequests(world);
            var position = CancellationActorPosition(world.ExportState());
            var first = world.SubmitInstruction(new("queued-first", "owner:test", HarvestInstructionActor,
                OwnerInstructionKind.Order, $"go to {position.X},{position.Y}", Queue: true));
            var second = world.SubmitInstruction(new("queued-second", "owner:test", HarvestInstructionActor,
                OwnerInstructionKind.Order, "gather five wood", Queue: true));
            var before = world.ExportState();
            var replacement = world.SubmitInstruction(new("replacement", "owner:test", HarvestInstructionActor,
                OwnerInstructionKind.Order, $"go to {position.X},{position.Y}"));
            var replaced = WorldRuntimeCodec.Encode(world.ExportState());
            Assert.Equal(CancellationOrder(before, first.InstructionId), CancellationOrder(world.ExportState(), first.InstructionId));
            Assert.Equal(CancellationOrder(before, second.InstructionId), CancellationOrder(world.ExportState(), second.InstructionId));

            provider.Release.TrySetResult(true);
            await worker;
            Assert.Equal(replaced, WorldRuntimeCodec.Encode(world.ExportState()));
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            var after = world.ExportState();
            Assert.Equal(position, CancellationActorPosition(after));
            Assert.Equal(("cancelled", 0), (CancellationOrder(after, current.InstructionId).Status,
                CancellationOrder(after, current.InstructionId).CompletedUnits));
            Assert.Equal("finished", CancellationOrder(after, replacement.InstructionId).Status);
            Assert.Equal(CancellationOrder(before, first.InstructionId), CancellationOrder(after, first.InstructionId));
            Assert.Equal(CancellationOrder(before, second.InstructionId), CancellationOrder(after, second.InstructionId));
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(after)));
            Assert.Equal(WorldRuntimeCodec.Encode(after), WorldRuntimeCodec.Encode(restored.ExportState()));
        }
        finally
        {
            provider.Release.TrySetResult(true);
            if (worker is not null) await worker;
        }
    }
}
