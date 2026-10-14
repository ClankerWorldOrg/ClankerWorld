using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>Bounded directed trust earned from committed cooperation.</summary>
public sealed record SocialStandingEntry(string SubjectId, int Trust, long LastChangedTick);

public sealed partial class WorldRuntime
{
    private int TrustScore(string owner, string subject)
    {
        var saved = agents[owner].SocialStanding?.FirstOrDefault(item => item.SubjectId == subject);
        if (saved is not null)
        {
            return saved.Trust;
        }

        // Durable cooperation memories provide the projection until a direct
        // trust transition materializes this bounded snapshot.
        return Math.Min(10, society.Checkpoint.Memories.Where(memory =>
                memory.OwnerId == owner && memory.SubjectId == subject && memory.TombstonedTick is null)
            .Sum(memory => memory.Id.Value.StartsWith("project-gratitude:", StringComparison.Ordinal) ? 2
                : memory.Id.Value.StartsWith("lesson-gratitude:", StringComparison.Ordinal) ? 2
                : memory.Id.Value.StartsWith("trust-memory:", StringComparison.Ordinal) ? 1 : 0));
    }

    private void IncreaseTrust(string owner, string subject, int amount, string reason)
    {
        if (owner == subject || amount <= 0 || !agents.TryGetValue(owner, out var person) || !agents.ContainsKey(subject))
        {
            return;
        }

        var previous = TrustScore(owner, subject);
        var next = Math.Min(10, checked(previous + amount));
        if (next == previous)
        {
            return;
        }

        var standing = (person.SocialStanding ?? []).Where(item => item.SubjectId != subject)
            .Append(new SocialStandingEntry(subject, next, WorldTick))
            .OrderBy(item => item.SubjectId, StringComparer.Ordinal).ToArray();
        agents[owner] = person with { SocialStanding = standing };
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent(new SocialStandingChangedEventPayload(owner, subject, reason));
    }

    private static void ValidateSocialStanding(
        AgentState person,
        IEnumerable<string> knownAgents,
        long worldTick)
    {
        if (person.SocialStanding is null)
        {
            return;
        }
        var known = knownAgents.ToHashSet(StringComparer.Ordinal);
        if (person.SocialStanding.Select(item => item.SubjectId).Distinct(StringComparer.Ordinal).Count() != person.SocialStanding.Count ||
            person.SocialStanding.Any(item => item.SubjectId == person.AgentId || !known.Contains(item.SubjectId) ||
                item.Trust is < 1 or > 10 || item.LastChangedTick < 0 || item.LastChangedTick > worldTick))
        {
            throw new InvalidDataException("The saved social standing is invalid.");
        }
    }
}
