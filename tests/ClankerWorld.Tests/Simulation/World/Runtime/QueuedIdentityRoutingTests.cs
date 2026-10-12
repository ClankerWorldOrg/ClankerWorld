using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class QueuedIdentityRoutingTests
{
    private const string Actor = "founder-scout";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletingHostedIdentityDefersRetainedDeterministicWorkUntilAFreshTick(bool deferReturn)
    {
        using var initial = new WorldRuntime("queued-idle-context");
        var state = initial.ExportState();
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == Actor
                ? person with { FullnessBasisPoints = 7_020, IdentityChoicePending = true } : person).ToArray(),
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Inventory = state.Society.Society.Inventory with
                    {
                        Lots = state.Society.Society.Inventory.Lots
                            .Where(lot => !InventoryContainerRules.IsFood(lot.ItemKind)).ToArray(),
                    },
                },
            },
        };
        var provider = new IdentityPlanningProvider(deferReturn);
        using var world = Restore(state, provider);
        try
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var original = Assert.Single(provider.HostedRequests);
            Assert.True(original.NeedsPersonality);
            Assert.True(original.NeedsAspiration);
            Assert.DoesNotContain(original.Candidates, candidate => candidate.Id is "seek_food" or "harvest_food");

            for (var tick = 0; tick < 6; tick++)
                Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            Assert.Contains(Assert.Single(world.ExportState().Society.Cognition.Queue,
                entry => entry.AgentId == Actor).Observation.Candidates,
                candidate => candidate.Id is "seek_food" or "harvest_food");
            Assert.Empty(provider.RoutineRequests);

            var beforeCompletion = WorldRuntimeCodec.Encode(world.ExportState());
            provider.Release.TrySetResult(true);
            var completion = WaitForHostedCompletion(world);
            if (deferReturn)
            {
                await provider.Replied.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(completion.IsCompleted);
                Assert.False(world.HostedDecisionsFinished(Actor));
                Assert.Equal(beforeCompletion, WorldRuntimeCodec.Encode(world.ExportState()));
                provider.Complete.TrySetResult(true);
            }
            await completion;
            Assert.Equal(beforeCompletion, WorldRuntimeCodec.Encode(world.ExportState()));
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var admitted = false;
            for (var tick = 0; tick < 20 && !admitted; tick++)
            {
                var step = await world.AdvanceOneTickNonBlockingAsync(cancellationToken: deadline.Token);
                Assert.True(step.Advanced);
                var decisions = step.Decisions.Where(item => item.AgentId == Actor).ToArray();
                if (decisions.Length == 0)
                {
                    await Task.Delay(5, deadline.Token);
                    continue;
                }
                var decision = Assert.Single(decisions);
                Assert.True(decision.Admission.Accepted);
                Assert.Equal(DecisionProviderKind.LargeLanguageModel, decision.Admission.Intention!.Provider);
                admitted = true;
            }
            Assert.True(admitted, "The held identity reply was not admitted within the bounded wait.");
            Assert.Empty(provider.RoutineRequests);
            Assert.False(world.Agents.Single(person => person.AgentId == Actor).IdentityChoicePending);
            Assert.Single(world.ExportState().Society.Cognition.Queue, entry => entry.AgentId == Actor);

            var admittedTick = world.WorldTick;
            var saved = WorldRuntimeCodec.Encode(world.ExportState());
            var restoredProvider = new IdentityPlanningProvider();
            using var restored = Restore(WorldRuntimeCodec.Decode(saved), restoredProvider);
            Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
            foreach (var current in new[] { world, restored })
            {
                var step = await current.AdvanceOneTickNonBlockingAsync();
                var decision = Assert.Single(step.Decisions, item => item.AgentId == Actor);
                Assert.True(decision.Admission.Accepted);
                Assert.Equal(DecisionProviderKind.Deterministic, decision.Admission.Intention!.Provider);
                Assert.DoesNotContain(current.ExportState().Society.Cognition.Queue, entry => entry.AgentId == Actor);
                current.Validate();
            }
            foreach (var currentProvider in new[] { provider, restoredProvider })
            {
                var fresh = Assert.Single(currentProvider.RoutineRequests);
                Assert.Equal(admittedTick + 1, fresh.WorldTick);
                Assert.False(fresh.NeedsPersonality);
                Assert.False(fresh.NeedsAspiration);
                Assert.Equal("Patient and curious", fresh.Self!.Personality);
                Assert.Equal("Explore the riverbanks", fresh.Self.Aspiration);
                Assert.Contains(fresh.Candidates, candidate => candidate.Id is "seek_food" or "harvest_food");
            }
            Assert.Single(provider.HostedRequests);
            Assert.Empty(restoredProvider.HostedRequests);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()),
                WorldRuntimeCodec.Encode(restored.ExportState()));
        }
        finally
        {
            provider.Release.TrySetResult(true);
            provider.Complete.TrySetResult(true);
        }
    }

    private static async Task WaitForHostedCompletion(WorldRuntime world)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!world.HostedDecisionsFinished(Actor)) await Task.Delay(1, deadline.Token);
    }

    private static WorldRuntime Restore(WorldRuntimeState state, IdentityPlanningProvider provider) =>
        WorldRuntime.Restore(state, id => id == Actor ? provider : new QuietProvider());

    // Exercise the supported per-observation routing contract: personal identity
    // needs a hosted planner, while subsequent ordinary decisions remain local.
    private sealed class IdentityPlanningProvider(bool deferReturn = false) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ConcurrentQueue<AgentObservation> HostedRequests { get; } = new();
        public ConcurrentQueue<AgentObservation> RoutineRequests { get; } = new();
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Replied { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Complete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public DecisionProviderKind KindFor(AgentObservation observation) =>
            observation.NeedsPersonality || observation.NeedsAspiration
                ? DecisionProviderKind.LargeLanguageModel : DecisionProviderKind.Deterministic;

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var kind = KindFor(request.Observation);
            if (kind == DecisionProviderKind.Deterministic)
            {
                RoutineRequests.Enqueue(request.Observation);
                return Reply(request, kind, ProviderEpoch);
            }
            HostedRequests.Enqueue(request.Observation);
            Started.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
            var reply = Reply(request, kind, ProviderEpoch) with
            {
                ChosenPersonality = "Patient and curious",
                ChosenAspiration = "Explore the riverbanks",
            };
            Replied.TrySetResult(true);
            if (deferReturn) await Complete.Task.WaitAsync(cancellationToken);
            return reply;
        }
    }

    private sealed class QuietProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(Reply(request, Kind, ProviderEpoch));
    }

    private static CognitionDecisionResponse Reply(CognitionDecisionRequest request, DecisionProviderKind kind, long epoch) =>
        new(request.RequestId, request.Observation.AgentId, kind, epoch, request.Observation.RunEpoch,
            request.Observation.DecisionGeneration, request.Observation.ObservationDigest, "safe_idle", 1,
            request.Observation.Candidates.ToDictionary(candidate => candidate.Id,
                candidate => candidate.Id == "safe_idle" ? 1d : 0d));
}
