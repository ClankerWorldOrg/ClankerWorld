using ClankerWorld.Simulation;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    private static readonly Lazy<Task<byte[]>> CancellationAdult = new(CreateCancellationAdultAsync);

    internal static async Task<WorldRuntimeState> CancellationAdultStateAsync() =>
        WorldRuntimeCodec.Decode(await CancellationAdult.Value);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NativeBornAdultOrderCancelsWithCompleteTargetAndReloadedReceipt(bool bornAdult)
    {
        var state = WorldRuntimeCodec.Decode(await CancellationAdult.Value);
        var birth = Assert.Single(state.Society.Society.Births);
        var actor = bornAdult ? birth.ChildId : birth.PrimaryCaregiverId;
        var other = state.Society.Society.Agents.First(person => person.Id != actor && person.Id.Length <= 128).Id;
        Assert.InRange(birth.ChildId.Length, 1, DerivedId.MaximumLength);
        Assert.InRange(birth.PrimaryCaregiverId.Length, 1, 128);
        using var world = WorldRuntime.Restore(state, _ => new QuietProvider());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            _ => new QuietProvider());
        foreach (var runtime in new[] { world, replay })
        {
            runtime.Pause();
            runtime.SetJevEnabled(false);
        }
        var request = new OwnerInstructionRequest("native-cancel-order", "owner:test", actor,
            OwnerInstructionKind.MustDo, "Eat 1 food");
        var instruction = world.SubmitInstruction(request);
        Assert.Equal(instruction, replay.SubmitInstruction(request));
        var queuedRequest = request with { IdempotencyKey = "native-cancel-queued", Queue = true };
        var queued = world.SubmitInstruction(queuedRequest);
        Assert.Equal(queued, replay.SubmitInstruction(queuedRequest));
        var waiting = Assert.Single(world.ExportState().Instructions!, item => item.InstructionId == instruction.InstructionId);
        Assert.Equal(actor, waiting.TargetAgentId);
        Assert.Equal("consume_food", waiting.Order!.Action);
        Assert.Equal("waiting", waiting.Order.Status);
        var cancel = new OwnerOrderCancelRequest("native-cancel", "owner:test", world.Society.WorldId, actor, instruction.InstructionId);
        var receipt = world.CancelOrder(cancel);
        var refused = cancel with { IdempotencyKey = "native-refused-cancel" };
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        foreach (var invalid in new[]
                 {
                     refused with { IdempotencyKey = new string('k', 129) },
                     refused with { IssuerId = new string('i', 129) },
                     refused with { WorldId = new string('w', 129) },
                     refused with { OrderId = new string('o', 129) },
                     refused with { TargetAgentId = actor + "\0" },
                 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => world.CancelOrder(invalid));
            Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        }
        Assert.Throws<ArgumentException>(() => world.CancelOrder(refused with { TargetAgentId = " " }));
        Assert.Throws<ArgumentException>(() => world.CancelOrder(refused with { TargetAgentId = other }));
        Assert.Throws<ArgumentException>(() => world.CancelOrder(refused with { TargetAgentId = actor + ":unknown" }));
        Assert.Throws<ArgumentException>(() => world.CancelOrder(refused with { OrderId = "missing-order" }));
        Assert.Throws<InvalidOperationException>(() => world.CancelOrder(refused with { WorldId = "another-world" }));
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.True(receipt.Changed);
        Assert.Equal("cancelled", receipt.Status);
        Assert.Equal(receipt, replay.CancelOrder(cancel));
        var cancelled = world.ExportState();
        Assert.Equal("cancelled", Assert.Single(cancelled.Instructions!, item => item.InstructionId == instruction.InstructionId).Order!.Status);
        Assert.Equal("queued", Assert.Single(cancelled.Instructions!, item => item.InstructionId == queued.InstructionId).Order!.Status);
        var savedCancellation = Assert.Single(cancelled.OrderCancellations!);
        Assert.Equal(actor, savedCancellation.TargetAgentId);
        Assert.Equal(instruction.InstructionId, savedCancellation.OrderId);
        Assert.Equal(world.Society.WorldId, savedCancellation.WorldId);
        Assert.Equal(receipt, savedCancellation.Receipt);
        var saved = WorldRuntimeCodec.Encode(cancelled);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(replay.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.Equal(receipt, restored.CancelOrder(cancel with { TargetAgentId = " " + actor + " " }));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.Throws<InvalidOperationException>(() => restored.CancelOrder(cancel with { OrderId = queued.InstructionId }));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        world.Resume();
        replay.Resume();
        var beforeRefusal = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(beforeRefusal, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 3; tick++)
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            await WaitForRequests(world);
            Assert.True((await replay.AdvanceOneTickNonBlockingAsync()).Advanced);
            await WaitForRequests(replay);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        world.Validate();
    }

    private static async Task<byte[]> CreateCancellationAdultAsync()
    {
        var state = WorldRuntimeCodec.Decode(await Born.Value);
        var child = Assert.Single(state.Society.Society.Births).ChildId;
        var society = state.Society.Society;
        foreach (var parent in society.Relationships.Where(edge => edge.Type == SocietyRelationshipType.BiologicalParentage && edge.TargetId == child)
                     .Select(edge => edge.ProposerId))
            society = ChosenBirthNameTestFixture.NameParent(society, parent);
        state = state with { Society = state.Society with { Society = society } };
        using var world = WorldRuntime.Restore(state, id => id == child ? new InitialIdentityProvider() : new QuietProvider());
        world.Pause();
        world.SetLifePace(365);
        world.Resume();
        while (world.Society.AgeAt(world.Society.GetAgent(child), world.WorldTick) < 15)
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            await WaitForRequests(world);
        }
        world.Pause();
        world.SetLifePace(1);
        world.Resume();
        for (var tick = 0; tick < 40 && (world.Agents.Single(person => person.AgentId == child).IdentityChoicePending ||
                 world.Society.GetAgent(child).NeedsName); tick++)
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            await WaitForRequests(world);
        }
        Assert.Equal(SocietyAgeBand.Adult, world.Society.GetAgent(child).AgeBand);
        Assert.False(world.Society.GetAgent(child).NeedsName);
        Assert.False(world.Agents.Single(person => person.AgentId == child).IdentityChoicePending);
        world.Pause();
        return WorldRuntimeCodec.Encode(world.ExportState());
    }

}
