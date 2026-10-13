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
    public async Task CompletingModelIdentityDefersRetainedDeterministicWorkUntilAFreshTick(bool deferReturn)
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
            Assert.True((await world.TickModelWorkAsync()).Advanced);
            await ModelWorkHarness.For(world).WaitAsync(provider.Started.Task);
            var original = Assert.Single(provider.ModelRequests);
            Assert.True(original.NeedsPersonality);
            Assert.True(original.NeedsAspiration);
            Assert.DoesNotContain(original.Candidates, candidate => candidate.Id is "seek_food" or "harvest_food");

            for (var tick = 0; tick < 6; tick++)
                Assert.True((await world.TickModelWorkAsync()).Advanced);
            Assert.Contains(Assert.Single(world.ExportState().Society.Cognition.Queue,
                entry => entry.AgentId == Actor).Observation.Candidates,
                candidate => candidate.Id is "seek_food" or "harvest_food");
            Assert.Empty(provider.RoutineRequests);

            var beforeCompletion = WorldRuntimeCodec.Encode(world.ExportState());
            provider.Release.TrySetResult(true);
            var completion = WaitForModelCompletion(world);
            if (deferReturn)
            {
                await ModelWorkHarness.For(world).WaitAsync(provider.Replied.Task);
                Assert.False(completion.IsCompleted);
                Assert.False(Assert.Single(world.PendingModelWork, item => item.Transition.ActorId == Actor).Completion.IsCompleted);
                Assert.Equal(beforeCompletion, WorldRuntimeCodec.Encode(world.ExportState()));
                provider.Complete.TrySetResult(true);
            }
            await completion;
            Assert.Equal(beforeCompletion, WorldRuntimeCodec.Encode(world.ExportState()));
            using var deadline = new CancellationTokenSource();
            var admitted = false;
            for (var tick = 0; tick < 20 && !admitted; tick++)
            {
                var step = await world.TickModelWorkAsync(cancellationToken: deadline.Token);
                Assert.True(step.Advanced);
                var decisions = step.Decisions.Where(item => item.AgentId == Actor).ToArray();
                if (decisions.Length == 0)
                {
                    await ModelWorkHarness.For(world).SettleAsync();
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
            var (firstStep, replayStep) = await ModelWorkHarness.For(world).AdvanceTogetherAsync(ModelWorkHarness.For(restored));
            foreach (var (current, step) in new[] { (world, firstStep), (restored, replayStep) })
            {
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
            Assert.Single(provider.ModelRequests);
            Assert.Empty(restoredProvider.ModelRequests);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()),
                WorldRuntimeCodec.Encode(restored.ExportState()));
        }
        finally
        {
            provider.Release.TrySetResult(true);
            provider.Complete.TrySetResult(true);
        }
    }

    private static Task WaitForModelCompletion(WorldRuntime world) =>
        Assert.Single(world.PendingModelWork, item => item.Transition.ActorId == Actor).Completion;

    private static WorldRuntime Restore(WorldRuntimeState state, IdentityPlanningProvider provider) =>
        WorldRuntime.Restore(state, id => id == Actor ? provider : new QuietProvider());

    // Exercise the supported per-observation routing contract: personal identity
    // needs a hosted planner, while subsequent ordinary decisions remain local.
    private sealed class IdentityPlanningProvider(bool deferReturn = false) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ConcurrentQueue<AgentObservation> ModelRequests { get; } = new();
        public ConcurrentQueue<AgentObservation> RoutineRequests { get; } = new();
        public InlineCompletionSource<bool> Started { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> Release { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> Replied { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> Complete { get; } = new(TaskCreationOptions.None);

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
            ModelRequests.Enqueue(request.Observation);
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
