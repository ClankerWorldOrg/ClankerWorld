namespace ClankerWorld.Server.Runtime;

/// <summary>
/// Tracks recently authenticated game clients without making transport
/// presence part of the persistent world state. The host stays reachable while
/// the simulation service uses this lease to avoid advancing an unattended
/// world.
/// </summary>
public sealed class OwnerClientPresenceLease
{
    private readonly object gate = new();
    private readonly Dictionary<string, long> lastSeenByDevice = new(StringComparer.Ordinal);
    private readonly TimeProvider timeProvider;
    private TaskCompletionSource arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public OwnerClientPresenceLease(TimeSpan timeout, TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        Timeout = timeout;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public TimeSpan Timeout { get; }

    /// <summary>Idle hosts have no polling timer; authenticated reconnect wakes them.</summary>
    public Task WaitForClientAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            RemoveExpiredUnsafe(timeProvider.GetTimestamp());
            if (lastSeenByDevice.Count > 0) return Task.CompletedTask;
            if (arrived.Task.IsCompleted)
                arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return arrived.Task.WaitAsync(cancellationToken);
        }
    }

    public bool HasActiveClient
    {
        get
        {
            lock (gate)
            {
                RemoveExpiredUnsafe(timeProvider.GetTimestamp());
                return lastSeenByDevice.Count > 0;
            }
        }
    }

    public int ActiveClientCount
    {
        get
        {
            lock (gate)
            {
                RemoveExpiredUnsafe(timeProvider.GetTimestamp());
                return lastSeenByDevice.Count;
            }
        }
    }

    public void RecordAuthenticatedReconnect(string deviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        lock (gate)
        {
            var now = timeProvider.GetTimestamp();
            RemoveExpiredUnsafe(now);
            lastSeenByDevice[deviceId.Trim()] = now;
            arrived.TrySetResult();
        }
    }

    private void RemoveExpiredUnsafe(long now)
    {
        foreach (var deviceId in lastSeenByDevice
                     .Where(item => timeProvider.GetElapsedTime(item.Value, now) >= Timeout)
                     .Select(item => item.Key)
                     .ToArray())
        {
            lastSeenByDevice.Remove(deviceId);
        }
    }
}
