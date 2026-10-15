using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private LearningActionPlan? PlanLearningAction(string actor, LearningChoice choice, bool waitingForModel = false)
    {
        if (!agents.ContainsKey(actor)) return null;
        var student = choice.Kind switch
        {
            LearningChoiceKind.Accept or LearningChoiceKind.Decline or LearningChoiceKind.Teach => choice.TargetId,
            _ => actor,
        };
        if (student is null || !agents.TryGetValue(student, out var person)) return null;
        var original = person.Lesson;
        var teacher = choice.Kind == LearningChoiceKind.Request ? choice.TargetId : original?.TeacherId;
        var skill = choice.Kind == LearningChoiceKind.Request ? choice.Skill : original?.Skill;
        if (teacher is null || skill is null || !agents.ContainsKey(teacher)) return null;
        var site = SharedStoragePosition;
        var other = actor == student ? teacher : student;
        return new(actor, choice, student, teacher, skill.Value, original, site,
            Reach(actor, site, ReachPurpose.Learning, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable),
            Reach(other, site, ReachPurpose.Learning, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable), waitingForModel);
    }

    private bool MayRequestLesson(string student, string teacher, SkillKind skill) => student != teacher &&
        IsAdult(student) && !HasSkill(student, skill) && ReadyForLesson(student) && !ActiveLesson(agents[student].Lesson) &&
        (agents[student].Lesson is null || WorldTick - agents[student].Lesson!.LastTransitionTick >= 300) &&
        CanMentor(teacher, skill) && FreeToMentor(teacher) &&
        CanReach(student, SharedStoragePosition, ReachPurpose.Learning, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable) &&
        CanReach(teacher, SharedStoragePosition, ReachPurpose.Learning, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable);

    private bool MayAcceptLesson(string teacher, string student, SkillLesson lesson) => lesson.Stage == "requested" &&
        ReadyForLesson(student) && CanMentor(teacher, lesson.Skill) && FreeToMentor(teacher, student) &&
        CanReach(teacher, SharedStoragePosition, ReachPurpose.Learning, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable) &&
        CanReach(student, SharedStoragePosition, ReachPurpose.Learning, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable);

    private ActionCheck CheckLearningActionPlan(LearningActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? LearningCapabilities);
        if (!capability.Allowed) return capability;
        if (!agents.TryGetValue(plan.StudentId, out var student) || !agents.ContainsKey(plan.TeacherId) || SharedStoragePosition != plan.Site)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected student, teacher or lesson site has changed");
        var current = student.Lesson;
        var continuing = plan.Choice.Kind is LearningChoiceKind.Attend or LearningChoiceKind.Teach;
        var sameLesson = continuing
            ? current is { Stage: "accepted" or "training" } && plan.Original is { } original &&
                current.TeacherId == original.TeacherId && current.Skill == original.Skill && current.RequestedTick == original.RequestedTick
            : current == plan.Original;
        if (!sameLesson)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected lesson request or agreement has changed");
        var allowed = plan.Choice.Kind switch
        {
            LearningChoiceKind.Request => MayRequestLesson(plan.StudentId, plan.TeacherId, plan.Skill),
            LearningChoiceKind.Accept => current is { } lesson && plan.Actor == plan.TeacherId && MayAcceptLesson(plan.TeacherId, plan.StudentId, lesson),
            LearningChoiceKind.Decline => current is { } lesson && ActiveLesson(lesson) && plan.Actor == plan.TeacherId,
            LearningChoiceKind.Cancel => ActiveLesson(current) && plan.Actor == plan.StudentId,
            _ => CanContinueLesson(plan.Actor, plan.WaitingForModel) &&
                (plan.Actor == plan.StudentId || ActiveStudent(plan.Actor)?.AgentId == plan.StudentId),
        };
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that lesson choice is no longer available");
    }

    private void AddLearningCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var options = new List<CognitionCandidate>();
        AddLearningCandidateOptions(options, actor);
        foreach (var candidate in options)
            if (LearningChoice.Decode(candidate.Id) is { } choice && LearningDefinition.Offer(this, actor, choice) is { } plan)
                candidates.Add(candidate with { ActionPlan = plan });
    }

    private void ApplyLearningActionPlan(LearningActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteLearningAction(plan);
    }
}
