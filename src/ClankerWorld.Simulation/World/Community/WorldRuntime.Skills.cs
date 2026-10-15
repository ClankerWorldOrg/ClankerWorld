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
    private const int SkillsSchemaVersion = CommunitySystem.SkillsSchemaVersion;
    private int SkilledWorkTicks(string actor, SkillKind skill, int ticks) => CommunitySystem.SkilledWorkTicks(agentStore, actor, skill, ticks);
    private int SkilledWorkProgress(string actor, SkillKind skill, int done, int work) => CommunitySystem.SkilledWorkProgress(agentStore, actor, skill, done, work);
    private bool HasSkill(string actor, SkillKind skill) => CommunitySystem.HasSkill(agentStore, actor, skill);
    private void GainSkill(string actor, SkillKind skill, string? teacher = null) => CommunitySystem.GainSkill(CommunitySkillWorld, actor, skill, teacher);
    private SkillKind SkillForRecipe(RecipeDefinition recipe) => CommunitySystem.SkillForRecipe(worldContent, recipe);
    private static void ValidateSkills(AgentState person, int schema, long latestTick, HashSet<string> known) => CommunitySystem.ValidateSkills(person, schema, latestTick, known);
}
