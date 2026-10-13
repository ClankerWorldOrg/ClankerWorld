using ClankerWorld.Simulation;
using System.Text.Json.Nodes;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class PrivateMemorySummaryTests
{
    [Theory]
    [InlineData("control\u0000source")]
    [InlineData(" padded-source ")]
    public async Task ExistingSourceSpellingSurvivesNativeSummaryAdmissionAndReload(string key)
    {
        var state = await ArchivedState();
        var owner = state.Agents[0].AgentId;
        var original = state.Society.Society.ArchivedMemories.First(item => item.Memory.OwnerId == owner);
        var memories = state.Society.Society.ArchivedMemories.Select(item => item == original
            ? item with { Memory = item.Memory with { Id = Identity.ParseStored(key) } } : item)
            .OrderBy(item => item.Memory.Id.Value, StringComparer.Ordinal).ToArray();
        state = state with
        {
            JevEnabled = true,
            RoutineHelper = new("jev", "jev-1.13.0"),
            Society = state.Society with { Society = state.Society.Society with { ArchivedMemories = memories } },
        };
        using var world = WorldRuntime.Restore(state,
            id => new SummaryProvider(id == owner ? DecisionProviderKind.Jev : DecisionProviderKind.Deterministic));
        for (var tick = 0; tick < 8 && world.Society.MemorySummaries.Count == 0; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var summary = Assert.Single(world.Society.MemorySummaries);
        Assert.Contains(summary.Sources, source => source.Id.Value == key);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var loaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(loaded.ExportState()));
        Assert.Equal(summary.Sources, Assert.Single(loaded.Society.MemorySummaries).Sources);
        Assert.Equal(memories, loaded.Society.ArchivedMemories);
    }

    [Theory]
    [InlineData(45)]
    [InlineData(46)]
    [InlineData(47)]
    public async Task AcceptedUnicodeExtractsKeepTheirSourcesAndReloadExactly(int prefixLength)
    {
        var state = await ArchivedState();
        var owner = state.Agents[0].AgentId;
        var original = state.Society.Society.ArchivedMemories.Select(item => item.Memory.OwnerId == owner
            ? item with { Memory = item.Memory with { Summary = new string('a', prefixLength) + "😀" + new string('b', 40) } }
            : item).ToArray();
        state = state with
        {
            JevEnabled = true,
            RoutineHelper = new("jev", "jev-1.13.0"),
            Society = state.Society with { Society = state.Society.Society with { ArchivedMemories = original } }
        };
        using var world = WorldRuntime.Restore(state,
            id => new SummaryProvider(id == owner ? DecisionProviderKind.Jev : DecisionProviderKind.Deterministic));
        for (var tick = 0; tick < 8 && world.Society.MemorySummaries.Count == 0; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var summary = Assert.Single(world.Society.MemorySummaries);
        _ = new System.Text.UTF8Encoding(false, true).GetBytes(summary.Text);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var loaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        var loadedSummary = Assert.Single(loaded.Society.MemorySummaries);
        Assert.Equal((summary.Id, summary.OwnerId, summary.Choice, summary.Text, summary.CreatedTick),
            (loadedSummary.Id, loadedSummary.OwnerId, loadedSummary.Choice, loadedSummary.Text, loadedSummary.CreatedTick));
        Assert.Equal(summary.Sources, loadedSummary.Sources);
        Assert.Equal(original, loaded.Society.ArchivedMemories);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(loaded.ExportState()));
    }

    [Fact]
    public void LongOwnerSummaryCanEnterTheActualCognitionSchedulerAfterReload()
    {
        var owner = "descendant:" + new string('a', 140);
        var config = new SocietyConfig { TicksPerWorldDay = 1, BaseNaturalMortalityBasisPoints = 0 };
        var society = SocietyRules.CreateGenesis("long-summary", [SocietyRules.CreateFounder(owner, "Aster Vale", config: config)], config: config);
        for (var index = 0; index < 4; index++)
            society = SocietyRules.RecordSocialMemory(society, new(Identity.Parse("old-" + index), owner, owner,
                "I remember a long walk along the old familiar path and the weather that afternoon " + index, "private", 0)).Checkpoint;
        society = SocietyRules.AdvanceTo(SocietyRules.Resume(society).Checkpoint, 4).Checkpoint;
        society = SocietyMemoryArchiveRules.Archive(society, new HashSet<string>(), new HashSet<string>());
        society = SocietyRules.RecordAgentMemorySummary(society, owner, SocietyMemorySummaryRules.Options(society, owner)[0]);
        society = SocietyCheckpointCodec.Decode(SocietyCheckpointCodec.Encode(society));
        var candidates = new CognitionCandidate[] { new("safe_idle", "Remember the familiar path.", 0) };
        var memories = WorldMemoryRetrieval.Retrieve(society.Memories, society.Beliefs ?? [], null, owner, 4, candidates, society);
        var summary = Assert.Single(memories, memory => memory.Kind == "summary");
        Assert.Equal(owner, summary.OwnerId);
        Assert.InRange(summary.SubjectId.Length, 1, 128);
        Assert.All(summary.SummarySources!, source => Assert.Equal(summary.SubjectId, source.SubjectId));
        var observation = new AgentObservation(owner, 4, 0, 1, "sha256:long-summary", 10_000, candidates) { RetrievedMemories = memories };
        var scheduler = new SocietyCognitionScheduler(society.Agents);
        Assert.True(scheduler.Enqueue(new("long-summary-recall", owner, 1, 4, ["memory-recall"], observation)));
        scheduler.Validate();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void StandaloneSummaryFormatsRefuseTheirPredecessorAndMissingSummaryLedger(bool runtime, bool missing)
    {
        var society = SocietyRules.CreateGenesis("summary-format", [SocietyRules.CreateFounder("agent", "Aster Vale")]);
        using var world = new SocietyRuntime(society);
        var bytes = runtime ? SocietyRuntimeCodec.Encode(world.ExportState()) : SocietyCheckpointCodec.Encode(society);
        var document = JsonNode.Parse(bytes)!;
        if (missing)
        {
            var checkpoint = runtime ? document["state"]!["society"]! : document["checkpoint"]!;
            Assert.True(checkpoint.AsObject().Remove("memorySummaries"));
        }
        else document["format"] = runtime ? "clankerworld.society-runtime/v3" : "clankerworld.society/v3";
        var invalid = System.Text.Encoding.UTF8.GetBytes(document.ToJsonString());
        var error = Record.Exception(() =>
        {
            if (runtime) SocietyRuntimeCodec.Decode(invalid);
            else SocietyCheckpointCodec.Decode(invalid);
        });
        Assert.True(error is System.Text.Json.JsonException or InvalidDataException,
            error?.ToString() ?? "The previous envelope and missing summary ledger must both be refused.");
        Assert.Equal(bytes, runtime ? SocietyRuntimeCodec.Encode(SocietyRuntimeCodec.Decode(bytes))
            : SocietyCheckpointCodec.Encode(SocietyCheckpointCodec.Decode(bytes)));
    }

    [Theory]
    [InlineData(DecisionProviderKind.Jev)]
    [InlineData(DecisionProviderKind.OpenAiDecisions)]
    public async Task AcceptedHelperSummarizesOnlyOwnArchiveAndRecallPreservesEvidenceAcrossReplay(DecisionProviderKind kind)
    {
        var state = await ArchivedState();
        var owner = state.Agents[0].AgentId;
        var other = state.Agents[1].AgentId;
        var observed = new List<AgentObservation>();
        using var world = WorldRuntime.Restore(state with { JevEnabled = true, RoutineHelper = new("jev", "jev-1.13.0") },
            id => id == owner ? new SummaryProvider(kind, observed) : new SummaryProvider(DecisionProviderKind.Deterministic, observed));
        for (var tick = 0; tick < 8 && world.Society.MemorySummaries.Count == 0; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var summary = Assert.Single(world.Society.MemorySummaries);
        Assert.Equal(owner, summary.OwnerId);
        Assert.Equal(5, summary.Sources.Count);
        Assert.DoesNotContain(summary.Sources, source => source.Id == "foreign");
        Assert.Contains("Hearsay:", summary.Text, StringComparison.Ordinal);
        Assert.True(summary.Text.Length < state.Society.Society.ArchivedMemories.Where(item => item.Memory.OwnerId == owner)
            .Sum(item => item.Memory.Summary.Length) + state.Society.Society.ArchivedBeliefs.Sum(item => item.Belief.Statement.Length));
        Assert.Equal(state.Society.Society.ArchivedMemories, world.Society.ArchivedMemories);
        Assert.Equal(state.Society.Society.ArchivedBeliefs, world.Society.ArchivedBeliefs);
        Assert.All(observed.Where(item => item.AgentId == other), observation => Assert.Empty(observation.MemorySummaryOptions ?? []));
        Assert.DoesNotContain(world.ExportState().Events, item => item.Detail.Contains("secret", StringComparison.Ordinal));
        Assert.Contains(world.Society.Memories, memory => memory.Id == "permanent");

        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        var loadedState = WorldRuntimeCodec.Decode(bytes);
        using var loaded = WorldRuntime.Restore(loadedState);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(loaded.ExportState()));
        var recall = new List<AgentObservation>();
        using var replay = WorldRuntime.Restore(loadedState, _ => new SummaryProvider(DecisionProviderKind.Deterministic, recall));
        using var paired = WorldRuntime.Restore(loadedState, _ => new SummaryProvider(DecisionProviderKind.Deterministic));
        replay.Pause(); paired.Pause();
        replay.SetJevEnabled(false); paired.SetJevEnabled(false);
        replay.Resume(); paired.Resume();
        replay.SubmitInstruction(new("summary-recall", "owner:test", owner, OwnerInstructionKind.Suggestion, "Remember the familiar path."));
        paired.SubmitInstruction(new("summary-recall", "owner:test", owner, OwnerInstructionKind.Suggestion, "Remember the familiar path."));
        for (var tick = 0; tick < 8 && !recall.Any(item => item.AgentId == owner); tick++)
        {
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.True((await paired.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(replay.ExportState()), WorldRuntimeCodec.Encode(paired.ExportState()));
        }
        var memory = Assert.Single(recall.First(item => item.AgentId == owner).RetrievedMemories!, item => item.Kind == "summary");
        var rumor = Assert.Single(memory.SummarySources!, source => source.Kind == "belief");
        Assert.Equal(("hearsay", 4_200, other), (rumor.Provenance, rumor.ConfidenceBasisPoints, rumor.SourceAgentId));
        Assert.Equal(summary.Text, memory.Summary);
        Assert.Single(replay.Society.MemorySummaries);
        var correction = replay.CorrectAgentBelief(owner, rumor.Id,
            new(Identity.Parse("corrected"), owner, "I saw the path was dry.", SocietyBeliefProvenance.Firsthand, 9_000, replay.WorldTick));
        Assert.Equal(rumor.Id, correction.SupersedesBeliefId?.Value);
        var correctedBytes = WorldRuntimeCodec.Encode(replay.ExportState());
        using var corrected = WorldRuntime.Restore(WorldRuntimeCodec.Decode(correctedBytes));
        Assert.Equal(correctedBytes, WorldRuntimeCodec.Encode(corrected.ExportState()));
        recall.Clear();
        replay.SubmitInstruction(new("summary-correction-recall", "owner:test", owner, OwnerInstructionKind.Suggestion, "Remember the path and its correction."));
        for (var tick = 0; tick < 8 && !recall.Any(item => item.AgentId == owner); tick++)
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        var correctedMemory = Assert.Single(recall.First(item => item.AgentId == owner).RetrievedMemories!, item => item.Kind == "summary");
        Assert.True(Assert.Single(correctedMemory.SummarySources!, source => source.Id == rumor.Id).IsCorrected);
        Assert.Equal(summary.Text, correctedMemory.Summary);
    }

    [Fact]
    public async Task HelperOffDiscardsHeldSummaryAndRefusedTickPreservesCheckpoint()
    {
        var state = await ArchivedState();
        var owner = state.Agents[0].AgentId;
        var held = new HeldSummaryProvider();
        using var world = WorldRuntime.Restore(state with { JevEnabled = true, RoutineHelper = new("jev", "jev-1.13.0") },
            id => id == owner ? held : new SummaryProvider(DecisionProviderKind.Deterministic));
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.TickModelWorkAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.True((await world.TickModelWorkAsync()).Advanced);
        var observation = await ModelWorkHarness.For(world).WaitAsync(held.Started.Task);
        Assert.NotEmpty(observation.MemorySummaryOptions!);
        var completion = Assert.Single(world.PendingModelWork, item =>
            item.Transition.Kind == ModelWorkKind.Decision && item.Transition.ActorId == owner).Completion;
        world.Pause();
        Assert.True(world.SetRoutineHelper(RoutineHelperSettings.Off));
        var switched = WorldRuntimeCodec.Encode(world.ExportState());
        held.Release.SetResult(true);
        await ModelWorkHarness.For(world).WaitAsync(held.Returned.Task);
        await ModelWorkHarness.For(world).WaitAsync(completion);
        Assert.False((await world.TickModelWorkAsync()).Advanced);
        Assert.Equal(switched, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Empty(world.Society.MemorySummaries);
    }

    [Theory]
    [InlineData("off")]
    [InlineData("failure")]
    [InlineData("decline")]
    [InlineData("unrequested")]
    public async Task OptionalOrUnusableHelperCannotWriteSummaryOrChangeTheArchive(string mode)
    {
        var state = await ArchivedState();
        var owner = state.Agents[0].AgentId;
        var observed = new List<AgentObservation>();
        using var world = WorldRuntime.Restore(state with { JevEnabled = mode != "off", RoutineHelper = mode == "off" ? RoutineHelperSettings.Off : new("jev", "jev-1.13.0") },
            id => new SummaryProvider(mode == "off" || id != owner ? DecisionProviderKind.Deterministic : DecisionProviderKind.Jev, observed, mode));
        for (var tick = 0; tick < 5; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Empty(world.Society.MemorySummaries);
        Assert.Equal(state.Society.Society.ArchivedMemories, world.Society.ArchivedMemories);
        Assert.Equal(state.Society.Society.ArchivedBeliefs, world.Society.ArchivedBeliefs);
        if (mode == "off") Assert.All(observed, item => Assert.Null(item.MemorySummaryOptions));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("text")]
    [InlineData("tick")]
    [InlineData("duplicate")]
    [InlineData("source")]
    public async Task StrictLoadingRejectsAlteredSummarySourcesWithoutChangingOriginalBytes(string fault)
    {
        var state = await ArchivedState();
        var owner = state.Agents[0].AgentId;
        using var world = WorldRuntime.Restore(state with { JevEnabled = true, RoutineHelper = new("jev", "jev-1.13.0") },
            id => new SummaryProvider(id == owner ? DecisionProviderKind.Jev : DecisionProviderKind.Deterministic));
        for (var tick = 0; tick < 8 && world.Society.MemorySummaries.Count == 0; tick++) await world.AdvanceOneTickAsync();
        Assert.Single(world.Society.MemorySummaries);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        var json = JsonNode.Parse(bytes)!;
        var society = json["state"]!["society"]!["society"]!;
        var summary = society["memorySummaries"]![0]!;
        switch (fault)
        {
            case "missing": society.AsObject().Remove("memorySummaries"); break;
            case "foreign": summary["ownerId"] = state.Agents[1].AgentId; break;
            case "text": summary["text"] = "A made-up authoritative fact."; break;
            case "tick": summary["createdTick"] = world.WorldTick + 1; break;
            case "duplicate": society["memorySummaries"]!.AsArray().Add(summary.DeepClone()); break;
            case "source": summary["sources"]![0]!["id"] = "foreign"; break;
        }
        Assert.ThrowsAny<Exception>(() => WorldRuntimeCodec.Decode(System.Text.Encoding.UTF8.GetBytes(json.ToJsonString())));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
    }

    private static async Task<WorldRuntimeState> ArchivedState()
    {
        using var seed = new WorldRuntime("summary-private");
        var state = seed.ExportState();
        var owner = state.Agents[0].AgentId;
        var other = state.Agents[1].AgentId;
        var config = state.Society.Society.Config with { TicksPerWorldDay = 2, BaseNaturalMortalityBasisPoints = 0 };
        var society = state.Society.Society with
        {
            Config = config,
            WorldTick = 5,
            Inventory = state.Society.Society.Inventory with { WorldTick = 5 },
            Agents = state.Society.Society.Agents.Select(item => item with
            {
                BirthTick = item.BirthTick * 2 / state.Society.Society.Config.TicksPerWorldDay,
                BirthLifeTick = item.BirthLifeTick * 2 / state.Society.Society.Config.TicksPerWorldDay,
            }).ToArray(),
            Memories = Enumerable.Range(0, 4).Select(index => new SocietySocialMemory(Identity.Parse("old-" + index), owner, other,
                "I walked along a familiar path and remembered the weather that afternoon " + index, "private", 0))
                .Append(new(Identity.Parse("foreign"), other, owner, "Another person's secret supply location.", "private", 0))
                .Append(new(Identity.Parse("permanent"), owner, other, "My learned skill remains dependable.", "private", 0) { Kind = SocietyMemoryKind.Skill })
                .OrderBy(item => item.Id.Value, StringComparer.Ordinal).ToArray(),
            Beliefs = [new(Identity.Parse("rumor"), owner, "Someone told me the familiar path was muddy; I have not seen it myself.",
                SocietyBeliefProvenance.Hearsay, 4_200, 0, SourceAgentId: other)],
        };
        var systems = WorldSystemsRules.CreateGenesis(state.WorldSeed, state.WorldSystems!.Config with { TicksPerDay = 2, CalendarOffsetTicks = 0 },
            state.WorldSystems.Ecology.Resources, state.WorldSystems.Chunks);
        for (var tick = 0; tick < 5; tick++) systems = WorldSystemsRules.AdvanceOneTick(systems);
        using var world = WorldRuntime.Restore(state with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Society = state.Society with { Society = society },
            WorldSystems = systems,
            Agents = state.Agents.Select(item => item with { LastDecisionContext = null, FullnessBasisPoints = 10_000 }).ToArray(),
        }, _ => new SummaryProvider(DecisionProviderKind.Deterministic));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(5, world.Society.ArchivedMemories.Count);
        return world.ExportState() with
        {
            Agents = world.ExportState().Agents.Select(item => item with { LastDecisionContext = null }).ToArray(),
        };
    }

    private sealed class HeldSummaryProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Jev;
        public long ProviderEpoch => 1;
        public InlineCompletionSource<AgentObservation> Started { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> Release { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> Returned { get; } = new(TaskCreationOptions.None);
        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(request.Observation);
            await Release.Task;
            Returned.TrySetResult(true);
            return new(request.RequestId, request.Observation.AgentId, Kind, ProviderEpoch, request.Observation.RunEpoch,
                request.Observation.DecisionGeneration, request.Observation.ObservationDigest, "safe_idle", 1,
                new Dictionary<string, double> { ["safe_idle"] = 1 }, MemorySummaryChoice: request.Observation.MemorySummaryOptions![0].Choice);
        }
    }

    private sealed class SummaryProvider(DecisionProviderKind kind, List<AgentObservation>? observations = null, string mode = "accepted") : IDecisionProvider
    {
        public DecisionProviderKind Kind => kind;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            observations?.Add(request.Observation);
            if (mode == "failure" && kind == DecisionProviderKind.Jev) throw new InvalidOperationException("Helper unavailable.");
            const string selected = "safe_idle";
            var choice = mode == "unrequested" ? "unknown" : mode == "accepted" ? request.Observation.MemorySummaryOptions is { Count: > 0 } options ? options[0].Choice : null : null;
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind, ProviderEpoch,
                request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest, selected, 1,
                new Dictionary<string, double> { [selected] = 1 }, MemorySummaryChoice: choice));
        }
    }
}
