using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using Microsoft.Extensions.Hosting;

namespace ClankerWorld.Tests;

public sealed class LauncherHostLifecycleTests
{
    [Fact]
    public async Task IdlePresenceWaitWakesOnlyForAnAuthenticatedClientAndCanBeCancelled()
    {
        var clock = new ManualTimeProvider();
        var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(5), clock);
        var waiting = presence.WaitForClientAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromDays(1));
        Assert.False(waiting.IsCompleted);
        presence.RecordAuthenticatedReconnect("owner");
        await ModelWorkHarness.WaitForSignalAsync(waiting);
        Assert.True(presence.HasActiveClient);
        clock.Advance(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        var again = presence.WaitForClientAsync(cancellation.Token);
        Assert.False(again.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => again);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LastDisconnectSavesPausedBeforeOptionalExitAndRetriesARefusedWrite(bool stop, bool failWrite)
    {
        var directory = Directory.CreateTempSubdirectory("launcher-host-lifecycle-");
        try
        {
            var clock = new ManualTimeProvider();
            var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(5), clock);
            using var world = NormalPathWorld.CreateGenerated("launcher-host-lifecycle", _ => new DeterministicDecisionProvider());
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            var lifetime = new Lifetime(() =>
            {
                using var saved = file.LoadOrCreate("launcher-host-lifecycle");
                Assert.True(saved.Society.IsPaused);
                Assert.Equal(world.WorldTick, saved.WorldTick);
            });
            using var service = new WorldRuntimeService(world, file, presence,
                hosting: new LauncherHostOptions(stop), lifetime: lifetime);
            presence.RecordAuthenticatedReconnect("owner");
            Assert.True(await service.AdvanceWithModelWorkAsync(world));
            var tick = world.WorldTick;
            var previous = File.ReadAllBytes(file.Path);
            if (failWrite)
            {
                File.Delete(file.Path);
                Directory.CreateDirectory(file.Path);
            }
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.False(await service.AdvanceWithModelWorkAsync(world));
            Assert.True(world.Society.IsPaused);
            Assert.Equal(tick, world.WorldTick);
            Assert.Equal(failWrite, service.HostIdleSaveFailed);
            Assert.Equal(stop && !failWrite ? 1 : 0, lifetime.StopCalls);
            if (failWrite)
            {
                Directory.Delete(file.Path);
                File.WriteAllBytes(file.Path, previous);
                Assert.False(await service.AdvanceWithModelWorkAsync(world));
                Assert.False(service.HostIdleSaveFailed);
                Assert.Equal(1, lifetime.StopCalls);
            }
            using var restored = file.LoadOrCreate("launcher-host-lifecycle");
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(restored.ExportState()));
            Assert.False(await service.AdvanceWithModelWorkAsync(world));
            Assert.Equal(tick, world.WorldTick);
            Assert.Equal(stop ? 1 : 0, lifetime.StopCalls);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task AuthenticatedReconnectDuringIdleSaveKeepsTheHostAndTheNextIdleTransition()
    {
        var directory = Directory.CreateTempSubdirectory("launcher-host-reconnect-");
        try
        {
            var clock = new ManualTimeProvider();
            var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(5), clock);
            using var world = NormalPathWorld.CreateGenerated("launcher-host-reconnect", _ => new DeterministicDecisionProvider());
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            var lifetime = new Lifetime(() => { });
            var log = new RecordingLogger<WorldRuntimeService>();
            using var service = new WorldRuntimeService(world, file, presence, log,
                hosting: new LauncherHostOptions(true), lifetime: lifetime);
            presence.RecordAuthenticatedReconnect("owner");
            Assert.True(await service.AdvanceWithModelWorkAsync(world));
            var tick = world.WorldTick;
            // Reconnect after the real idle checkpoint commits, before the exit decision.
            var reconnected = log.WaitForAsync(message =>
            {
                if (!message.StartsWith("launcher_host outcome=saved ", StringComparison.Ordinal)) return false;
                presence.RecordAuthenticatedReconnect("owner");
                return true;
            });
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.False(await service.AdvanceWithModelWorkAsync(world));
            Assert.True(reconnected.IsCompletedSuccessfully);
            Assert.True(presence.HasActiveClient);
            Assert.Equal(0, lifetime.StopCalls);
            Assert.True(world.Society.IsPaused);
            Assert.Equal(tick, world.WorldTick);
            using var restored = file.LoadOrCreate("launcher-host-reconnect");
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(restored.ExportState()));
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.False(await service.AdvanceWithModelWorkAsync(world));
            Assert.Equal(1, lifetime.StopCalls);
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed class Lifetime(Action beforeStop) : IHostApplicationLifetime
    {
        public int StopCalls { get; private set; }
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { beforeStop(); StopCalls++; }
    }
}
