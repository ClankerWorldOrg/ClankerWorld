using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal static void ApplyMemorySummary(ICognitionMemoriesWorld world, string ownerId, CognitionMemorySummaryOption summary)
    {
        // Recheck the source batch after an awaited reply: corrections or another
        // accepted summary may have changed it while the helper was working.
        var current = WorldMemoryRetrieval.SummaryOptions(world.Society, ownerId)
            .SingleOrDefault(option => option.Choice == summary.Choice && option.Text == summary.Text &&
                option.OwnerId == summary.OwnerId && option.Sources.SequenceEqual(summary.Sources));
        if (current is null) return;
        var sources = summary.Sources.Select(source => new SocietyMemorySummarySource(Identity.ParseStored(source.Id),
            source.Kind == "experience" ? SocietyMemorySourceKind.Experience : SocietyMemorySourceKind.Belief, source.SourceTick)).ToArray();
        var next = SocietyRules.RecordAgentMemorySummary(world.Society, ownerId,
            new SocietyMemorySummaryOption(summary.Choice, summary.Text, sources));
        world.RecordMemorySummaryState(next);
        world.MarkCheckpointCurrent();
        world.AppendEvent(new AgentMemoriesSummarizedEventPayload(ownerId, sources.Length));
    }


    internal static void ApplyMemoryCompaction(ICognitionMemoriesWorld world,
        string ownerId,
        IReadOnlyList<CognitionMemoryCompactionScore> scores)
    {
        if (scores.Count == 0) return;
        var indexed = scores.Select(score => new SocietyAgentMemoryImportance(
                Identity.Parse(score.Id),
                score.Kind switch
                {
                    "experience" => SocietyMemorySourceKind.Experience,
                    "belief" => SocietyMemorySourceKind.Belief,
                    _ => throw new InvalidDataException("A memory compaction returned an unsupported source kind."),
                },
                score.SourceTick,
                score.ImportanceBasisPoints,
                score.ConfidenceBasisPoints,
                world.WorldTick))
            .ToArray();
        var next = SocietyRules.RecordAgentMemoryCompaction(world.Society, ownerId, indexed);
        world.RecordMemoryCompactionState(next);
        world.MarkCheckpointCurrent();
        var retained = (world.Society.MemoryCompactions ?? [])
            .Single(item => item.OwnerId == ownerId).Sources.Count;
        world.Host.RecordMemoryCompaction(new WorldMemoryCompactionTransition(
                    world.WorldTick, ownerId, indexed.Length, retained));
    }

}
