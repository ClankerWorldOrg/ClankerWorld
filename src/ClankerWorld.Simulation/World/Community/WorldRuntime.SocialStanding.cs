
namespace ClankerWorld.Simulation.World;

/// <summary>Bounded directed trust earned from committed cooperation.</summary>
public sealed record SocialStandingEntry(string SubjectId, int Trust, long LastChangedTick);

public sealed partial class WorldRuntime
{

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
        CommunitySystem.ValidateLocalStanding(person.AgentId, person.SocialStanding);
        if (person.SocialStanding.Any(item => !known.Contains(item.SubjectId) || item.LastChangedTick > worldTick))
        {
            throw new InvalidDataException("The saved social standing is invalid.");
        }
    }
}
