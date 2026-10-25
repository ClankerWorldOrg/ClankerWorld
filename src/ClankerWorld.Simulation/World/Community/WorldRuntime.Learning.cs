using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed record SkillLesson(string TeacherId, SkillKind Skill, string Stage,
    int Progress, long RequestedTick, long LastTransitionTick);

public sealed partial class WorldRuntime
{
    private void SetLesson(string student, SkillLesson lesson) => CommunitySystem.SetLesson(CommunityLearningWorld, student, lesson);

    private static void ValidateLessons(WorldRuntimeState state)
    {
        var known = state.Society.Society.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var active = state.Agents.Where(person => CommunitySystem.ActiveLesson(person.Lesson)).Select(person => person.Lesson!.TeacherId).ToArray();
        if (active.Distinct(StringComparer.Ordinal).Count() != active.Length)
        {
            throw new InvalidDataException("A mentor cannot hold more than one active apprenticeship.");
        }
        foreach (var person in state.Agents)
        {
            ValidateSkills(person, state.SchemaVersion, state.Society.Society.WorldTick, known);
            if (person.Lesson is { } localLesson) CommunitySystem.ValidateLocalLesson(localLesson);
            if (person.Lesson is { } lesson && (state.SchemaVersion < global::ClankerWorld.Simulation.World.CommunitySystem.SkillsSchemaVersion || !known.Contains(lesson.TeacherId) ||
                lesson.TeacherId == person.AgentId || lesson.LastTransitionTick > state.Society.Society.WorldTick))
            {
                throw new InvalidDataException("The saved apprenticeship is invalid.");
            }
        }
    }
}
