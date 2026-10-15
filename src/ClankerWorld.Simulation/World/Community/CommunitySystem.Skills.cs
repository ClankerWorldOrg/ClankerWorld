using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CommunitySystem
{
    internal const int SkillsSchemaVersion = 31;
    // Provisional for playtesting. Round down to whole ticks, with at least one.
    private const int SkillWorkReductionPercent = 20;

    internal static int SkilledWorkTicks(AgentStore agents, string actor, SkillKind skill, int ticks) =>
        HasSkill(agents, actor, skill) ? Math.Max(1, (int)((long)ticks * (100 - SkillWorkReductionPercent) / 100)) : ticks;

    internal static int SkilledWorkProgress(AgentStore agents, string actor, SkillKind skill, int done, int work)
    {
        if (!HasSkill(agents, actor, skill)) return done + work;
        // Keep progress in its existing full-work units, including completion
        // receipts. Convert back with a ceiling so a whole tick always advances.
        var rate = 100 - SkillWorkReductionPercent;
        var elapsed = ((long)done * rate + 99) / 100;
        return (int)((elapsed + work) * 100 / rate);
    }

    internal static bool HasSkill(AgentStore agents, string actor, SkillKind skill) =>
        agents.Living.TryGetValue(actor, out var person) && person.Skills?.Any(item => item.Kind == skill) == true;

    internal static void GainSkill(ICommunitySkillWorld world, string actor, SkillKind skill, string? teacher = null)
    {
        if (!world.PhysicalAgents.Living.TryGetValue(actor, out var person) || HasSkill(world.PhysicalAgents, actor, skill)) return;
        world.PhysicalAgents.RecordSkills(actor, person, (person.Skills ?? []).Append(new LearnedSkill(skill, world.WorldTick, teacher))
                .OrderBy(item => item.Kind).ToArray());
        world.MarkCurrentSchema();
        world.AppendEvent("skill_learned", $"{actor}|{skill.ToString().ToLowerInvariant()}|{teacher ?? "work"}");
    }

    internal static SkillKind SkillForRecipe(DeclarativeWorldContentState content, RecipeDefinition recipe)
    {
        var workstation = content.Buildings.FirstOrDefault(item => item.CanonicalId == recipe.WorkstationBuildingId);
        if (recipe.IsCrop || workstation?.Tags.Contains("farmhouse", StringComparer.Ordinal) == true)
            return SkillKind.Farming;
        return workstation?.Tags.Contains("blacksmith", StringComparer.Ordinal) == true
            ? SkillKind.Smithing : SkillKind.Crafting;
    }

    internal static void ValidateSkills(AgentState person, int schema, long latestTick,
        HashSet<string> known)
    {
        if (person.Skills is not { } skills) return;
        ValidateLocalSkills(person.AgentId, skills);
        if (schema < SkillsSchemaVersion || skills.Any(item => item.LearnedTick > latestTick ||
            item.TeacherId is { } teacher && !known.Contains(teacher)))
            throw new InvalidDataException("The saved skills are invalid.");
    }
    internal static int ProjectPracticeBonus(DeclarativeWorldContentState content, AgentState person, AgentProject project)
    {
        var practice = person.Proficiency ?? new();
        var experience = project.CandidateId.StartsWith("build:building:", StringComparison.Ordinal)
            ? practice.Building
            : content.Recipes.FirstOrDefault(recipe => "build:recipe:" + recipe.CanonicalId == project.CandidateId)?.IsCrop == true
                ? practice.Farming : practice.Crafting;
        return WorkProficiency.Level(experience);
    }

    internal static void CreditCompletedWork(ICommunitySkillWorld world, string actor, string domain, SkillKind? skill = null)
    {
        if (!world.PhysicalAgents.Living.TryGetValue(actor, out var person)) return;
        var previous = person.Proficiency ?? new();
        var next = domain switch
        {
            "building" => previous with { Building = Math.Min(30, previous.Building + 1) },
            "farming" => previous with { Farming = Math.Min(30, previous.Farming + 1) },
            "crafting" => previous with { Crafting = Math.Min(30, previous.Crafting + 1) },
            _ => throw new InvalidOperationException("Unknown practice domain."),
        };
        if (next != previous)
        {
            world.PhysicalAgents.RecordProficiency(actor, person, next);
            world.MarkCurrentSchema();
            world.AppendEvent("work_practice_earned", actor);
        }
        GainSkill(world, actor, skill ?? domain switch
        {
            "building" => SkillKind.Building,
            "farming" => SkillKind.Farming,
            _ => SkillKind.Crafting,
        });
    }

    internal static void ValidateProficiency(AgentState person)
    {
        if (person.Proficiency is { } practice &&
            (practice.Building is < 0 or > 30 || practice.Farming is < 0 or > 30 || practice.Crafting is < 0 or > 30))
            throw new InvalidDataException("Work proficiency exceeds its experience bounds.");
    }

    internal static void ValidateLocalSkills(string actor, IReadOnlyList<LearnedSkill> skills)
    {
        if (skills.Count > Enum.GetValues<SkillKind>().Length || skills.Any(item => item is null) ||
            skills.Select(item => item.Kind).Distinct().Count() != skills.Count ||
            skills.Any(item => !Enum.IsDefined(item.Kind) || item.LearnedTick < 0 || item.TeacherId == actor))
            throw new InvalidDataException("Community skills have invalid local state.");
    }
}
