using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class IdentityReplyReplayTests
{
    [Fact]
    public async Task CompletedCohortsKeepExactEventOrderAfterSlotReuseAndReload()
    {
        using var initial = NormalPathWorld.CreateGenerated("identity-reply-replay", _ => new DeterministicDecisionProvider());
        var state = initial.ExportState();
        var checkpoint = state.Society.Society;
        var actors = checkpoint.Agents.Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();
        var midlife = checkpoint.Config.DayLifecycle!.MaximumDay / 2;
        state = state with
        {
            Society = state.Society with
            {
                Society = checkpoint with
                {
                    Agents = checkpoint.Agents.Select(person => person with
                    {
                        BirthTick = checkpoint.WorldTick - midlife * (long)checkpoint.Config.TicksPerWorldDay +
                            (Array.IndexOf(actors, person.Id) < 2 ? 1 : 5),
                        BirthLifeTick = null,
                        LastLifecycleYearChecked = midlife - 1,
                        NeedsName = false,
                    }).ToArray(),
                },
            },
            Agents = state.Agents.Select(person => person with { IdentityChoicePending = false }).ToArray(),
        };
        var provider = new IdentityReplies(actors[3]);
        using var world = WorldRuntime.Restore(state, _ => provider);
        var work = ModelWorkHarness.For(world);
        Assert.True((await work.TickAsync()).Advanced);
        await work.WaitForKindAsync(ModelWorkKind.IdentityMoment);
        Assert.Equal(actors.Take(2), work.Pending.Where(item => item.Transition.Kind == ModelWorkKind.IdentityMoment)
            .Select(item => item.Transition.ActorId));
        Assert.True((await work.TickAsync()).Advanced);
        Assert.Equal(2, work.Transitions.Count(item => item.Kind == ModelWorkKind.IdentityMoment && item.Stage == ModelWorkStage.Admitted));
        Assert.DoesNotContain(work.Pending, item => item.Transition.Kind == ModelWorkKind.IdentityMoment);

        // Keep the live dictionary's freed slots; a strict reload has no slot history.
        world.Pause();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        var replayProvider = new IdentityReplies(actors[3]);
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => replayProvider);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(replay.ExportState()));
        var replayWork = ModelWorkHarness.For(replay);
        world.Resume();
        replay.Resume();
        for (var tick = 0; tick < 6; tick++)
        {
            await work.WaitForAllAsync();
            await replayWork.WaitForAllAsync();
            if (work.Pending.Any(item => item.Transition.Kind == ModelWorkKind.IdentityMoment))
            {
                Assert.Equal(actors.Skip(2), work.Pending.Where(item => item.Transition.Kind == ModelWorkKind.IdentityMoment)
                    .Select(item => item.Transition.ActorId));
                var before = WorldRuntimeCodec.Encode(world.ExportState());
                Assert.False((await work.TickAsync(() => false)).Advanced);
                Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
            }
            Assert.True((await work.TickAsync()).Advanced);
            Assert.True((await replayWork.TickAsync()).Advanced);
            Assert.Equal(world.ExportState().Events, replay.ExportState().Events);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.Equal(4, provider.Replies);
        Assert.Equal(2, replayProvider.Replies);
        Assert.Equal(2, replayWork.Transitions.Count(item => item.Kind == ModelWorkKind.IdentityMoment && item.Stage == ModelWorkStage.Admitted));
        foreach (var actor in actors.Skip(2))
        {
            var person = world.Agents.Single(item => item.AgentId == actor);
            var moment = Assert.Single(person.IdentityMoments!);
            Assert.Equal(AgentIdentityMomentKind.Midlife, moment.Kind);
            Assert.Equal(actor == actors[3] ? "accepted" : "kept", moment.Outcome);
            if (actor == actors[3]) Assert.Equal("Patient and curious", person.Personality);
        }
        Assert.Equal(actors, world.ExportState().Events.Where(item => item.Kind is "agent_identity_moment_kept" or "agent_identity_revised")
            .Select(item => item.Payload.ActorId));
        var completed = WorldRuntimeCodec.Encode(world.ExportState());
        using var completedReload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(completed));
        Assert.Equal(completed, WorldRuntimeCodec.Encode(completedReload.ExportState()));
    }

    private sealed class IdentityReplies(string changedActor) : IDecisionProvider
    {
        public int Replies { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public DecisionProviderKind KindFor(AgentObservation observation) => observation.IdentityMoment is null
            ? DecisionProviderKind.Deterministic : Kind;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            var identity = observation.IdentityMoment is not null;
            if (identity) Replies++;
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId,
                KindFor(observation), ProviderEpoch, observation.RunEpoch, observation.DecisionGeneration,
                observation.ObservationDigest, identity ? "identity_optional" : "safe_idle", 1, new Dictionary<string, double>(),
                ChosenPersonality: identity && observation.AgentId == changedActor ? "Patient and curious" : null));
        }
    }
}
