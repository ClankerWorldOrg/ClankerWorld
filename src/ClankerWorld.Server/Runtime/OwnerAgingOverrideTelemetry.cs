namespace ClankerWorld.Server.Runtime;

public static partial class OwnerAgingOverrideTelemetry
{
    [LoggerMessage(EventId = 2211, Level = LogLevel.Information,
        Message = "world_aging_override tick={WorldTick} rate={Rate}")]
    public static partial void Changed(ILogger logger, long worldTick, int rate);
}
