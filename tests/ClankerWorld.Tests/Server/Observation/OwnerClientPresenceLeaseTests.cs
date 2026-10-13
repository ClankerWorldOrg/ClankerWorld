using System.Collections.Concurrent;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed class OwnerClientPresenceLeaseTests
{
    [Fact]
    public async Task DisconnectCancelsDeferredModelCallButKeepsCommittedWorldAndQueue()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"clankerworld-deferred-presence-{Guid.NewGuid():N}");
        try
        {
            var clock = new ManualTimeProvider();
            var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(5), clock);
            var provider = new WaitingModelProvider();
            using var runtime = new WorldRuntime("deferred-presence", id =>
                id == "founder-scout" ? provider : new DeterministicDecisionProvider());
            using var service = new WorldRuntimeService(runtime,
                new WorldStateFile(Path.Combine(directory, "world.json")), presence);
            presence.RecordAuthenticatedReconnect("owner");
            Assert.True(await service.AdvanceWithModelWorkAsync(runtime));
            await ModelWorkHarness.For(runtime).WaitAsync(provider.Started.Task);
            var committedTick = runtime.WorldTick;
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.False(await service.AdvanceWithModelWorkAsync(runtime));
            Assert.True(provider.ReceivedCancellation.IsCancellationRequested);
            Assert.Equal(committedTick, runtime.WorldTick);
            Assert.Contains(runtime.ExportState().Society.Cognition.Queue,
                item => item.AgentId == "founder-scout");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class WaitingModelProvider : IDecisionProvider
    {
        public InlineCompletionSource<bool> Started { get; } = new(TaskCreationOptions.None);
        public CancellationToken ReceivedCancellation { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Jev;
        public long ProviderEpoch => 1;
        public async ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            ReceivedCancellation = cancellationToken;
            Started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The hosted request must be cancelled.");
        }
    }

    [Theory]
    [InlineData(NormalPathWorld.Founder1)]
    public async Task ModelFailureLogReportsCompleteActorAndOutcomeWithoutProviderExceptionText(string actorId)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"clankerworld-hosted-log-{Guid.NewGuid():N}");
        try
        {
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(1));
            var provider = new ThrowingModelProvider();
            var logger = new RecordingLogger<WorldRuntimeService>();
            // Begin with the normal active content and idle peers, so startup
            // changes do not queue another decision while testing one failure.
            using var runtime = NormalPathWorld.CreateGenerated("hosted-log", id =>
                id == actorId ? provider : new ActionCoverageRecorder(chooseIdle: true));
            using var service = new WorldRuntimeService(runtime,
                new WorldStateFile(Path.Combine(directory, "world.json")), presence, logger);
            presence.RecordAuthenticatedReconnect("owner");
            Assert.True(await service.AdvanceWithModelWorkAsync(runtime));
            await ModelWorkHarness.For(runtime).WaitAsync(provider.Started.Task);
            for (var attempt = 0; attempt < 50 &&
                 !logger.Messages.Any(message => message.Contains("model_decision_progress", StringComparison.Ordinal) &&
                     message.Contains("model_unavailable", StringComparison.Ordinal)); attempt++)
            {
                await service.AdvanceWithModelWorkAsync(runtime);
            }
            Assert.Contains(logger.Messages, message => message.Contains("model_decision_progress", StringComparison.Ordinal) &&
                message.Contains("model_unavailable", StringComparison.Ordinal));
            Assert.Equal(1, provider.Calls);
            Assert.All(logger.Messages.Where(message => message.Contains("model_decision_progress tick=", StringComparison.Ordinal)),
                message => Assert.Contains($"agent={actorId} outcome=", message, StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message => message.Contains("super-secret-api-key", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class ThrowingModelProvider : IDecisionProvider
    {
        public int Calls { get; private set; }
        public InlineCompletionSource<bool> Started { get; } = new(TaskCreationOptions.None);
        public DecisionProviderKind Kind => DecisionProviderKind.Jev;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            Started.TrySetResult(true);
            throw new HttpRequestException("super-secret-api-key-must-not-appear");
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DisconnectOrPauseCancelsAnInFlightProviderWithoutAdvancing(bool manualPause, bool deferCancellation)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"clankerworld-presence-cancel-{Guid.NewGuid():N}");
        try
        {
            var clock = new ManualTimeProvider();
            var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(5), clock);
            using var provider = new WaitingProvider(deferCancellation);
            using var runtime = new WorldRuntime("presence-cancel", _ => provider);
            using var service = new WorldRuntimeService(runtime,
                new WorldStateFile(Path.Combine(directory, "world.json")), presence);
            using var lifetime = new CancellationTokenSource();
            presence.RecordAuthenticatedReconnect("owner");
            var harness = ModelWorkHarness.For(runtime);
            var tick = service.TryAdvanceOnceAsync(lifetime.Token).AsTask();
            // This deterministic provider belongs to the uncommitted tick itself.
            var completion = ModelWorkHarness.WaitForExistingAsync(runtime,
                item => item.Transition.Kind == ModelWorkKind.Decision, tick);
            try
            {
                await harness.WaitAsync(Task.WhenAny(provider.Started.Task, completion));
                Assert.True(provider.Started.Task.IsCompleted, "The tick finished before provider entry.");
                Assert.False(completion.IsCompleted);
                if (manualPause)
                {
                    runtime.Pause();
                }
                else
                {
                    clock.Advance(TimeSpan.FromSeconds(5));
                }
                var expected = WorldRuntimeCodec.Encode(runtime.ExportState());
                await harness.WaitAsync(provider.Cancelled.Task);
                Assert.True(provider.ReceivedCancellation.IsCancellationRequested);
                if (deferCancellation)
                {
                    Assert.False(tick.IsCompleted);
                    Assert.False(completion.IsCompleted);
                    Assert.Equal(expected, WorldRuntimeCodec.Encode(runtime.ExportState()));
                    provider.CompleteCancellation();
                }
                await completion;
                Assert.False(await tick);
                Assert.Equal(0, runtime.WorldTick);
                Assert.Equal(expected, WorldRuntimeCodec.Encode(runtime.ExportState()));
                Assert.Equal(manualPause, runtime.Society.IsPaused);
            }
            finally
            {
                lifetime.Cancel();
                provider.CompleteCancellation();
                try { await completion; }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private sealed class WaitingProvider(bool deferCancellation) : IDecisionProvider, IDisposable
    {
        private readonly InlineCompletionSource<CognitionDecisionResponse> response = new();
        private readonly ConcurrentBag<CancellationTokenRegistration> registrations = [];
        public InlineCompletionSource<bool> Started { get; } = new();
        public InlineCompletionSource<bool> Cancelled { get; } = new();
        public CancellationToken ReceivedCancellation { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            ReceivedCancellation = cancellationToken;
            registrations.Add(cancellationToken.Register(() =>
            {
                Cancelled.TrySetResult(true);
                if (!deferCancellation) CompleteCancellation();
            }));
            Started.TrySetResult(true);
            return new ValueTask<CognitionDecisionResponse>(response.Task);
        }

        public void CompleteCancellation()
        {
            if (ReceivedCancellation.IsCancellationRequested)
                response.TrySetCanceled(ReceivedCancellation);
        }

        public void Dispose()
        {
            foreach (var registration in registrations) registration.Dispose();
        }
    }

    [Fact]
    public void AuthenticatedReconnectLeaseExpiresAndSupportsMultipleDevices()
    {
        var clock = new ManualTimeProvider();
        var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(5), clock);

        Assert.False(presence.HasActiveClient);

        presence.RecordAuthenticatedReconnect("device-one");
        Assert.True(presence.HasActiveClient);
        Assert.Equal(1, presence.ActiveClientCount);

        clock.Advance(TimeSpan.FromSeconds(3));
        presence.RecordAuthenticatedReconnect("device-two");
        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.True(presence.HasActiveClient);
        Assert.Equal(1, presence.ActiveClientCount);

        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.False(presence.HasActiveClient);
        Assert.Equal(0, presence.ActiveClientCount);
    }

    [Fact]
    public async Task WorldAdvancesOnlyDuringPresenceAndStillHonorsManualPause()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"clankerworld-client-presence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var clock = new ManualTimeProvider();
            var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(5), clock);
            using var runtime = new WorldRuntime("client-presence-seed");
            var stateFile = new WorldStateFile(Path.Combine(directory, "world.json"));
            var logger = new RecordingLogger<WorldRuntimeService>();
            var service = new WorldRuntimeService(runtime, stateFile, presence, logger);

            Assert.False(await service.AdvanceWithModelWorkAsync(runtime));
            Assert.Equal(0, runtime.WorldTick);

            presence.RecordAuthenticatedReconnect("device-one");
            Assert.True(await service.AdvanceWithModelWorkAsync(runtime));
            Assert.Equal(1, runtime.WorldTick);

            runtime.Pause();
            Assert.False(await service.AdvanceWithModelWorkAsync(runtime));
            Assert.Equal(1, runtime.WorldTick);
            Assert.Contains(logger.Messages, message =>
                message.Contains("world_tick_gate state=waiting_for_client", StringComparison.Ordinal));
            Assert.Contains(logger.Messages, message =>
                message.Contains("world_tick_gate state=advancing", StringComparison.Ordinal));
            Assert.Contains(logger.Messages, message =>
                message.Contains("world_tick_gate state=paused", StringComparison.Ordinal));
            Assert.Contains(logger.Messages, message =>
                message.Contains("model_decision tick=1", StringComparison.Ordinal));

            runtime.Resume();
            for (var tick = 0; tick < 20; tick++)
            {
                Assert.True(await service.AdvanceWithModelWorkAsync(runtime));
            }
            Assert.Contains(logger.Messages, message => message.Contains("project_activity", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message => message.Contains("Cooking fire", StringComparison.Ordinal));
            var tickBeforeDisconnect = runtime.WorldTick;
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.False(await service.AdvanceWithModelWorkAsync(runtime));
            Assert.Equal(tickBeforeDisconnect, runtime.WorldTick);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(WorldTickFailurePolicy.Strict)]
    [InlineData(WorldTickFailurePolicy.RefuseSingleAction)]
    public async Task FailedRecoveryWriteRetainsTickPausesAndRetriesWithoutAdvancing(WorldTickFailurePolicy failurePolicy)
    {
        var directory = Path.Combine(Path.GetTempPath(), "recovery-secret-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var blocker = Path.Combine(directory, "blocked");
        File.WriteAllText(blocker, "secret-key-must-not-be-logged");
        try
        {
            using var runtime = new WorldRuntime("recovery-hold");
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(5));
            presence.RecordAuthenticatedReconnect("owner");
            var logger = new RecordingLogger<WorldRuntimeService>();
            var file = new WorldStateFile(Path.Combine(blocker, "world.json"));
            using var service = new WorldRuntimeService(runtime, file, presence, logger, failurePolicy: failurePolicy);
            Assert.False(await service.AdvanceWithModelWorkAsync(runtime));
            Assert.Equal(1, runtime.WorldTick);
            Assert.True(runtime.Society.IsPaused);
            runtime.Resume();
            Assert.False(await service.AdvanceWithModelWorkAsync(runtime));
            Assert.Equal(1, runtime.WorldTick);
            Assert.True(runtime.Society.IsPaused);
            Assert.Single(logger.Messages, message => message.Contains("held_for_write", StringComparison.Ordinal));
            File.Delete(blocker);
            Directory.CreateDirectory(blocker);
            Assert.False(await service.AdvanceWithModelWorkAsync(runtime));
            Assert.Equal(1, runtime.WorldTick);
            Assert.True(runtime.Society.IsPaused);
            Assert.True(File.Exists(file.Path));
            Assert.Contains(logger.Messages, message => message.Contains("saved_paused", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message => message.Contains(directory, StringComparison.Ordinal) ||
                message.Contains("secret-key", StringComparison.Ordinal));
            runtime.Resume();
            Assert.True(await service.AdvanceWithModelWorkAsync(runtime));
            Assert.Equal(2, runtime.WorldTick);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan duration) => timestamp = checked(timestamp + duration.Ticks);
    }
}
