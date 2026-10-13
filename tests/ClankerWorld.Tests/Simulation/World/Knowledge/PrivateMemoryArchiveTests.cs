using ClankerWorld.Simulation;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class PrivateMemoryArchiveTests
{
    [Fact]
    public async Task NativeRenameRemainsLiveAcrossTheDailyArchiveBoundaryAndReload()
    {
        using var renamed = Restore(WorldRuntimeCodec.Encode(NearBoundary(generated: true)));
        var owner = renamed.Society.Agents[0].Id;
        Assert.True(renamed.RenameAgent(owner, "Archiveproof Vale"));
        var original = Assert.Single(renamed.Society.Memories, item => item.Id.Value.StartsWith("player-rename:", StringComparison.Ordinal));
        Assert.True(original.Permanent);
        Assert.Equal((owner, owner, 5L, SocietyMemoryKind.Experience), (original.OwnerId, original.SubjectId, original.SourceTick, original.Kind));
        var ordinary = new SocietySocialMemory(Identity.Parse("ordinary-rename-age-control"), owner, owner, "An ordinary private experience.", "private", original.SourceTick);
        var state = renamed.ExportState();
        state = WithSources(state, state.Society.Society.Memories.Append(ordinary).ToArray(), state.Society.Society.Beliefs ?? []);
        using var world = Restore(WorldRuntimeCodec.Encode(state));
        for (var tick = 0; tick < 6; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(11, world.WorldTick);
        Assert.Contains(ordinary, world.Society.Memories);
        Assert.Contains(original, world.Society.Memories);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = Restore(bytes);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(12, world.WorldTick);
        Assert.Contains(world.Society.ArchivedMemories, item => item.Memory == ordinary && item.ArchivedTick == 12);
        Assert.DoesNotContain(world.Society.Memories, item => item.Id == ordinary.Id);
        Assert.Contains(original, world.Society.Memories);
        Assert.DoesNotContain(world.Society.ArchivedMemories, item => item.Memory.Id == original.Id);
        var after = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.Equal(after, WorldRuntimeCodec.Encode(replay.ExportState()));
        using var loaded = Restore(after);
        Assert.Equal(after, WorldRuntimeCodec.Encode(loaded.ExportState()));
        Assert.Contains(original, loaded.Society.Memories);
        var observations = new List<AgentObservation>();
        using var recalling = WorldRuntime.Restore(WorldRuntimeCodec.Decode(after), _ => new IdleProvider(observations));
        recalling.SubmitInstruction(new("recall-rename", "owner:test", owner, OwnerInstructionKind.Suggestion, "Remember my accepted name."));
        for (var tick = 0; tick < 4 && observations.All(item => item.AgentId != owner); tick++)
            Assert.True((await recalling.AdvanceOneTickAsync()).Advanced);
        var observed = Assert.Single(observations.Where(item => item.AgentId == owner).Take(1));
        Assert.Contains(observed.RetrievedMemories ?? [], item => item.Id == original.Id && item.Summary == original.Summary);
        Assert.DoesNotContain(observed.RetrievedMemories ?? [], item => item.Id == ordinary.Id);
        Assert.All(observations.Where(item => item.AgentId != owner), item =>
            Assert.DoesNotContain(item.RetrievedMemories ?? [], memory => memory.Id == original.Id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StrictLoadingRefusesAnArchivedPlayerRenameWithEitherPermanentMarker(bool permanent)
    {
        using var world = Restore(WorldRuntimeCodec.Encode(NearBoundary(generated: true)));
        Assert.True(world.RenameAgent(world.Society.Agents[0].Id, "Archiveproof Vale"));
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        var json = JsonNode.Parse(bytes)!;
        var society = json["state"]!["society"]!["society"]!;
        var live = society["memories"]!.AsArray();
        var memory = live.Single(item => item!["id"]!.GetValue<string>().StartsWith("player-rename:", StringComparison.Ordinal))!;
        var archived = memory.DeepClone();
        Assert.True(live.Remove(memory));
        archived["permanent"] = permanent;
        society["archivedMemories"]!.AsArray().Add(new JsonObject { ["memory"] = archived, ["archivedTick"] = world.WorldTick });
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(System.Text.Encoding.UTF8.GetBytes(json.ToJsonString())));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
    }

    [Fact]
    public async Task DailyNativeRuleWorksWithoutHelpersAndPreservesProtectedRecordsAcrossReplay()
    {
        var state = NearBoundary(generated: true);
        var owner = state.Agents[0].AgentId;
        var other = state.Agents[1].AgentId;
        var memories = new List<SocietySocialMemory>
        {
            new(Identity.Parse("old-low"), owner, other, "An old private conversation.", "private", 0),
            new(Identity.Parse("old-cutoff"), owner, other, "A useful remembered path.", "private", 0),
            new(Identity.Parse("recent"), owner, other, "A recent ordinary experience.", "private", 4),
            new(Identity.Parse("foreign"), other, owner, "Someone else's hidden location.", "private", 0),
        };
        foreach (var kind in new[] { SocietyMemoryKind.LifeEvent, SocietyMemoryKind.Relationship, SocietyMemoryKind.Skill, SocietyMemoryKind.Commitment })
            memories.Add(new(Identity.Parse("protected-" + kind), owner, other, "Permanent " + kind, "private", 0) { Kind = kind });
        var rumor = new SocietyAgentBelief(Identity.Parse("old-rumor"), owner, "Someone said the old path was muddy.",
            SocietyBeliefProvenance.Hearsay, 4_200, 0, SourceAgentId: other);
        var protectedBeliefs = memories.Where(item => item.Kind != SocietyMemoryKind.Experience).Select(item =>
            new SocietyAgentBelief(Identity.Parse("belief-" + item.Kind), owner, item.Summary, SocietyBeliefProvenance.Firsthand, 7_000, 0) { Kind = item.Kind }).ToArray();
        state = WithSources(state, memories, protectedBeliefs.Append(rumor).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(), [new(owner,
            [new(Identity.Parse("old-cutoff"), SocietyMemorySourceKind.Experience, 0, 2_500, 8_000, 0)])]);
        var bytes = WorldRuntimeCodec.Encode(state);
        using var world = Restore(bytes);
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = Restore(bytes);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var saved = world.ExportState();
        Assert.Equal(new[] { memories[3].Id, memories[0].Id }, saved.Society.Society.ArchivedMemories.Select(item => item.Memory.Id));
        Assert.Equal(memories.Single(item => item.Id == "old-low"), saved.Society.Society.ArchivedMemories.Single(item => item.Memory.Id == "old-low").Memory);
        Assert.Equal(rumor, Assert.Single(saved.Society.Society.ArchivedBeliefs).Belief);
        Assert.All(saved.Society.Society.ArchivedMemories, item => Assert.Equal(6, item.ArchivedTick));
        Assert.Contains(saved.Society.Society.Memories, item => item.Id == "recent");
        Assert.Contains(saved.Society.Society.Memories, item => item.Id == "old-cutoff");
        Assert.All(memories.Where(item => item.Kind != SocietyMemoryKind.Experience), item => Assert.Contains(item, saved.Society.Society.Memories));
        Assert.Equal(JsonSerializer.Serialize(state.Society.Society.Relationships), JsonSerializer.Serialize(saved.Society.Society.Relationships));
        Assert.All(protectedBeliefs, item => Assert.Contains(item, saved.Society.Society.Beliefs!));
        Assert.Equal(state.Agents.Select(item => item.Skills), saved.Agents.Select(item => item.Skills));
        Assert.Contains(saved.Events, item => item.Kind == "agent_memories_archived" && item.Detail == "2:1");
        Assert.DoesNotContain(saved.Events, item => item.Detail.Contains(rumor.Statement, StringComparison.Ordinal));
        var after = WorldRuntimeCodec.Encode(saved);
        using var loaded = Restore(after);
        Assert.Equal(after, WorldRuntimeCodec.Encode(loaded.ExportState()));
        var observations = new List<AgentObservation>();
        using var recalling = WorldRuntime.Restore(WorldRuntimeCodec.Decode(after), _ => new IdleProvider(observations));
        recalling.SubmitInstruction(new("recall-now", "owner:test", owner, OwnerInstructionKind.Suggestion, "Remember the path."));
        for (var tick = 0; tick < 4 && observations.All(item => item.AgentId != owner); tick++)
            Assert.True((await recalling.AdvanceOneTickAsync()).Advanced);
        var observed = Assert.Single(observations.Where(item => item.AgentId == owner).Take(1));
        Assert.DoesNotContain(observed.RetrievedMemories ?? [], item => item.Id is "old-low" or "old-rumor" or "foreign");
        Assert.Contains(observed.RetrievedMemories ?? [], item => item.Id == "old-cutoff");
        Assert.Equal(2, recalling.Society.ArchivedMemories.Count);
        Assert.Single(recalling.Society.ArchivedBeliefs);
    }

    [Fact]
    public async Task AnOpenOwnerOrderPostponesArchivingWithoutResettingTheSavedAge()
    {
        var state = NearBoundary();
        var owner = state.Agents[0].AgentId;
        state = WithSources(state, [new(Identity.Parse("open-task-memory"), owner, state.Agents[1].AgentId, "An unfinished task.", "private", 0)], []);
        using var world = Restore(WorldRuntimeCodec.Encode(state));
        var receipt = world.SubmitInstruction(new("blocked-task", "owner:test", owner, OwnerInstructionKind.Order, "keep gathering wood"));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Contains(world.Society.Memories, item => item.Id == "open-task-memory");
        Assert.Empty(world.Society.ArchivedMemories);
        var open = world.ExportState().Instructions!.Single(item => item.InstructionId == receipt.InstructionId);
        Assert.True(open.Task!.Status is "queued" or "waiting" or "doing" or "interrupted" or "blocked", open.Task.Status);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var reloaded = Restore(bytes);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reloaded.ExportState()));
        reloaded.CancelOrder(new("cancel-blocked-task", "owner:test", reloaded.Society.WorldId, owner, open.InstructionId));
        Assert.True((await reloaded.AdvanceOneTickAsync()).Advanced);
        Assert.True((await reloaded.AdvanceOneTickAsync()).Advanced);
        var archived = Assert.Single(reloaded.Society.ArchivedMemories);
        Assert.Equal((0L, 8L), (archived.Memory.SourceTick, archived.ArchivedTick));
    }

    [Fact]
    public async Task AnArchivedHearsaySourceCanBeCorrectedWithoutLosingItsOriginalEvidence()
    {
        var state = NearBoundary();
        var owner = state.Agents[0].AgentId;
        var other = state.Agents[1].AgentId;
        var rumor = new SocietyAgentBelief(Identity.Parse("old-rumor"), owner, "Mira said the tool was missing.", SocietyBeliefProvenance.Hearsay, 4_200, 0, SourceAgentId: other);
        state = WithSources(state, [], [rumor]);
        using var world = Restore(WorldRuntimeCodec.Encode(state));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var corrected = world.CorrectAgentBelief(owner, rumor.Id,
            new(Identity.Parse("seen-tool"), owner, "I saw the tool beside the House.", SocietyBeliefProvenance.Firsthand, 9_600, world.WorldTick));
        var source = Assert.Single(world.Society.ArchivedBeliefs).Belief;
        Assert.Equal((rumor.Statement, rumor.SourceAgentId, rumor.ConfidenceBasisPoints, corrected.Id),
            (source.Statement, source.SourceAgentId, source.ConfidenceBasisPoints, source.SupersededByBeliefId));
        Assert.Equal(rumor.Id, corrected.SupersedesBeliefId);
        Assert.Equal(corrected, Assert.Single(world.Society.Beliefs!));
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.Throws<InvalidOperationException>(() => world.RecordAgentBelief(rumor));
        Assert.Throws<InvalidOperationException>(() => world.CorrectAgentBelief(other, corrected.Id,
            new(Identity.Parse("stolen-correction"), other, "My correction.", SocietyBeliefProvenance.Inference, 5_000, world.WorldTick)));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var loaded = Restore(bytes);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(loaded.ExportState()));
        for (var tick = 0; tick < 6; tick++) Assert.True((await loaded.AdvanceOneTickAsync()).Advanced);
        Assert.Empty(loaded.Society.Beliefs!);
        Assert.Equal(2, loaded.Society.ArchivedBeliefs.Count);
        var coldBytes = WorldRuntimeCodec.Encode(loaded.ExportState());
        using var coldReload = Restore(coldBytes);
        Assert.Equal(coldBytes, WorldRuntimeCodec.Encode(coldReload.ExportState()));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("protected")]
    [InlineData("owner")]
    [InlineData("future")]
    [InlineData("kind")]
    public async Task StrictLoadingRefusesMalformedArchivesAndPreservesTheOriginalCheckpoint(string fault)
    {
        var state = NearBoundary();
        var owner = state.Agents[0].AgentId;
        state = WithSources(state, [new(Identity.Parse("old-low"), owner, state.Agents[1].AgentId, "Private memory.", "private", 0)], []);
        using var world = Restore(WorldRuntimeCodec.Encode(state));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        var json = JsonNode.Parse(bytes)!;
        var society = json["state"]!["society"]!["society"]!;
        var memory = society["archivedMemories"]![0]!["memory"]!;
        switch (fault)
        {
            case "missing": society.AsObject().Remove("archivedBeliefs"); break;
            case "duplicate": society["memories"]!.AsArray().Add(memory.DeepClone()); break;
            case "protected": memory["kind"] = (int)SocietyMemoryKind.LifeEvent; break;
            case "owner": memory["ownerId"] = "unknown-owner"; break;
            case "future": society["archivedMemories"]![0]!["archivedTick"] = 7; break;
            case "kind": memory.AsObject().Remove("kind"); break;
        }
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(System.Text.Encoding.UTF8.GetBytes(json.ToJsonString())));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
    }

    private static WorldRuntimeState WithSources(WorldRuntimeState state,
        IReadOnlyList<SocietySocialMemory> memories, IReadOnlyList<SocietyAgentBelief> beliefs,
        IReadOnlyList<SocietyAgentMemoryCompaction>? compactions = null) => state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                { Memories = memories.OrderBy(item => item.Id.Value, StringComparer.Ordinal).ToArray(), Beliefs = beliefs, MemoryCompactions = compactions }
            },
        };

    private static WorldRuntimeState NearBoundary(bool generated = false)
    {
        using var seed = generated ? NormalPathWorld.CreateGenerated("memory-archive-native", _ => new IdleProvider(), ticksPerDay: 2) :
            new WorldRuntime("memory-archive-native", _ => new IdleProvider());
        var state = seed.ExportState();
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
        };
        var systems = WorldSystemsRules.CreateGenesis(state.WorldSeed, state.WorldSystems!.Config with { TicksPerDay = 2, CalendarOffsetTicks = 0 },
            state.WorldSystems.Ecology.Resources, state.WorldSystems.Chunks);
        for (var tick = 0; tick < 5; tick++) systems = WorldSystemsRules.AdvanceOneTick(systems);
        return state with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Society = state.Society with { Society = society },
            WorldSystems = systems,
            Agents = state.Agents.Select(item => item with { LastDecisionContext = null, FullnessBasisPoints = 10_000 }).ToArray(),
        };
    }

    private static WorldRuntime Restore(byte[] bytes) => WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new IdleProvider());

    private sealed class IdleProvider(List<AgentObservation>? observations = null) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            observations?.Add(request.Observation);
            const string selected = "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind, ProviderEpoch,
                request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest, selected, 1,
                new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
