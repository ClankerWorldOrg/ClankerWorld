using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed partial class PurposefulExplorationTests
{
    [Theory]
    [InlineData("gather wood", "wood")]
    [InlineData("harvest fruit", "fruit")]
    public async Task ActualUntargetedOrderPurposeAppearsOnTheOwnerCardAndListAcrossPauseAndReload(string text, string target)
    {
        var (state, actor, _, _, _) = await MaterialCourse();
        using var world = Restore(state);
        var receipt = world.SubmitInstruction(new("show-scout-purpose", "owner:test", actor, OwnerInstructionKind.Order, text));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(new ExplorationGoal("resource", target, receipt.InstructionId), Person(world, actor).Exploration!.Goal);
        AssertPurpose(world, actor, target);
        world.Pause();
        var paused = WorldRuntimeCodec.Encode(world.ExportState());
        using var loaded = Restore(WorldRuntimeCodec.Decode(paused));
        Assert.Equal(paused, WorldRuntimeCodec.Encode(loaded.ExportState()));
        AssertPurpose(loaded, actor, target);
        loaded.CancelOrder(new("cancel-shown-purpose", "owner:test", loaded.Society.WorldId, actor, receipt.InstructionId));
        var visible = new OwnerWorldObservationStore(loaded).GetSnapshot().Agents.Single(person => person.Id == actor);
        Assert.NotEqual("looking for " + target, visible.PublicIntention!.Summary);
        Assert.Equal("safe_idle", visible.PublicIntention.CandidateId);
        Assert.Equal(("cancelled", 0), (Order(loaded, receipt.InstructionId).Status, Order(loaded, receipt.InstructionId).CompletedUnits));
        loaded.Validate();

        var exploration = Person(loaded, actor).Exploration!;
        var inventory = loaded.Society.Inventory;
        for (var index = 0; index < OwnerQueuedInstruction.RecentClosedOrderLimitPerAgent; index++)
            loaded.SubmitInstruction(new($"retire-purpose-{index}", "owner:test", actor, OwnerInstructionKind.Order, "dance"));
        Assert.DoesNotContain(loaded.ExportState().Instructions!, item => item.InstructionId == receipt.InstructionId);
        var retired = WorldRuntimeCodec.Encode(loaded.ExportState());
        Assert.Equal(exploration with { Goal = null }, Person(loaded, actor).Exploration);
        Assert.Equal(inventory, loaded.Society.Inventory);
        using var retiredReplay = Restore(WorldRuntimeCodec.Decode(retired));
        Assert.Equal(retired, WorldRuntimeCodec.Encode(retiredReplay.ExportState()));
        loaded.Resume();
        retiredReplay.Resume();
        Assert.True((await loaded.AdvanceOneTickAsync()).Advanced);
        Assert.True((await retiredReplay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(loaded.ExportState()), WorldRuntimeCodec.Encode(retiredReplay.ExportState()));
    }

    private static void AssertPurpose(WorldRuntime world, string actor, string target)
    {
        var visible = new OwnerWorldObservationStore(world).GetSnapshot().Agents.Single(person => person.Id == actor);
        Assert.Equal("safe_idle", visible.PublicIntention!.CandidateId);
        Assert.Equal("looking for " + target, visible.PublicIntention.Summary);
        // Local Must Do execution retains the prior admitted model intention.
        // These are the actual card/list arguments; no intention identity is invented.
        Assert.Equal("looking for " + target, ClankerWorld.Contracts.GameUiText.ActivityPhrase(
            visible.PublicIntention.CandidateId, visible.PublicIntention.Summary));
        Assert.Equal("looking for " + target, ClankerWorld.Contracts.GameUiText.ActivityPhrase(
            "knowledge_write:field_record", visible.PublicIntention.Summary));
    }
}
