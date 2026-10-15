using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class OrderStoreTests
{
    private static OwnerQueuedInstruction Instruction(long sequence = 1) => new(
        "private-instruction-" + sequence.ToString("D10", System.Globalization.CultureInfo.InvariantCulture),
        "request-" + sequence, "owner", "agent", OwnerInstructionKind.Order, "move to 1 2", 0, 0, sequence,
        OwnerInstructionState.Queued, Task: new("move_to", "waiting", 1, 0, "arrivals", false));

    [Fact]
    public void InvalidProgressIsReportedBeforeItsInstructionIsPublished()
    {
        var store = new OrderStore();
        var original = Instruction();
        store.QueueInstruction(original);
        var invalid = original with { Task = original.Task! with { CompletedUnits = -1 } };
        var failure = Assert.Throws<InvalidDataException>(() => store.RecordProgress(original.IdempotencyKey, invalid));
        Assert.Contains("Owner order", failure.Message, StringComparison.Ordinal);
        Assert.Same(original, store.Instructions[original.IdempotencyKey]);
    }

    [Fact]
    public void ABindingCannotPublishAnotherInstructionsIdempotencyKey()
    {
        var store = new OrderStore();
        var original = Instruction();
        store.QueueInstruction(original);
        Assert.Throws<InvalidDataException>(() => store.RecordStatus(original.IdempotencyKey, original with { IdempotencyKey = "different-key" }));
        Assert.Same(original, Assert.Single(store.Instructions).Value);
    }

    [Fact]
    public void RestoredStoreKeepsReceiptSequenceAndIndependentCompletionState()
    {
        var original = new OrderStore();
        original.QueueInstruction(Instruction(2));
        original.QueueInstruction(Instruction(1));
        var restored = new OrderStore();
        restored.Restore(original.Instructions.Values, original.Completed, original.Cancellations.Values);
        var receipt = restored.Receipts["request-2"];
        restored.Restore(restored.Instructions.Values, restored.Completed, restored.Cancellations.Values);
        Assert.Equal(original.Instructions.Keys, restored.Instructions.Keys);
        Assert.Equal(receipt, restored.Receipts["request-2"]);
        Assert.Equal(3, restored.BeginSubmissionSequence());
        restored.CompleteInstruction(Instruction(2).InstructionId);
        Assert.Empty(original.Completed);
        Assert.Single(restored.Completed);
    }
}
