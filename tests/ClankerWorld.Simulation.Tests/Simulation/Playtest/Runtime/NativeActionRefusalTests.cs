using System.Reflection;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Viewer.Observation;

namespace ClankerWorld.Simulation.Tests;

public sealed class NativeActionRefusalTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ActualNativeActionsAreRefusedOnlyInLiveSingleActorRecovery(bool live, bool globalFault)
    {
        var directory = Directory.CreateTempSubdirectory("native-action-refusal-");
        try
        {
            var provider = new ExploringProvider();
            using var world = NormalPathWorld.CreateGenerated("native-action-refusal", _ => provider);
            var file = new PrivateWorldStateFile(Path.Combine(directory.FullName, "world.json"), _ => provider);
            file.Save(world);
            var baseline = world.ExportState();
            var before = File.ReadAllBytes(file.Path);
            var actor = baseline.Inhabitants.OrderBy(person => person.InhabitantId, StringComparer.Ordinal).First();
            var prepares = 0;
            var writes = 0;
            Action Prepare(PrivateWorldRuntime proposed)
            {
                prepares++;
                // Controlled defect: actual movement creates an invalid work plan.
                // The ordinary checkpoint codec, rather than a fake failure, rejects it.
                var physical = (Dictionary<string, PlaytestInhabitantState>)typeof(PrivateWorldRuntime)
                    .GetField("inhabitants", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(proposed)!;
                if (globalFault || physical[actor.InhabitantId].Position != actor.Position)
                    physical[actor.InhabitantId] = physical[actor.InhabitantId] with
                    {
                        Project = new("explore", "Injected invalid work plan", proposed.WorldTick, "invalid"),
                    };
                var write = file.PrepareSave(proposed);
                return () => { writes++; write(); };
            }
            var tick = world.AdvanceOneTickWithCheckpointAsync(Prepare, failurePolicy: live
                ? PrivateWorldTickFailurePolicy.RefuseSingleAction : PrivateWorldTickFailurePolicy.Strict).AsTask();
            if (!live || globalFault)
            {
                await Assert.ThrowsAsync<InvalidDataException>(() => tick);
                Assert.Equal(before, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
                Assert.Equal(before, File.ReadAllBytes(file.Path));
                Assert.Equal(0, writes);
                Assert.Equal(live ? 2 : 1, prepares);
                return;
            }
            var result = await tick;
            Assert.True(result.Advanced);
            Assert.Equal(1, writes);
            Assert.InRange(prepares, 3, 6);
            var state = world.ExportState();
            Assert.Equal(actor.Position, state.Inhabitants.Single(person => person.InhabitantId == actor.InhabitantId).Position);
            Assert.All(state.Inhabitants.Where(person => person.InhabitantId != actor.InhabitantId), person =>
                Assert.NotEqual(baseline.Inhabitants.Single(original => original.InhabitantId == person.InhabitantId).Position, person.Position));
            var refusal = Assert.Single(state.ActionRefusals);
            Assert.Equal(actor.InhabitantId, refusal.InhabitantId);
            Assert.Equal("explore", refusal.CandidateId);
            Assert.Equal(1, state.RefusedActionCount);
            Assert.Equal(state.WorldSystems!.Config.TicksPerDay, refusal.RetryAfterTick - refusal.RefusedTick);
            var refusalEvent = Assert.Single(result.Events, item => item.Kind == "action_refused");
            Assert.Equal(refusal, JsonSerializer.Deserialize<PrivateWorldActionRefusal>(refusalEvent.Detail));
            Assert.Equal(4, provider.Calls);
            Assert.All(provider.Offered, offered => Assert.Contains("explore", offered));
            using var replay = PrivateWorldRuntime.Restore(baseline, _ => new ExploringProvider());
            Assert.True((await replay.AdvanceOneTickReplayingActionRefusalAsync(refusalEvent, _ => () => { })).Advanced);
            Assert.Equal(PrivateWorldRuntimeCodec.Encode(state), PrivateWorldRuntimeCodec.Encode(replay.ExportState()));
            using var reloaded = file.LoadOrCreate(state.WorldSeed);
            Assert.Equal(PrivateWorldRuntimeCodec.Encode(state), PrivateWorldRuntimeCodec.Encode(reloaded.ExportState()));
            Assert.True((await world.AdvanceOneTickWithCheckpointAsync(file.PrepareSave)).Advanced);
            Assert.True((await reloaded.AdvanceOneTickWithCheckpointAsync(_ => () => { })).Advanced);
            Assert.Equal(PrivateWorldRuntimeCodec.Encode(world.ExportState()), PrivateWorldRuntimeCodec.Encode(reloaded.ExportState()));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task HealthyNativeTickPreparesAndWritesOnce()
    {
        var directory = Directory.CreateTempSubdirectory("native-healthy-tick-");
        try
        {
            var provider = new ExploringProvider();
            using var world = NormalPathWorld.CreateGenerated("native-action-refusal", _ => provider);
            var file = new PrivateWorldStateFile(Path.Combine(directory.FullName, "world.json"));
            var prepares = 0;
            var writes = 0;
            Assert.True((await world.AdvanceOneTickWithCheckpointAsync(proposed =>
            {
                prepares++;
                var write = file.PrepareSave(proposed);
                return () => { writes++; write(); };
            }, failurePolicy: PrivateWorldTickFailurePolicy.RefuseSingleAction)).Advanced);
            Assert.Equal(1, prepares);
            Assert.Equal(1, writes);
            Assert.Equal(4, provider.Calls);
            Assert.Empty(world.ExportState().ActionRefusals);
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed class ExploringProvider : IDecisionProvider
    {
        public int Calls { get; private set; }
        public List<string[]> Offered { get; } = [];
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            Offered.Add(request.Observation.Candidates.Select(candidate => candidate.Id).ToArray());
            var selected = request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id == "explore")
                ?? request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.InhabitantId,
                Kind, ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected.Id, 1,
                request.Observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == selected.Id ? 1d : 0d)));
        }
    }
}
