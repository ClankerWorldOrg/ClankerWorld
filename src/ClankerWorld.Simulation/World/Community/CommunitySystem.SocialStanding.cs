namespace ClankerWorld.Simulation.World;

internal sealed partial class CommunitySystem
{
    internal static int TrustScore(ICommunitySkillWorld world, string owner, string subject)
    {
        var saved = world.PhysicalAgents.Living[owner].SocialStanding?.FirstOrDefault(item => item.SubjectId == subject);
        if (saved is not null)
        {
            return saved.Trust;
        }

        // Durable cooperation memories provide the projection until a direct
        // trust transition materializes this bounded snapshot.
        return Math.Min(10, world.Society.Memories.Where(memory =>
                memory.OwnerId == owner && memory.SubjectId == subject && memory.TombstonedTick is null)
            .Sum(memory => memory.Id.StartsWith("project-gratitude:", StringComparison.Ordinal) ? 2
                : memory.Id.StartsWith("lesson-gratitude:", StringComparison.Ordinal) ? 2
                : memory.Id.StartsWith("trust-memory:", StringComparison.Ordinal) ? 1 : 0));
    }

    internal static void IncreaseTrust(ICommunitySkillWorld world, string owner, string subject, int amount, string reason)
    {
        if (owner == subject || amount <= 0 || !world.PhysicalAgents.Living.TryGetValue(owner, out var person) || !world.PhysicalAgents.Living.ContainsKey(subject))
        {
            return;
        }

        var previous = TrustScore(world, owner, subject);
        var next = Math.Min(10, checked(previous + amount));
        if (next == previous)
        {
            return;
        }

        var standing = (person.SocialStanding ?? []).Where(item => item.SubjectId != subject)
            .Append(new SocialStandingEntry(subject, next, world.WorldTick))
            .OrderBy(item => item.SubjectId, StringComparer.Ordinal).ToArray();
        world.PhysicalAgents.RecordSocialStanding(owner, person, standing);
        world.MarkCurrentSchema();
        world.AppendEvent("social_standing_changed", $"{owner}:{subject}:{reason}");
    }

    internal static void ValidateLocalStanding(string actor, IReadOnlyList<SocialStandingEntry> standing)
    {
        if (standing.Select(item => item.SubjectId).Distinct(StringComparer.Ordinal).Count() != standing.Count ||
            standing.Any(item => item.SubjectId == actor || string.IsNullOrWhiteSpace(item.SubjectId) ||
                item.Trust is < 1 or > 10 || item.LastChangedTick < 0))
            throw new InvalidDataException("Community social standing has invalid local state.");
    }
}
