using System.Text.Json;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class OrderStatusTests
{
    [Fact]
    public void UnknownSavedStatusIsRefusedAsDamagedJson()
    {
        var order = new OrderTask(new MoveToOrderDefinition(new(1, 1)), "queued", 1, 0, "arrivals", false);
        var json = JsonSerializer.Serialize(order).Replace("\"queued\"", "\"unknown\"", StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OrderTask>(json));
    }

    [Theory]
    [InlineData(OrderStatus.Finished)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.NotUnderstood)]
    public void ClosedOrdersCannotReturnToActiveWork(OrderStatus closed)
    {
        var order = new OrderTask(new MoveToOrderDefinition(new(1, 1)), "queued", 1, 0, "arrivals", false);
        var ended = order.Transition(closed);
        Assert.True(ended.IsTerminal);
        Assert.Equal(closed, ended.LifecycleStatus);
        Assert.Throws<InvalidOperationException>(() => ended.Transition(OrderStatus.Doing));
        Assert.Equal(ended, ended.Transition(closed));
        Assert.False(order.IsTerminal);
    }

    [Fact]
    public void BlockedOrdersRequireAReasonAndClearDecisionWaitWhenResuming()
    {
        var order = new OrderTask(new MoveToOrderDefinition(new(1, 1)), "queued", 1, 0, "arrivals", false);
        Assert.Throws<ArgumentException>(() => order.Transition(OrderStatus.Blocked));
        Assert.Throws<ArgumentException>(() => order.Transition(OrderStatus.Doing, waitForDecision: true));
        var blocked = order.Transition(OrderStatus.Blocked, "The route is occupied.", true);
        var resumed = blocked.Transition(OrderStatus.Doing);
        Assert.Null(resumed.BlockedReason);
        Assert.False(resumed.WaitForDecisionAfterFailure);
    }

    [Theory]
    [InlineData(OrderStatus.Queued)]
    [InlineData(OrderStatus.Waiting)]
    [InlineData(OrderStatus.Doing)]
    [InlineData(OrderStatus.Interrupted)]
    [InlineData(OrderStatus.Blocked)]
    [InlineData(OrderStatus.Finished)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.NotUnderstood)]
    public void StatusKeepsItsSaveSpellingThroughReload(OrderStatus status)
    {
        var order = new OrderTask(new MoveToOrderDefinition(new(1, 1)), "queued", 1, 0, "arrivals", false)
            .Transition(status, status == OrderStatus.Blocked ? "Waiting for a route." : null);
        var json = JsonSerializer.Serialize(order);
        var restored = JsonSerializer.Deserialize<OrderTask>(json)!;
        Assert.Equal(order, restored);
        Assert.Equal(status, restored.LifecycleStatus);
        Assert.Contains($"\"Status\":\"{status.ToWireValue()}\"", json);
        Assert.DoesNotContain("LifecycleStatus", json);
    }
}
