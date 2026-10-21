using System.Text.Json;
using System.Text.Json.Nodes;
using ClankerWorld.Server.Saves;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TypedMemoryCompatibilityTests
{
    [Theory]
    [InlineData("oversized")]
    [InlineData("control")]
    [InlineData("padded")]
    public void ExistingMemorySpellingReloadsWithoutRewritingOrEnteringRecall(string spelling)
    {
        using var world = new WorldRuntime("stored-memory-spelling");
        var state = world.ExportState();
        var owner = state.Society.Society.Agents[0].Id;
        var checkpoint = SocietyRules.RecordSocialMemory(state.Society.Society,
            new(Identity.Parse("source"), owner, owner, "A quiet walk beside the river.", "private", 0)).Checkpoint;
        state = state with { Society = state.Society with { Society = checkpoint } };
        var document = JsonNode.Parse(WorldRuntimeCodec.Encode(state))!;
        var key = spelling switch { "oversized" => new string('x', 129), "control" => "control\0memory", _ => " padded-memory " };
        document["state"]!["society"]!["society"]!["memories"]!.AsArray()[0]!["id"] = key;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document);
        var directory = Directory.CreateTempSubdirectory("stored-memory-spelling-");
        try
        {
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            File.WriteAllBytes(file.Path, bytes);
            using var loaded = file.LoadOrCreate(state.WorldSeed);
            Assert.Equal(key, Assert.Single(loaded.Society.Memories).Id.Value);
            Assert.Equal(bytes, File.ReadAllBytes(file.Path));
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(loaded.ExportState()));
            Assert.Empty(WorldMemoryRetrieval.Retrieve(loaded.Society.Memories, [], null, owner, 0,
                [new CognitionCandidate("safe_idle", "Remember the walk.", 0)]));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("archived-memory")]
    [InlineData("belief")]
    [InlineData("archived-belief")]
    [InlineData("summary")]
    [InlineData("compaction-source")]
    [InlineData("summary-source")]
    public void RequiredStoredIdsUseTheDomainRefusalPath(string damage)
    {
        var state = Sources();
        var summary = state.MemorySummaries[0];
        var badSources = summary.Sources.Select((source, index) => index == 0 ? source with { Id = null! } : source).ToArray();
        var damaged = damage switch
        {
            "memory" => state with { Memories = state.Memories.Select((item, index) => index == 0 ? item with { Id = null! } : item).ToArray() },
            "archived-memory" => state with { ArchivedMemories = state.ArchivedMemories.Select((item, index) => index == 0 ? item with { Memory = item.Memory with { Id = null! } } : item).ToArray() },
            "belief" => state with { Beliefs = state.Beliefs!.Select((item, index) => index == 0 ? item with { Id = null! } : item).ToArray() },
            "archived-belief" => state with { ArchivedBeliefs = state.ArchivedBeliefs.Select((item, index) => index == 0 ? item with { Belief = item.Belief with { Id = null! } } : item).ToArray() },
            "summary" => state with { MemorySummaries = [summary with { Id = null! }] },
            "compaction-source" => state with { MemoryCompactions = [state.MemoryCompactions![0] with { Sources = state.MemoryCompactions[0].Sources.Select((item, index) => index == 0 ? item with { SourceId = null! } : item).ToArray() }] },
            _ => state with { MemorySummaries = [summary with { Sources = badSources, Id = Identity.Parse(SocietyMemorySummaryRules.Id(summary.OwnerId, badSources)) }] },
        };
        Assert.Throws<InvalidDataException>(() => SocietyRules.Validate(damaged));
        Assert.Throws<InvalidDataException>(() => SocietyCheckpointCodec.Encode(damaged));
    }

    private static SocietyCheckpoint Sources()
    {
        var config = new SocietyConfig { TicksPerWorldDay = 1, BaseNaturalMortalityBasisPoints = 0 };
        var state = SocietyRules.CreateGenesis("typed-source-refusal",
            [SocietyRules.CreateFounder("owner", "Owner Vale", config: config), SocietyRules.CreateFounder("other", "Other Vale", config: config)], config: config);
        for (var index = 0; index < 4; index++)
            state = SocietyRules.RecordSocialMemory(state, new(Identity.Parse("old-" + index), "owner", "other",
                "I walked along the familiar river path and remembered the changing weather that afternoon " + index, "private", 0)).Checkpoint;
        state = SocietyRules.RecordAgentBelief(state, new(Identity.Parse("old-belief"), "owner",
            "I believe the river path is muddy.", SocietyBeliefProvenance.Firsthand, 5_000, 0));
        state = SocietyRules.AdvanceTo(SocietyRules.Resume(state).Checkpoint, 4).Checkpoint;
        state = SocietyMemoryArchiveRules.Archive(state, new HashSet<string>(), new HashSet<string>());
        state = SocietyRules.RecordSocialMemory(state, new(Identity.Parse("live"), "owner", "other", "A recent walk.", "private", 4)).Checkpoint;
        state = SocietyRules.RecordAgentBelief(state, new(Identity.Parse("live-belief"), "owner", "The sky is clear.", SocietyBeliefProvenance.Firsthand, 5_000, 4));
        state = SocietyRules.RecordAgentMemorySummary(state, "owner", SocietyMemorySummaryRules.Options(state, "owner")[0]);
        return SocietyRules.RecordAgentMemoryCompaction(state, "owner",
            [new(Identity.Parse("live"), SocietyMemorySourceKind.Experience, 4, 5_000, 8_000, 4), new(Identity.Parse("old-0"), SocietyMemorySourceKind.Experience, 0, 5_000, 8_000, 4)]);
    }
}
