using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class QueuedLifeMomentIdentityTests
{
    private const string Actor = NormalPathWorld.Founder1;
    private const string FirstPersonality = "Patient and curious";
    private const string LaterPersonality = "More willing to take chances";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedOrdinaryWorkWaitsForAnActiveLifeMomentThenUsesItsIdentity(bool midlife)
    {
        using var initial = NormalPathWorld.CreateGenerated("queued-life-moment", _ => new ActionCoverageRecorder(chooseIdle: true));
        var state = initial.ExportState();
        var checkpoint = state.Society.Society;
        var age = midlife ? 30 : 20;
        state = state with
        {
            Society = state.Society with
            {
                Society = checkpoint with
                {
                    Agents = checkpoint.Agents.Select(person => person.Id == Actor ? person with
                    {
                        BirthTick = checkpoint.WorldTick - age * (long)checkpoint.Config.TicksPerWorldDay + 1,
                        BirthLifeTick = null,
                        LastLifecycleYearChecked = age - 1,
                        NeedsName = false,
                    } : person).ToArray(),
                    Inventory = checkpoint.Inventory with
                    {
                        Lots = checkpoint.Inventory.Lots.Where(lot => !InventoryContainerRules.IsFood(lot.ItemKind)).ToArray(),
                    },
                },
            },
            Agents = state.Agents.Select(person => person.AgentId == Actor
                ? person with { FullnessBasisPoints = 7_010, IdentityChoicePending = true } : person).ToArray(),
        };
        var provider = new LifeMomentProvider();
        using var world = WorldRuntime.Restore(state, id => id == Actor
            ? provider : new ActionCoverageRecorder(chooseIdle: true));
        try
        {
            Assert.True((await world.TickModelWorkAsync()).Advanced);
            await ModelWorkHarness.For(world).WaitAsync(provider.InitialStarted.Task);
            var original = Assert.Single(provider.InitialRequests);
            Assert.DoesNotContain(original.Candidates, candidate => candidate.Id is "seek_food" or "harvest_food");
            for (var tick = 0; tick < 2; tick++)
                Assert.True((await world.TickModelWorkAsync()).Advanced);
            Assert.Contains(Assert.Single(world.ExportState().Society.Cognition.Queue,
                entry => entry.AgentId == Actor).Observation.Candidates,
                candidate => candidate.Id is "seek_food" or "harvest_food");
            Assert.Empty(provider.RoutineRequests);

            provider.ReleaseInitial.TrySetResult();
            for (var tick = 0; tick < 20 && world.Agents.Single(person => person.AgentId == Actor).IdentityChoicePending; tick++)
            {
                Assert.True((await world.TickModelWorkAsync()).Advanced);
                await ModelWorkHarness.For(world).SettleAsync();
            }
            Assert.False(world.Agents.Single(person => person.AgentId == Actor).IdentityChoicePending);
            Assert.Equal(FirstPersonality, world.Agents.Single(person => person.AgentId == Actor).Personality);
            Assert.Empty(provider.RoutineRequests);
            Assert.Single(world.ExportState().Society.Cognition.Queue, entry => entry.AgentId == Actor);

            if (midlife)
            {
                await ModelWorkHarness.For(world).WaitAsync(provider.MomentStarted.Task);
                Assert.Equal("requested", Assert.Single(world.Agents.Single(person => person.AgentId == Actor).IdentityMoments!).Outcome);
                for (var tick = 0; tick < 2; tick++)
                {
                    var step = await world.TickModelWorkAsync();
                    Assert.True(step.Advanced);
                    Assert.DoesNotContain(step.Decisions, decision => decision.AgentId == Actor);
                    Assert.Empty(provider.RoutineRequests);
                    Assert.Single(world.ExportState().Society.Cognition.Queue, entry => entry.AgentId == Actor);
                }
                provider.ReleaseMoment.TrySetResult();
                for (var tick = 0; tick < 20 && world.Agents.Single(person => person.AgentId == Actor).Personality != LaterPersonality; tick++)
                {
                    Assert.True((await world.TickModelWorkAsync()).Advanced);
                    await ModelWorkHarness.For(world).SettleAsync();
                }
                Assert.Equal(LaterPersonality, world.Agents.Single(person => person.AgentId == Actor).Personality);
                Assert.Single(provider.MomentRequests);
            }
            else Assert.Empty(provider.MomentRequests);

            Assert.True((await world.TickModelWorkAsync()).Advanced);
            var fresh = Assert.Single(provider.RoutineRequests);
            Assert.Equal(midlife ? LaterPersonality : FirstPersonality, fresh.Self!.Personality);
            Assert.Equal(world.WorldTick, fresh.WorldTick);
            Assert.DoesNotContain(world.ExportState().Society.Cognition.Queue, entry => entry.AgentId == Actor);
            Assert.Single(provider.InitialRequests);
            world.Validate();
        }
        finally
        {
            provider.ReleaseInitial.TrySetResult();
            provider.ReleaseMoment.TrySetResult();
        }
    }

    private sealed class LifeMomentProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ConcurrentQueue<AgentObservation> InitialRequests { get; } = new();
        public ConcurrentQueue<AgentObservation> MomentRequests { get; } = new();
        public ConcurrentQueue<AgentObservation> RoutineRequests { get; } = new();
        public InlineCompletionSource InitialStarted { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource MomentStarted { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource ReleaseInitial { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource ReleaseMoment { get; } = new(TaskCreationOptions.None);

        public DecisionProviderKind KindFor(AgentObservation observation) =>
            observation.NeedsPersonality || observation.NeedsAspiration
                ? DecisionProviderKind.LargeLanguageModel : DecisionProviderKind.Deterministic;

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            string? personality = null;
            var selected = "safe_idle";
            if (observation.IdentityMoment is not null)
            {
                MomentRequests.Enqueue(observation);
                MomentStarted.TrySetResult();
                await ReleaseMoment.Task.WaitAsync(cancellationToken);
                personality = LaterPersonality;
                selected = "identity_optional";
            }
            else if (KindFor(observation) == DecisionProviderKind.LargeLanguageModel)
            {
                InitialRequests.Enqueue(observation);
                InitialStarted.TrySetResult();
                await ReleaseInitial.Task.WaitAsync(cancellationToken);
                personality = FirstPersonality;
            }
            else RoutineRequests.Enqueue(observation);
            return new(request.RequestId, observation.AgentId, KindFor(observation), ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, selected, 1,
                observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == selected ? 1d : 0d),
                ChosenPersonality: personality, ChosenAspiration: personality is null ? null : "Explore the riverbanks");
        }
    }
}
