using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class WorldMemoryImportanceRetentionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void ExistingProtectedScoresAllowOnlyNewAssessmentsThatCanBeRetained(long sourceTick)
    {
        var checkpoint = SocietyRules.AdvanceTo(SocietyRules.CreateGenesis("importance-retention",
            [SocietyRules.CreateFounder("owner", "Owner"), SocietyRules.CreateFounder("other", "Other")]), 11).Checkpoint;
        var protectedMemories = Enumerable.Range(0, 256).Select(index => new SocietySocialMemory(
            $"protected-{index:D3}", "owner", "owner", "I learned a useful craft.", "private", 10)
        { Kind = SocietyMemoryKind.Skill }).ToArray();
        var ordinaryMemories = Enumerable.Range(0, 12).Select(index => new SocietySocialMemory(
            $"ordinary-{index:D3}", "owner", "owner", "I remembered a quiet walk.", "private", sourceTick)).ToArray();
        var previousScores = protectedMemories.Select(memory =>
            new SocietyAgentMemoryImportance(memory.Id, SocietyMemorySourceKind.Experience, 10, 5_000, 8_000, 10)).ToArray();
        checkpoint = checkpoint with
        {
            Memories = protectedMemories.Concat(ordinaryMemories).Append(new("foreign", "other", "other",
                    "Someone else's private experience.", "private", 11))
                .OrderBy(memory => memory.Id, StringComparer.Ordinal).ToArray(),
            MemoryCompactions = [new("owner", previousScores)],
        };
        var before = SocietyCheckpointCodec.Encode(checkpoint);
        checkpoint = SocietyCheckpointCodec.Decode(before);
        var candidates = Unassessed(checkpoint);
        Assert.Equal(before, SocietyCheckpointCodec.Encode(checkpoint));
        if (sourceTick == 0)
        {
            Assert.Empty(candidates);
            Assert.Equal(previousScores, checkpoint.MemoryCompactions!.Single().Sources);
            return;
        }

        Assert.Equal(12, candidates.Count);
        Assert.All(candidates, candidate =>
        {
            Assert.Equal("owner", candidate.OwnerId);
            Assert.StartsWith("ordinary-", candidate.Id);
        });
        var accepted = SocietyRules.RecordAgentMemoryCompaction(checkpoint, "owner", candidates.Select(candidate =>
            new SocietyAgentMemoryImportance(candidate.Id, SocietyMemorySourceKind.Experience,
                candidate.SourceTick, 5_000, 8_000, checkpoint.WorldTick)).ToArray());
        var after = SocietyCheckpointCodec.Encode(accepted);
        accepted = SocietyCheckpointCodec.Decode(after);
        Assert.Equal(after, SocietyCheckpointCodec.Encode(accepted));
        Assert.Equal(checkpoint.Memories, accepted.Memories);
        var scores = accepted.MemoryCompactions!.Single().Sources;
        Assert.Equal(256, scores.Count);
        Assert.Equal(12, scores.Count(score => score.SourceId.StartsWith("ordinary-", StringComparison.Ordinal)));
        Assert.All(scores.Where(score => score.SourceId.StartsWith("protected-", StringComparison.Ordinal)),
            score => Assert.Contains(score, previousScores));
        Assert.Empty(Unassessed(accepted));

        static IReadOnlyList<CognitionMemoryCompactionCandidate> Unassessed(SocietyCheckpoint state) =>
            WorldMemoryRetrieval.Unassessed(state.Memories, state.Beliefs ?? [], state.MemoryCompactions!.Single(),
                "owner", state.WorldTick);
    }
}
