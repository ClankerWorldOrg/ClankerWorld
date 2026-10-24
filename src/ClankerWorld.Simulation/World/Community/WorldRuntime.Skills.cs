using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public enum SkillKind
{
    Building,
    Farming,
    Crafting,
    Smithing,
}

/// <summary>A first learning event, independent of roles and work proficiency.</summary>
public sealed record LearnedSkill(SkillKind Kind, long LearnedTick, string? TeacherId = null);

public sealed partial class WorldRuntime
{
    private int SkilledWorkTicks(string actor, SkillKind skill, int ticks) => CommunitySystem.SkilledWorkTicks(agentStore, actor, skill, ticks);
    private int SkilledWorkProgress(string actor, SkillKind skill, int done, int work) => CommunitySystem.SkilledWorkProgress(agentStore, actor, skill, done, work);
    private static void ValidateSkills(AgentState person, int schema, long latestTick, HashSet<string> known) => CommunitySystem.ValidateSkills(person, schema, latestTick, known);
}
