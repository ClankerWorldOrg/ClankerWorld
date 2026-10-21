using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CommunitySystem
{
    internal const int LessonWorkRequired = 20;

    internal static bool ActiveLesson(SkillLesson? lesson) => lesson?.Stage is "requested" or "accepted" or "training";

    internal static bool ReadyForLesson(ICommunityLearningWorld world, string actor) => world.PhysicalAgents.Living.TryGetValue(actor, out var person) &&
        !world.NeedsUrgentFood(person) && !world.NeedsUrgentWarmth(person);

    internal static bool CanMentor(ICommunityLearningWorld world, string teacher, SkillKind skill) =>
        world.IsAdult(teacher) && !ActiveLesson(world.PhysicalAgents.Living[teacher].Lesson) && world.HasSkill(teacher, skill);

    internal static bool FreeToMentor(ICommunityLearningWorld world, string teacher, string? requestedStudent = null) =>
        ReadyForLesson(world, teacher) && world.PendingInstructionFor(teacher) is null &&
        !world.IsConversationBusy(teacher) && ActiveStudent(world, teacher) is null &&
        world.PhysicalAgents.Living[teacher].Project is null or { Stage: "completed" or "cancelled" or "paused" } &&
        !world.HasCompetingLessonDecision(teacher) &&
        !world.PhysicalAgents.Living.Values.Any(item => item.AgentId != requestedStudent &&
            ActiveLesson(item.Lesson) && item.Lesson!.TeacherId == teacher);

    internal static AgentState? ActiveStudent(ICommunityLearningWorld world, string teacher) => world.PhysicalAgents.Living.Values.FirstOrDefault(person =>
        person.Lesson is { Stage: "accepted" or "training" } lesson && lesson.TeacherId == teacher);

    internal static bool HasLearningDecision(ICommunityLearningWorld world, string actor) => ReadyForLesson(world, actor) && world.PhysicalAgents.Living.Values.Any(person =>
        person.Lesson is { Stage: "requested" } lesson && lesson.TeacherId == actor);

    internal static bool CanContinueLesson(ICommunityLearningWorld world, string actor, bool waitingForModel = false)
    {
        if (!world.IsAdult(actor) || !ReadyForLesson(world, actor) || world.IsConversationBusy(actor) ||
            !waitingForModel && world.HasCompetingLessonDecision(actor))
        {
            return false;
        }
        if (world.PhysicalAgents.Living[actor].Lesson is { Stage: "accepted" or "training" } lesson)
        {
            return ReadyForLesson(world, lesson.TeacherId) && world.IsAdult(lesson.TeacherId) &&
                !world.IsConversationBusy(lesson.TeacherId) &&
                (!world.HasHousingDecision(lesson.TeacherId) || world.ModelDecisionPending(lesson.TeacherId));
        }
        return ActiveStudent(world, actor) is { } student && world.IsAdult(student.AgentId) &&
            ReadyForLesson(world, student.AgentId) && !world.IsConversationBusy(student.AgentId) &&
            (!world.HasHousingDecision(student.AgentId) || world.ModelDecisionPending(student.AgentId));
    }

    internal static void MaintainLessons(ICommunityLearningWorld world)
    {
        foreach (var person in world.PhysicalAgents.Living.Values.ToArray())
        {
            if (person.Lesson is not { } lesson || !ActiveLesson(lesson))
            {
                continue;
            }
            if (!world.IsAdult(person.AgentId) || !CanMentor(world, lesson.TeacherId, lesson.Skill) ||
                world.WorldTick - lesson.RequestedTick > (lesson.Stage == "requested" ? 120 : 600))
            {
                SetLesson(world, person.AgentId, lesson with { Stage = "cancelled" });
            }
        }
    }

    internal static void AddLearningCandidateOptions(ICommunityLearningWorld world, List<CognitionCandidate> candidates, string actor)
    {
        if (!ReadyForLesson(world, actor) || !world.HasSurvivalState)
        {
            return;
        }
        var person = world.PhysicalAgents.Living[actor];
        if (ActiveLesson(person.Lesson))
        {
            if (person.Lesson!.Stage is "accepted" or "training" && CanContinueLesson(world, actor))
            {
                candidates.Add(new("lesson_attend", "Attend the agreed practical lesson.", 18));
            }
            candidates.Add(new("lesson_cancel", "Withdraw from the requested or ongoing lesson.", 110));
        }
        else if (ActiveStudent(world, actor) is { } student)
        {
            if (CanContinueLesson(world, actor))
            {
                candidates.Add(new(new LearningChoice(LearningChoiceKind.Teach, student.AgentId).Encode(), "Continue the agreed practical lesson.", 17));
            }
            candidates.Add(new(new LearningChoice(LearningChoiceKind.Decline, student.AgentId).Encode(), "Stop the lesson without granting the skill.", 110));
        }
        else
        {
            foreach (var request in world.PhysicalAgents.Living.Values.Where(item => item.Lesson is { Stage: "requested" } lesson && lesson.TeacherId == actor))
            {
                if (MayAcceptLesson(world, actor, request.AgentId, request.Lesson!))
                    candidates.Add(new(new LearningChoice(LearningChoiceKind.Accept, request.AgentId).Encode(),
                        $"Teach {world.Society.GetAgent(request.AgentId).Name} the requested {request.Lesson!.Skill.ToString().ToLowerInvariant()} skill.", 17));
                candidates.Add(new(new LearningChoice(LearningChoiceKind.Decline, request.AgentId).Encode(), "Decline this teaching request.", 70));
            }
            if (world.IsAdult(actor) &&
                (person.Lesson is null || world.WorldTick - person.Lesson.LastTransitionTick >= 300))
            {
                foreach (var skill in Enum.GetValues<SkillKind>().Where(skill => !world.HasSkill(actor, skill)))
                {
                    var mentor = world.PhysicalAgents.Living.Keys.Order(StringComparer.Ordinal).FirstOrDefault(id => MayRequestLesson(world, actor, id, skill));
                    if (mentor is not null)
                    {
                        candidates.Add(new(new LearningChoice(LearningChoiceKind.Request, mentor, skill).Encode(),
                            $"Ask {world.Society.GetAgent(mentor).Name} to teach {skill.ToString().ToLowerInvariant()}; they may refuse.", 35));
                    }
                }
            }
        }
    }

    internal static void CompleteLearningAction(ICommunityLearningWorld world, LearningActionPlan plan)
    {
        if (plan.Choice.Kind == LearningChoiceKind.Request)
        {
            SetLesson(world, plan.StudentId, new(plan.TeacherId, plan.Skill, "requested", 0, world.WorldTick, world.WorldTick));
            world.MarkCurrentSchema();
            return;
        }
        if (plan.Choice.Kind is LearningChoiceKind.Attend or LearningChoiceKind.Teach)
        {
            ContinueLesson(world, plan.Actor, plan.WaitingForModel, plan);
            return;
        }
        SetLesson(world, plan.StudentId, world.PhysicalAgents.Living[plan.StudentId].Lesson! with
        {
            Stage = plan.Choice.Kind switch
            { LearningChoiceKind.Accept => "accepted", LearningChoiceKind.Cancel => "cancelled", _ => "declined" }
        });
        if (plan.Choice.Kind == LearningChoiceKind.Accept)
            foreach (var participant in new[] { plan.TeacherId, plan.StudentId })
                if (world.PhysicalAgents.Living[participant].Project is { Stage: not ("completed" or "cancelled") } project)
                    world.PauseProjectForLesson(participant, project);
    }

    internal static void ContinueLesson(ICommunityLearningWorld world, string actor, bool waitingForModel = false, LearningActionPlan? originalPlan = null)
    {
        if (!world.PhysicalAgents.Living.TryGetValue(actor, out var person)) return;
        var choice = person.Lesson is { Stage: "accepted" or "training" }
            ? new LearningChoice(LearningChoiceKind.Attend) : new LearningChoice(LearningChoiceKind.Teach, ActiveStudent(world, actor)?.AgentId);
        var plan = originalPlan ?? PlanLearningAction(world, actor, choice, waitingForModel);
        if (plan is null || !world.CheckActionPlan(plan).Allowed) return;
        var storagePosition = plan.Site;
        world.RecordAttemptedAction(actor, plan.CandidateId);
        if (!world.IsWithinInteractionRange(person.Position, storagePosition, world.ResourceInteractionRange))
        {
            world.MoveToward(actor, person, storagePosition, "lesson", world.ResourceInteractionRange);
            return;
        }
        if (plan.TeacherId != actor || !world.PhysicalAgents.Living.TryGetValue(plan.StudentId, out var student) ||
            !world.IsWithinInteractionRange(student.Position, storagePosition, world.ResourceInteractionRange))
        {
            return;
        }
        var previousLesson = student.Lesson!;
        var lesson = previousLesson with { Progress = previousLesson.Progress + 1, Stage = "training" };
        if (lesson.Progress >= LessonWorkRequired)
        {
            lesson = lesson with { Stage = "completed" };
            world.GainSkill(student.AgentId, lesson.Skill, actor);
            world.IncreaseTrust(student.AgentId, actor, 2, "teaching_completed");
            world.RecordLessonGratitude(student.AgentId, actor, lesson.Skill);
        }
        SetLesson(world, student.AgentId, lesson);
    }

    internal static void SetLesson(ICommunityLearningWorld world, string student, SkillLesson lesson)
    {
        var person = world.PhysicalAgents.Living[student];
        var changed = person.Lesson?.Stage != lesson.Stage;
        if (changed) lesson = lesson with { LastTransitionTick = world.WorldTick };
        if (SystemInvariantChecks.Enabled) ValidateLocalLesson(lesson);
        if (changed) world.AppendEvent(WorldEvents.Lesson(lesson.Stage, student));
        world.PhysicalAgents.RecordLesson(student, person, lesson);
    }

    internal static void ValidateLocalLesson(SkillLesson lesson)
    {
        if (!Enum.IsDefined(lesson.Skill) || lesson.Stage is not ("requested" or "accepted" or "training" or "completed" or "cancelled" or "declined") ||
            lesson.Progress is < 0 or > LessonWorkRequired || lesson.RequestedTick < 0 ||
            lesson.Stage is "requested" or "accepted" && lesson.Progress != 0 ||
            lesson.Stage == "training" && lesson.Progress is <= 0 or >= LessonWorkRequired ||
            lesson.Stage == "completed" && lesson.Progress != LessonWorkRequired || lesson.LastTransitionTick < lesson.RequestedTick)
            throw new InvalidDataException("Community lesson has an invalid local state.");
    }
}
