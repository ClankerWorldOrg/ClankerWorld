using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class InstructionStoreTests
{
    private static readonly string FirstAgent = "agent:born:family:" + new string('a', 110);
    private static readonly string SecondAgent = "agent:born:family:" + new string('b', 110);

    [Theory]
    [InlineData(OrderStatus.Queued)]
    [InlineData(OrderStatus.Waiting)]
    [InlineData(OrderStatus.Doing)]
    [InlineData(OrderStatus.Interrupted)]
    [InlineData(OrderStatus.Blocked)]
    public void ActiveOrdersStayInSubmissionOrderThroughEveryActiveStatus(OrderStatus status)
    {
        var store = new InstructionStore(StringComparer.Ordinal);
        var later = Instruction(9, FirstAgent);
        var earlier = Instruction(4, FirstAgent);
        var other = Instruction(2, SecondAgent);
        store.Add(later.IdempotencyKey, later);
        store.Add(other.IdempotencyKey, other);
        store.Add(earlier.IdempotencyKey, earlier);
        var updated = earlier with
        {
            Task = earlier.Task!.Transition(status, status == OrderStatus.Blocked ? "Waiting for a route." : null),
        };
        store[earlier.IdempotencyKey] = updated;

        Assert.Same(updated, store.FirstActiveFor(FirstAgent));
        Assert.Equal([updated, later], store.ActiveFor(FirstAgent));
        Assert.Same(other, store.FirstActiveFor(SecondAgent));
        Assert.False(store.TryAdd(earlier.IdempotencyKey, other));
        Assert.Same(updated, store.FirstActiveFor(FirstAgent));
    }

    [Theory]
    [InlineData(OrderStatus.Finished)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.NotUnderstood)]
    public void ClosingAnOrderAdvancesOnlyItsAgentsQueue(OrderStatus status)
    {
        var store = new InstructionStore(StringComparer.Ordinal);
        var first = Instruction(1, FirstAgent);
        var second = Instruction(2, FirstAgent);
        var other = Instruction(3, SecondAgent);
        foreach (var instruction in new[] { first, second, other }) store.Add(instruction.IdempotencyKey, instruction);
        store[first.IdempotencyKey] = first with { Task = first.Task!.Transition(status) };
        Assert.Same(second, store.FirstActiveFor(FirstAgent));
        Assert.Same(other, store.FirstActiveFor(SecondAgent));
        Assert.Equal(3, store.Values.Count);
        store[second.IdempotencyKey] = second with { Task = second.Task!.Transition(status) };
        Assert.Null(store.FirstActiveFor(FirstAgent));
        Assert.Empty(store.ActiveFor(FirstAgent));
        Assert.Same(other, store.FirstActiveFor(SecondAgent));
    }

    [Fact]
    public void CurrentWorkPrecedesOlderQueuedSubmissionsUntilItCloses()
    {
        var store = new InstructionStore(StringComparer.Ordinal);
        var queued = Instruction(1, FirstAgent);
        var current = Instruction(9, FirstAgent);
        store.Add(queued.IdempotencyKey, queued);
        store.Add(current.IdempotencyKey, current);
        var started = current with { Task = current.Task!.Transition(OrderStatus.Waiting) };
        store[current.IdempotencyKey] = started;
        Assert.Same(started, store.FirstActiveFor(FirstAgent));
        store[current.IdempotencyKey] = started with { Task = started.Task!.Transition(OrderStatus.Cancelled) };
        Assert.Same(queued, store.FirstActiveFor(FirstAgent));
    }

    [Fact]
    public void RebuildingTheStoreKeepsClosedHistoryAndSuggestionsOutOfTheActiveQueue()
    {
        var store = new InstructionStore(StringComparer.Ordinal);
        var pending = Instruction(1, FirstAgent);
        store.Add(pending.IdempotencyKey, pending);
        for (var sequence = 2; sequence <= 100; sequence++)
        {
            var closed = Instruction(sequence, FirstAgent);
            store.Add(closed.IdempotencyKey, closed with { Task = closed.Task!.Transition(OrderStatus.Finished) });
        }
        var suggestion = Instruction(101, FirstAgent) with { Kind = OwnerInstructionKind.Suggestion, Task = null };
        store.Add(suggestion.IdempotencyKey, suggestion);
        store[suggestion.IdempotencyKey] = suggestion with { ObservedTick = 2 };
        Assert.Same(pending, store.FirstActiveFor(FirstAgent));
        var saved = store.Values.ToArray();
        store.Clear();
        Assert.Empty(store.Values);
        Assert.Null(store.FirstActiveFor(FirstAgent));
        foreach (var instruction in saved) Assert.True(store.TryAdd(instruction.IdempotencyKey, instruction));
        Assert.Same(pending, Assert.Single(store.ActiveFor(FirstAgent)));
        Assert.Equal(101, store.Values.Count);
    }

    private static OwnerQueuedInstruction Instruction(long sequence, string agent) => new(
        $"private-instruction-{sequence:D10}", $"request-{sequence}", "owner:test", agent,
        OwnerInstructionKind.Order, "move to 1, 1", 0, 0, sequence, OwnerInstructionState.Queued,
        Task: new(new MoveToOrderDefinition(new(1, 1)), "queued", 1, 0, "arrivals", false));

    [Fact]
    public void PruningKeepsSixNewestClosedOrdersPerAgentAndEveryActiveOrder()
    {
        var store = new InstructionStore(StringComparer.Ordinal);
        var firstActive = Instruction(1, FirstAgent);
        var secondActive = Instruction(2, SecondAgent);
        store.Add(firstActive.IdempotencyKey, firstActive);
        store.Add(secondActive.IdempotencyKey, secondActive);
        for (var sequence = 3; sequence <= 40; sequence++)
        {
            var instruction = Instruction(sequence, sequence % 2 == 0 ? FirstAgent : SecondAgent);
            store.Add(instruction.IdempotencyKey, instruction with { Task = instruction.Task!.Transition(OrderStatus.Cancelled) });
        }
        var retired = store.PruneClosedOrders();
        Assert.Equal(26, retired.Length);
        Assert.Equal(14, store.Count);
        Assert.All(store.Values.Where(item => item.Task!.IsTerminal).GroupBy(item => item.TargetAgentId), group =>
            Assert.Equal(OwnerQueuedInstruction.RecentClosedOrderLimitPerAgent, group.Count()));
        Assert.DoesNotContain(store.Values, item => item.Task!.IsTerminal && item.SubmissionSequence < 29);
        Assert.Same(firstActive, store.FirstActiveFor(FirstAgent));
        Assert.Same(secondActive, store.FirstActiveFor(SecondAgent));
        Assert.Empty(store.PruneClosedOrders());
    }
}
