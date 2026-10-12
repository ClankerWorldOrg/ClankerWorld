namespace ClankerWorld.Server.Pairing;

/// <summary>Log lines for a host the game started on the player's own PC.</summary>
public static partial class LocalServerTelemetry
{
    [LoggerMessage(EventId = 2321, Level = LogLevel.Information,
        Message = "local_server outcome=shutdown_requested")]
    public static partial void ShutdownRequested(ILogger logger);
}
