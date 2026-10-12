namespace ClankerWorld.Server.Control;

/// <summary>
/// Warnings for requests the host refuses before reading them. Each kind logs at
/// most once a minute, with the number of refusals it skipped since its last
/// line, so a burst of bad requests cannot fill the log. The fields are fixed
/// words and numbers, never request text.
/// </summary>
public sealed partial class HostRequestRefusals(ILogger<HostRequestRefusals> logger)
{
    private readonly Window bodyTooLarge = new();
    private readonly Window localApprovalHost = new();

    public void BodyTooLarge(string routeKind, long limitBytes)
    {
        if (bodyTooLarge.TryEnter(out var skipped)) LogBodyTooLarge(logger, routeKind, limitBytes, skipped);
    }

    public void LocalApprovalHostRefused()
    {
        if (localApprovalHost.TryEnter(out var skipped)) LogLocalApprovalHostRefused(logger, skipped);
    }

    [LoggerMessage(EventId = 2323, Level = LogLevel.Warning,
        Message = "request_body_limit outcome=refused route_kind={RouteKind} limit_bytes={LimitBytes} skipped={Skipped}")]
    private static partial void LogBodyTooLarge(ILogger logger, string routeKind, long limitBytes, int skipped);

    [LoggerMessage(EventId = 2324, Level = LogLevel.Warning,
        Message = "local_approval outcome=refused reason=host_name skipped={Skipped}")]
    private static partial void LogLocalApprovalHostRefused(ILogger logger, int skipped);

    private sealed class Window
    {
        private static readonly long Interval = (long)TimeSpan.FromMinutes(1).TotalMilliseconds;
        private readonly Lock gate = new();
        private long nextLineAt = long.MinValue;
        private int skipped;

        public bool TryEnter(out int skippedBefore)
        {
            lock (gate)
            {
                var now = Environment.TickCount64;
                if (now < nextLineAt)
                {
                    skipped++;
                    skippedBefore = 0;
                    return false;
                }

                nextLineAt = now + Interval;
                skippedBefore = skipped;
                skipped = 0;
                return true;
            }
        }
    }
}
