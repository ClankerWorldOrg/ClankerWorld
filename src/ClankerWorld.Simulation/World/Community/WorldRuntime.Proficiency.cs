namespace ClankerWorld.Simulation.World;

/// <summary>Bounded experience earned by committed useful work, not model claims.</summary>
public sealed record WorkProficiency(int Building = 0, int Farming = 0, int Crafting = 0)
{
    public static int Level(int experience) => experience / 10;
}

public sealed partial class WorldRuntime
{
    private void CreditCompletedWork(string actor, string domain, SkillKind? skill = null) => CommunitySystem.CreditCompletedWork(CommunitySkillWorld, actor, domain, skill);
}
