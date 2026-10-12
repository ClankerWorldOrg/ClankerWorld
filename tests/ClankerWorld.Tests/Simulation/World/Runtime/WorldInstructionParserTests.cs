using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class WorldInstructionParserTests
{
    private const string TargetAgent = "founder-ilya";

    [Theory]
    [InlineData("gather fruit not berries")]
    [InlineData("harvest fruit from berry-patch")]
    [InlineData("gather berries from berry-patch-unknown")]
    [InlineData("go find food")]
    [InlineData("go share food")]
    [InlineData("go keep food")]
    [InlineData("keep food for winter")]
    [InlineData("go get 2 berries")]
    [InlineData("go get2berries")]
    [InlineData("eat 3 times")]
    [InlineData("eat 1,000,000 berries")]
    [InlineData("gather berries at tile12")]
    [InlineData("gather berries at 12,")]
    [InlineData("gather berries at 12,4 and eat food")]
    [InlineData("stop harvesting berries")]
    [InlineData("avoid eating berries")]
    [InlineData("are you hungry?")]
    public void UnrecognizedOrPartiallyUnderstoodDirectOrdersCloseWithoutChangingTheirWords(string text)
    {
        using var world = new WorldRuntime("strict-order-parse");

        var receipt = world.SubmitInstruction(new OwnerInstructionRequest(
            "strict-unknown", "owner:test", TargetAgent, OwnerInstructionKind.Order, text));

        var state = world.ExportState();
        var instruction = Assert.Single(state.Instructions!, item => item.InstructionId == receipt.InstructionId);
        Assert.Equal(text, instruction.Text);
        Assert.Equal("strict-unknown", instruction.IdempotencyKey);
        Assert.Equal("unknown", instruction.Task!.Action);
        Assert.Equal("not_understood", instruction.Task.Status);
        Assert.Contains(receipt.InstructionId, state.CompletedInstructionIds ?? []);
        Assert.Contains(state.Events, item => item.Kind == "instruction_not_understood" &&
            item.Detail == $"{TargetAgent}:{receipt.InstructionId}");
        world.Validate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnrecognizedMustDoLeavesCurrentAndQueuedOrdersUnchanged(bool queueUnknown)
    {
        using var world = new WorldRuntime($"unknown-does-not-replace-{queueUnknown}");
        var current = world.SubmitInstruction(new OwnerInstructionRequest(
            "recognized-current", "owner:test", TargetAgent, OwnerInstructionKind.Order, "gather berries"));
        var queued = world.SubmitInstruction(new OwnerInstructionRequest(
            "recognized-queued", "owner:test", TargetAgent, OwnerInstructionKind.Order,
            "eat berries", Queue: true));

        var unknown = world.SubmitInstruction(new OwnerInstructionRequest(
            $"unknown-{queueUnknown}", "owner:test", TargetAgent, OwnerInstructionKind.Order,
            "gather fruit not berries", Queue: queueUnknown));

        var state = world.ExportState();
        var savedCurrent = Assert.Single(state.Instructions!, item => item.InstructionId == current.InstructionId);
        var savedQueued = Assert.Single(state.Instructions!, item => item.InstructionId == queued.InstructionId);
        var savedUnknown = Assert.Single(state.Instructions!, item => item.InstructionId == unknown.InstructionId);
        Assert.Equal("waiting", savedCurrent.Task!.Status);
        Assert.Equal("queued", savedQueued.Task!.Status);
        Assert.DoesNotContain(current.InstructionId, state.CompletedInstructionIds ?? []);
        Assert.DoesNotContain(queued.InstructionId, state.CompletedInstructionIds ?? []);
        Assert.Equal("gather fruit not berries", savedUnknown.Text);
        Assert.Equal($"unknown-{queueUnknown}", savedUnknown.IdempotencyKey);
        Assert.Equal("not_understood", savedUnknown.Task!.Status);
        Assert.Contains(unknown.InstructionId, state.CompletedInstructionIds ?? []);
        Assert.DoesNotContain(state.Events, item => item.Kind == "instruction_order_cancelled" &&
            (item.Detail.Contains(current.InstructionId, StringComparison.Ordinal) ||
             item.Detail.Contains(queued.InstructionId, StringComparison.Ordinal)));

        var encoded = WorldRuntimeCodec.Encode(state);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(encoded));
        Assert.Equal(encoded, WorldRuntimeCodec.Encode(restored.ExportState()));
        restored.Validate();
    }

    [Theory]
    [InlineData("gather berries at 12 4", 12, 4)]
    [InlineData("gather berries at (12,4)", 12, 4)]
    public void CoordinateFormsAreConsumedAndSaved(string text, int x, int y)
    {
        using var world = new WorldRuntime("strict-order-coordinates");

        var receipt = world.SubmitInstruction(new OwnerInstructionRequest(
            "coordinate-order", "owner:test", TargetAgent, OwnerInstructionKind.Order, text));

        var state = world.ExportState();
        var order = Assert.Single(state.Instructions!, item => item.InstructionId == receipt.InstructionId).Task!;
        Assert.Equal("harvest_food", order.Action);
        Assert.Equal(new GridPoint(x, y), order.TargetPosition);
        world.Validate();
    }

    [Fact]
    public void ThousandsQuantityIsParsedWithoutChangingTheRequestedAmount()
    {
        using var world = new WorldRuntime("strict-order-thousands");

        var receipt = world.SubmitInstruction(new OwnerInstructionRequest(
            "thousand-food", "owner:test", TargetAgent, OwnerInstructionKind.Order, "eat 1,000 berries"));

        var order = Assert.Single(world.ExportState().Instructions!, item => item.InstructionId == receipt.InstructionId).Task!;
        Assert.Equal("consume_food", order.Action);
        Assert.Equal(1000, order.RequestedUnits);
        Assert.True(order.QuantityIsExplicit);
        Assert.Equal("food_items", order.ProgressUnit);
    }

    [Theory]
    [InlineData("Eat!", "consume_food")]
    [InlineData("gathering berries", "harvest_food")]
    [InlineData("go to the berry patch", "seek_food")]
    [InlineData("keep eating", "consume_food")]
    [InlineData("keep eating food", "consume_food")]
    public void ExplicitlySupportedDirectFormsRemainRecognized(string text, string action)
    {
        using var world = new WorldRuntime("strict-supported-order-forms");

        var receipt = world.SubmitInstruction(new OwnerInstructionRequest(
            "supported-form", "owner:test", TargetAgent, OwnerInstructionKind.Order, text));

        var order = Assert.Single(world.ExportState().Instructions!, item => item.InstructionId == receipt.InstructionId).Task!;
        Assert.Equal(action, order.Action);
        Assert.Equal("waiting", order.Status);
        Assert.Equal(text.StartsWith("keep ", StringComparison.Ordinal),
            order.RepeatUntilCancelled);
    }

}
