using System.Reflection;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed class ActionRefusalTests
{
    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, false, false, true, true)]
    public async Task ActualNativeActionsAreRefusedOnlyInLiveSingleActorRecovery(bool live, bool globalFault, bool multipleFaults, bool ordered, bool idleForOrder)
    {
        var directory = Directory.CreateTempSubdirectory("native-action-refusal-");
        try
        {
            var provider = new ExploringProvider(idleForOrder: idleForOrder);
            using var world = NormalPathWorld.CreateGenerated("native-action-refusal", _ => provider, ticksPerDay: 8);
            var harness = ModelWorkHarness.For(world);
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"), _ => provider);
            var actorId = world.Agents.OrderBy(person => person.AgentId, StringComparer.Ordinal).First().AgentId;
            if (ordered) world.SubmitInstruction(new("refused-native-order", "owner:test", actorId,
                ClankerWorld.Simulation.World.OwnerInstructionKind.Order, "gather berries"));
            file.Save(world);
            var baseline = world.ExportState();
            var before = File.ReadAllBytes(file.Path);
            var actor = baseline.Agents.OrderBy(person => person.AgentId, StringComparer.Ordinal).First();
            var prepares = 0;
            var writes = 0;
            Action Prepare(WorldRuntime proposed)
            {
                prepares++;
                // Controlled defect: actual movement creates an invalid work plan.
                // The ordinary checkpoint codec, rather than a fake failure, rejects it.
                var physical = (AgentStore)typeof(WorldRuntime)
                    .GetField("agentStore", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(proposed)!;
                foreach (var culprit in baseline.Agents.OrderBy(person => person.AgentId, StringComparer.Ordinal)
                    .Take(multipleFaults ? 2 : 1))
                    if (globalFault || physical.Living[culprit.AgentId].Position != culprit.Position)
                        physical.RecordProject(culprit.AgentId, physical.Living[culprit.AgentId],
                            new("explore", "Injected invalid work plan", proposed.WorldTick, "invalid"));
                var write = file.PrepareSave(proposed);
                return () => { writes++; write(); };
            }
            var tick = world.AdvanceOneTickWithCheckpointAsync(Prepare, failurePolicy: live
                ? WorldTickFailurePolicy.RefuseSingleAction : WorldTickFailurePolicy.Strict).AsTask();
            if (!live || globalFault || multipleFaults)
            {
                await Assert.ThrowsAsync<InvalidDataException>(() => tick);
                Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
                Assert.Equal(before, File.ReadAllBytes(file.Path));
                Assert.Equal(0, writes);
                if (multipleFaults) Assert.InRange(prepares, 3, 6);
                else Assert.Equal(live ? 2 : 1, prepares);
                return;
            }
            var result = await tick;
            Assert.True(result.Advanced);
            Assert.Equal(1, writes);
            Assert.InRange(prepares, 3, 6);
            var state = world.ExportState();
            Assert.Equal(actor.Position, state.Agents.Single(person => person.AgentId == actor.AgentId).Position);
            Assert.All(state.Agents.Where(person => person.AgentId != actor.AgentId), person =>
                Assert.NotEqual(baseline.Agents.Single(original => original.AgentId == person.AgentId).Position, person.Position));
            var refusal = Assert.Single(state.ActionRefusals);
            Assert.Equal(actor.AgentId, refusal.AgentId);
            if (ordered)
            {
                Assert.True(refusal.CandidateId is "seek_food" or "harvest_food" or "explore", refusal.CandidateId);
                var order = Assert.Single(state.Instructions!).Task!;
                Assert.Equal("cancelled", order.Status);
                Assert.Equal("Cancelled because this choice caused a game error.", order.BlockedReason);
                Assert.Single(state.CompletedInstructionIds!);
            }
            else Assert.Equal("explore", refusal.CandidateId);
            Assert.Equal(1, state.RefusedActionCount);
            Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(state with { ActionRefusals = [] }));
            Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(state with { RefusedActionCount = 0 }));
            Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(state with
            {
                ActionRefusals = [refusal with { CandidateId = "consume_food" }],
            }));
            Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(state with
            {
                ActionRefusals = [refusal with { RetryAfterTick = refusal.RetryAfterTick + 1 }],
            }));
            Assert.Equal(state.WorldSystems!.Config.TicksPerDay, refusal.RetryAfterTick - refusal.RefusedTick);
            var refusalEvent = Assert.Single(result.Events, item => item.Kind == "action_refused");
            Assert.Equal(refusal, JsonSerializer.Deserialize<WorldActionRefusal>(refusalEvent.Detail));
            Assert.Equal(4, provider.Calls);
            Assert.All(provider.Observations.Where(observation => !ordered || observation.AgentId != actorId),
                observation => Assert.Contains(observation.Candidates, candidate => candidate.Id == "explore"));
            var projection = new OwnerWorldObservationStore(world).GetSnapshot();
            Assert.Equal(1, projection.RefusedActionCount);
            var snapshot = JsonSerializer.Deserialize<ClankerWorld.Contracts.OwnerWorldSnapshot>(JsonSerializer.Serialize(projection))!;
            var name = snapshot.Agents.Single(person => person.Id == actorId).DisplayName;
            Assert.StartsWith(name + " couldn't ", EventTextFixture.Describe(
                new(refusalEvent.EventId, refusalEvent.WorldTick, refusalEvent.Kind, refusalEvent.Detail), snapshot));
            using var replay = WorldRuntime.Restore(baseline, _ => new ExploringProvider(idleForOrder: idleForOrder));
            Assert.True((await replay.AdvanceOneTickReplayingActionRefusalAsync(refusalEvent, _ => () => { })).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(state), WorldRuntimeCodec.Encode(replay.ExportState()));
            using var reloaded = file.LoadOrCreate(state.WorldSeed);
            Assert.Equal(WorldRuntimeCodec.Encode(state), WorldRuntimeCodec.Encode(reloaded.ExportState()));
            Assert.True((await world.AdvanceOneTickWithCheckpointAsync(file.PrepareSave)).Advanced);
            Assert.True((await reloaded.AdvanceOneTickWithCheckpointAsync(_ => () => { })).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
            while (!ordered && world.WorldTick < refusal.RetryAfterTick)
            {
                var offeredBefore = provider.Observations.Count;
                world.SubmitInstruction(new($"cooldown-{world.WorldTick}", "owner:test", actor.AgentId,
                    ClankerWorld.Simulation.World.OwnerInstructionKind.Suggestion, "Consider exploring again."));
                Assert.True((await world.AdvanceOneTickWithCheckpointAsync(file.PrepareSave)).Advanced);
                var offered = Assert.Single(provider.Observations.Skip(offeredBefore), observation => observation.AgentId == actor.AgentId);
                if (world.WorldTick < refusal.RetryAfterTick)
                    Assert.DoesNotContain(offered.Candidates, candidate => candidate.Id == refusal.CandidateId);
                else Assert.Contains(offered.Candidates, candidate => candidate.Id == refusal.CandidateId);
            }
            if (!ordered) Assert.Empty(world.ExportState().ActionRefusals);
            Assert.Equal(1, world.ExportState().RefusedActionCount);
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
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            var prepares = 0;
            var writes = 0;
            Assert.True((await world.AdvanceOneTickWithCheckpointAsync(proposed =>
            {
                prepares++;
                var write = file.PrepareSave(proposed);
                return () => { writes++; write(); };
            }, failurePolicy: WorldTickFailurePolicy.RefuseSingleAction)).Advanced);
            Assert.Equal(1, prepares);
            Assert.Equal(1, writes);
            Assert.Equal(4, provider.Calls);
            Assert.Empty(world.ExportState().ActionRefusals);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("pause")]
    [InlineData("presence")]
    [InlineData("cancellation")]
    [InlineData("provider")]
    public async Task ChangesDuringCheckpointPreparationDiscardTheNativeProposal(string boundary)
    {
        var directory = Directory.CreateTempSubdirectory("native-action-gate-");
        try
        {
            var provider = new ExploringProvider();
            using var world = NormalPathWorld.CreateGenerated("native-action-refusal", _ => provider);
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            file.Save(world);
            var before = File.ReadAllBytes(file.Path);
            var expected = before;
            var present = true;
            var writes = 0;
            using var cancellation = new CancellationTokenSource();
            var step = world.AdvanceOneTickWithCheckpointAsync(proposed =>
            {
                var write = file.PrepareSave(proposed);
                switch (boundary)
                {
                    case "pause": world.Pause(); expected = WorldRuntimeCodec.Encode(world.ExportState()); break;
                    case "presence": present = false; break;
                    case "cancellation": cancellation.Cancel(); break;
                    case "provider": provider = new ExploringProvider(1); break;
                }
                return () => { writes++; write(); };
            }, () => present, failurePolicy: WorldTickFailurePolicy.RefuseSingleAction,
                cancellationToken: cancellation.Token).AsTask();
            if (boundary == "cancellation") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => step);
            else Assert.False((await step).Advanced);
            Assert.Equal(0, writes);
            Assert.Equal(0, world.WorldTick);
            Assert.Equal(expected, WorldRuntimeCodec.Encode(world.ExportState()));
            Assert.Equal(before, File.ReadAllBytes(file.Path));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task RecoveryAdmitsTheActualReceivedModelRepliesOnceWithoutAnotherProviderCall()
    {
        var directory = Directory.CreateTempSubdirectory("native-hosted-refusal-");
        try
        {
            var provider = new ExploringProvider(usesModel: true);
            using var world = NormalPathWorld.CreateGenerated("native-action-refusal", _ => provider);
            var harness = ModelWorkHarness.For(world);
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"), _ => provider);
            Assert.True((await world.AdvanceOneTickWithCheckpointAsync(file.PrepareSave,
                failurePolicy: WorldTickFailurePolicy.RefuseSingleAction)).Advanced);
            var ids = world.Agents.Select(person => person.AgentId).ToArray();
            await harness.WaitForAllAsync();
            Assert.Equal(4, provider.Calls);
            file.Save(world);
            var actor = world.Agents.OrderBy(person => person.AgentId, StringComparer.Ordinal).First();
            var writes = 0;
            var result = await world.AdvanceOneTickWithCheckpointAsync(proposed =>
            {
                var people = (AgentStore)typeof(WorldRuntime)
                    .GetField("agentStore", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(proposed)!;
                if (people.Living[actor.AgentId].Position != actor.Position)
                    people.RecordProject(actor.AgentId, people.Living[actor.AgentId],
                        new("explore", "Injected invalid work plan", proposed.WorldTick, "invalid"));
                var write = file.PrepareSave(proposed);
                return () => { writes++; write(); };
            }, failurePolicy: WorldTickFailurePolicy.RefuseSingleAction);
            Assert.True(result.Advanced);
            Assert.Equal(4, provider.Calls);
            Assert.Equal(1, writes);
            Assert.Equal(4, result.Events.Count(item => item.Kind == "hosted_decision_completed"));
            Assert.Single(result.Events, item => item.Kind == "action_refused");
            Assert.Equal(actor.Position, world.Agents.Single(person => person.AgentId == actor.AgentId).Position);
            Assert.Equal(1, world.ExportState().RefusedActionCount);
            using var restored = file.LoadOrCreate(world.ExportState().WorldSeed);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(restored.ExportState()));
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed class ExploringProvider(long epoch = 0, bool usesModel = false, bool idleForOrder = false) : IDecisionProvider
    {
        public int Calls { get; private set; }
        public List<string[]> Offered { get; } = [];
        public List<AgentObservation> Observations { get; } = [];
        public DecisionProviderKind Kind => usesModel ? DecisionProviderKind.LargeLanguageModel : DecisionProviderKind.Deterministic;
        public long ProviderEpoch => epoch;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            lock (Observations)
            {
                Calls++;
                Observations.Add(request.Observation);
                Offered.Add(request.Observation.Candidates.Select(candidate => candidate.Id).ToArray());
            }
            var selected = idleForOrder && request.Observation.OperativeOrderInstructionId is not null
                ? request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle")
                : request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id == "explore")
                ?? request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id is "seek_food" or "harvest_food")
                ?? request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected.Id, 1,
                request.Observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == selected.Id ? 1d : 0d)));
        }
    }
}
