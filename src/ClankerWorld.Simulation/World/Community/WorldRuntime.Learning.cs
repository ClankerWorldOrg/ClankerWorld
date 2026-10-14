using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed record SkillLesson(string TeacherId, SkillKind Skill, string Stage,
    int Progress, long RequestedTick, long LastTransitionTick);

public sealed partial class WorldRuntime
{
    private const int LessonWorkRequired = 20;

    private static bool ActiveLesson(SkillLesson? lesson) => lesson?.Stage is "requested" or "accepted" or "training";

    private bool ReadyForLesson(string actor) => agents.TryGetValue(actor, out var person) &&
        !NeedsUrgentFood(person) && !NeedsUrgentWarmth(person);

    private bool CanReachLessonSite(string actor) =>
        IsWithinInteractionRange(agents[actor].Position, SharedStoragePosition, ResourceInteractionRange) ||
        Reach(actor, SharedStoragePosition, ReachPurpose.Learning, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0;

    private bool CanMentor(string teacher, SkillKind skill) =>
        IsAdult(teacher) && !ActiveLesson(agents[teacher].Lesson) && HasSkill(teacher, skill);

    private bool FreeToMentor(string teacher, string? requestedStudent = null) =>
        ReadyForLesson(teacher) && PendingInstructionFor(teacher) is null &&
        !IsConversationBusy(teacher) && ActiveStudent(teacher) is null &&
        agents[teacher].Project is null or { Stage: "completed" or "cancelled" or "paused" } &&
        !HasFoodPolicyDecision(teacher) && !HasHousingDecision(teacher) && !HasTradeResponse(teacher) && !HasFamilyDecision(teacher) &&
        !HasParenthoodDecision(teacher) && !HasDependentCareDecision(teacher) &&
        !agents.Values.Any(item => item.AgentId != requestedStudent &&
            ActiveLesson(item.Lesson) && item.Lesson!.TeacherId == teacher);

    private AgentState? ActiveStudent(string teacher) => agents.Values.FirstOrDefault(person =>
        person.Lesson is { Stage: "accepted" or "training" } lesson && lesson.TeacherId == teacher);

    private bool HasLearningDecision(string actor) => ReadyForLesson(actor) && agents.Values.Any(person =>
        person.Lesson is { Stage: "requested" } lesson && lesson.TeacherId == actor);

    private bool CanContinueLesson(string actor, bool waitingForModel = false)
    {
        if (!IsAdult(actor) || !ReadyForLesson(actor) || IsConversationBusy(actor) ||
            !waitingForModel && (HasFoodPolicyDecision(actor) || HasHousingDecision(actor) || HasTradeResponse(actor) || HasFamilyDecision(actor) || HasParenthoodDecision(actor) || HasDependentCareDecision(actor)))
        {
            return false;
        }
        if (agents[actor].Lesson is { Stage: "accepted" or "training" } lesson)
        {
            return ReadyForLesson(lesson.TeacherId) && IsAdult(lesson.TeacherId) &&
                !IsConversationBusy(lesson.TeacherId) &&
                (!HasHousingDecision(lesson.TeacherId) || society.PendingModelAgentIds().Contains(lesson.TeacherId));
        }
        return ActiveStudent(actor) is { } student && IsAdult(student.AgentId) &&
            ReadyForLesson(student.AgentId) && !IsConversationBusy(student.AgentId) &&
            (!HasHousingDecision(student.AgentId) || society.PendingModelAgentIds().Contains(student.AgentId));
    }

    private void MaintainLessons()
    {
        foreach (var person in agents.Values.ToArray())
        {
            if (person.Lesson is not { } lesson || !ActiveLesson(lesson))
            {
                continue;
            }
            if (!IsAdult(person.AgentId) || !CanMentor(lesson.TeacherId, lesson.Skill) ||
                WorldTick - lesson.RequestedTick > (lesson.Stage == "requested" ? 120 : 600))
            {
                SetLesson(person.AgentId, lesson with { Stage = "cancelled" });
            }
        }
    }

    private void AddLearningCandidateOptions(List<CognitionCandidate> candidates, string actor)
    {
        if (!ReadyForLesson(actor) || survivalState is null)
        {
            return;
        }
        var person = agents[actor];
        if (ActiveLesson(person.Lesson))
        {
            if (person.Lesson!.Stage is "accepted" or "training" && CanContinueLesson(actor))
            {
                candidates.Add(new("lesson_attend", "Attend the agreed practical lesson.", 18));
            }
            candidates.Add(new("lesson_cancel", "Withdraw from the requested or ongoing lesson.", 110));
        }
        else if (ActiveStudent(actor) is { } student)
        {
            if (CanContinueLesson(actor))
            {
                candidates.Add(new("lesson_teach:" + student.AgentId, "Continue the agreed practical lesson.", 17));
            }
            candidates.Add(new("lesson_decline:" + student.AgentId, "Stop the lesson without granting the skill.", 110));
        }
        else
        {
            foreach (var request in agents.Values.Where(item => item.Lesson is { Stage: "requested" } lesson && lesson.TeacherId == actor))
            {
                if (MayAcceptLesson(actor, request.AgentId, request.Lesson!))
                    candidates.Add(new("lesson_accept:" + request.AgentId,
                        $"Teach {society.Checkpoint.GetAgent(request.AgentId).Name} the requested {request.Lesson!.Skill.ToString().ToLowerInvariant()} skill.", 17));
                candidates.Add(new("lesson_decline:" + request.AgentId, "Decline this teaching request.", 70));
            }
            if (IsAdult(actor) &&
                (person.Lesson is null || WorldTick - person.Lesson.LastTransitionTick >= 300))
            {
                foreach (var skill in Enum.GetValues<SkillKind>().Where(skill => !HasSkill(actor, skill)))
                {
                    var mentor = agents.Keys.Order(StringComparer.Ordinal).FirstOrDefault(id => MayRequestLesson(actor, id, skill));
                    if (mentor is not null)
                    {
                        candidates.Add(new($"learn:{skill.ToString().ToLowerInvariant()}:{mentor}",
                            $"Ask {society.Checkpoint.GetAgent(mentor).Name} to teach {skill.ToString().ToLowerInvariant()}; they may refuse.", 35));
                    }
                }
            }
        }
    }

    private void ApplyLearningCandidate(string actor, string candidate, ActionPlan? originalPlan = null)
    {
        var plan = originalPlan as LearningActionPlan ?? (LearningChoice.Decode(candidate) is { } choice ? PlanLearningAction(actor, choice) : null);
        if (plan is not null) ApplyLearningActionPlan(plan);
    }

    private void CompleteLearningAction(LearningActionPlan plan)
    {
        if (plan.Choice.Kind == LearningChoiceKind.Request)
        {
            SetLesson(plan.StudentId, new(plan.TeacherId, plan.Skill, "requested", 0, WorldTick, WorldTick));
            checkpointSchemaVersion = StateSchemaVersion;
            return;
        }
        if (plan.Choice.Kind is LearningChoiceKind.Attend or LearningChoiceKind.Teach)
        {
            ContinueLesson(plan.Actor, plan.WaitingForModel, plan);
            return;
        }
        SetLesson(plan.StudentId, plan.Original! with
        {
            Stage = plan.Choice.Kind switch
            { LearningChoiceKind.Accept => "accepted", LearningChoiceKind.Cancel => "cancelled", _ => "declined" }
        });
        if (plan.Choice.Kind == LearningChoiceKind.Accept)
            foreach (var participant in new[] { plan.TeacherId, plan.StudentId })
                if (agents[participant].Project is { Stage: not ("completed" or "cancelled") } project)
                    SetProject(participant, project with { Stage = "paused", Blocker = "Taking part in an agreed lesson" });
    }

    private void ContinueLesson(string actor, bool waitingForModel = false, LearningActionPlan? originalPlan = null)
    {
        if (!agents.TryGetValue(actor, out var person)) return;
        var choice = person.Lesson is { Stage: "accepted" or "training" }
            ? new LearningChoice(LearningChoiceKind.Attend) : new LearningChoice(LearningChoiceKind.Teach, ActiveStudent(actor)?.AgentId);
        var plan = originalPlan ?? PlanLearningAction(actor, choice, waitingForModel);
        if (plan is null || !CheckActionPlan(plan).Allowed) return;
        var storagePosition = plan.Site;
        RecordAttemptedAction(actor, plan.CandidateId);
        if (!IsWithinInteractionRange(person.Position, storagePosition, ResourceInteractionRange))
        {
            MoveToward(actor, person, storagePosition, "lesson", ResourceInteractionRange);
            return;
        }
        if (plan.TeacherId != actor || !agents.TryGetValue(plan.StudentId, out var student) ||
            !IsWithinInteractionRange(student.Position, storagePosition, ResourceInteractionRange))
        {
            return;
        }
        var previousLesson = student.Lesson!;
        var lesson = previousLesson with { Progress = previousLesson.Progress + 1, Stage = "training" };
        if (lesson.Progress >= LessonWorkRequired)
        {
            lesson = lesson with { Stage = "completed" };
            GainSkill(student.AgentId, lesson.Skill, actor);
            IncreaseTrust(student.AgentId, actor, 2, "teaching_completed");
            var memoryId = LessonGratitudeMemoryId(student.AgentId, actor, lesson.Skill);
            if (!society.Checkpoint.Memories.Any(memory => memory.Id == memoryId))
            {
                society.Apply(checkpoint => SocietyRules.RecordSocialMemory(checkpoint, new(memoryId, student.AgentId, actor,
                    $"Learned {lesson.Skill.ToString().ToLowerInvariant()} with {checkpoint.GetAgent(actor).Name}.", "public", WorldTick)
                { Kind = SocietyMemoryKind.Skill }));
            }
        }
        SetLesson(student.AgentId, lesson);
    }

    private void SetLesson(string student, SkillLesson lesson)
    {
        var person = agents[student];
        if (person.Lesson?.Stage != lesson.Stage)
        {
            lesson = lesson with { LastTransitionTick = WorldTick };
            AppendEvent("lesson_" + lesson.Stage, student);
        }
        agents[student] = person with { Lesson = lesson };
    }

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
            if (person.Lesson is { } lesson && (state.SchemaVersion < SkillsSchemaVersion || !known.Contains(lesson.TeacherId) ||
                lesson.TeacherId == person.AgentId || !Enum.IsDefined(lesson.Skill) ||
                lesson.Stage is not ("requested" or "accepted" or "training" or "completed" or "cancelled" or "declined") ||
                lesson.Progress is < 0 or > LessonWorkRequired || lesson.RequestedTick < 0 ||
                lesson.Stage is "requested" or "accepted" && lesson.Progress != 0 ||
                lesson.Stage == "training" && lesson.Progress is <= 0 or >= LessonWorkRequired ||
                lesson.Stage == "completed" && lesson.Progress != LessonWorkRequired ||
                lesson.LastTransitionTick < lesson.RequestedTick || lesson.LastTransitionTick > state.Society.Society.WorldTick))
            {
                throw new InvalidDataException("The saved apprenticeship is invalid.");
            }
        }
    }
}
