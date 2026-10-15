using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed record SkillLesson(string TeacherId, SkillKind Skill, string Stage,
    int Progress, long RequestedTick, long LastTransitionTick);

public sealed partial class WorldRuntime
{
    private const int LessonWorkRequired = CommunitySystem.LessonWorkRequired;
    private static bool ActiveLesson(SkillLesson? lesson) => CommunitySystem.ActiveLesson(lesson);
    private bool ReadyForLesson(string actor) => CommunitySystem.ReadyForLesson(CommunityLearningWorld, actor);
    private bool CanMentor(string teacher, SkillKind skill) => CommunitySystem.CanMentor(CommunityLearningWorld, teacher, skill);
    private bool FreeToMentor(string teacher, string? requestedStudent = null) => CommunitySystem.FreeToMentor(CommunityLearningWorld, teacher, requestedStudent);
    private AgentState? ActiveStudent(string teacher) => CommunitySystem.ActiveStudent(CommunityLearningWorld, teacher);
    private bool HasLearningDecision(string actor) => CommunitySystem.HasLearningDecision(CommunityLearningWorld, actor);
    private bool CanContinueLesson(string actor, bool waitingForModel = false) => CommunitySystem.CanContinueLesson(CommunityLearningWorld, actor, waitingForModel);
    private void MaintainLessons() => CommunitySystem.MaintainLessons(CommunityLearningWorld);
    private void AddLearningCandidateOptions(List<CognitionCandidate> candidates, string actor) => CommunitySystem.AddLearningCandidateOptions(CommunityLearningWorld, candidates, actor);

    private void ApplyLearningCandidate(string actor, string candidate, ActionPlan? originalPlan = null)
    {
        var plan = originalPlan as LearningActionPlan ?? (LearningChoice.Decode(candidate) is { } choice ? PlanLearningAction(actor, choice) : null);
        if (plan is not null) ApplyLearningActionPlan(plan);
    }

    private void CompleteLearningAction(LearningActionPlan plan) => CommunitySystem.CompleteLearningAction(CommunityLearningWorld, plan);
    private void ContinueLesson(string actor, bool waitingForModel = false, LearningActionPlan? originalPlan = null) => CommunitySystem.ContinueLesson(CommunityLearningWorld, actor, waitingForModel, originalPlan);
    private void SetLesson(string student, SkillLesson lesson) => CommunitySystem.SetLesson(CommunityLearningWorld, student, lesson);

    private static void ValidateLessons(WorldRuntimeState state)
    {
        var known = state.Society.Society.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var active = state.Agents.Where(person => ActiveLesson(person.Lesson)).Select(person => person.Lesson!.TeacherId).ToArray();
        if (active.Distinct(StringComparer.Ordinal).Count() != active.Length)
        {
            throw new InvalidDataException("A mentor cannot hold more than one active apprenticeship.");
        }
        foreach (var person in state.Agents)
        {
            ValidateSkills(person, state.SchemaVersion, state.Society.Society.WorldTick, known);
            if (person.Lesson is { } localLesson) CommunitySystem.ValidateLocalLesson(localLesson);
            if (person.Lesson is { } lesson && (state.SchemaVersion < SkillsSchemaVersion || !known.Contains(lesson.TeacherId) ||
                lesson.TeacherId == person.AgentId || lesson.LastTransitionTick > state.Society.Society.WorldTick))
            {
                throw new InvalidDataException("The saved apprenticeship is invalid.");
            }
        }
    }
}
