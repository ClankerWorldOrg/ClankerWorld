namespace ClankerWorld.Simulation.World;

public enum OrderStatus
{
    Queued,
    Waiting,
    Doing,
    Interrupted,
    Blocked,
    Finished,
    Cancelled,
    NotUnderstood,
}

public static class OrderStatusRules
{
    public static bool IsTerminal(this OrderStatus status) =>
        status is OrderStatus.Finished or OrderStatus.Cancelled or OrderStatus.NotUnderstood;

    public static string ToWireValue(this OrderStatus status) => status switch
    {
        OrderStatus.Queued => "queued",
        OrderStatus.Waiting => "waiting",
        OrderStatus.Doing => "doing",
        OrderStatus.Interrupted => "interrupted",
        OrderStatus.Blocked => "blocked",
        OrderStatus.Finished => "finished",
        OrderStatus.Cancelled => "cancelled",
        OrderStatus.NotUnderstood => "not_understood",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static OrderStatus Parse(string value) => value switch
    {
        "queued" => OrderStatus.Queued,
        "waiting" => OrderStatus.Waiting,
        "doing" => OrderStatus.Doing,
        "interrupted" => OrderStatus.Interrupted,
        "blocked" => OrderStatus.Blocked,
        "finished" => OrderStatus.Finished,
        "cancelled" => OrderStatus.Cancelled,
        "not_understood" => OrderStatus.NotUnderstood,
        _ => throw new System.Text.Json.JsonException("The order status is unknown."),
    };
}
