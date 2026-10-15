namespace ClankerWorld.Simulation.World;

internal sealed partial class CommunitySystem
{
    internal static ActorCapabilityPolicy LearningCapabilities =>
        new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true);

    internal static LearningActionPlan? PlanLearningAction(ICommunityLearningWorld world, string actor, LearningChoice choice, bool waitingForModel = false)
    {
        if (!world.PhysicalAgents.Living.ContainsKey(actor)) return null;
        var student = choice.Kind switch
        {
            LearningChoiceKind.Accept or LearningChoiceKind.Decline or LearningChoiceKind.Teach => choice.TargetId,
            _ => actor,
        };
        if (student is null || !world.PhysicalAgents.Living.TryGetValue(student, out var person)) return null;
        var original = person.Lesson;
        var teacher = choice.Kind == LearningChoiceKind.Request ? choice.TargetId : original?.TeacherId;
        var skill = choice.Kind == LearningChoiceKind.Request ? choice.Skill : original?.Skill;
        if (teacher is null || skill is null || !world.PhysicalAgents.Living.ContainsKey(teacher)) return null;
        var site = world.SharedStoragePosition;
        var other = actor == student ? teacher : student;
        return new(actor, choice, student, teacher, skill.Value, original, site,
            world.Reach(actor, site, ReachPurpose.Learning, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable),
            world.Reach(other, site, ReachPurpose.Learning, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable), waitingForModel);
    }

    internal static bool MayRequestLesson(ICommunityLearningWorld world, string student, string teacher, SkillKind skill) => student != teacher &&
        world.IsAdult(student) && !world.HasSkill(student, skill) && ReadyForLesson(world, student) && !ActiveLesson(world.PhysicalAgents.Living[student].Lesson) &&
        (world.PhysicalAgents.Living[student].Lesson is null || world.WorldTick - world.PhysicalAgents.Living[student].Lesson!.LastTransitionTick >= 300) &&
        CanMentor(world, teacher, skill) && FreeToMentor(world, teacher) &&
        world.CanReach(student, world.SharedStoragePosition, ReachPurpose.Learning, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable) &&
        world.CanReach(teacher, world.SharedStoragePosition, ReachPurpose.Learning, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable);

    internal static bool MayAcceptLesson(ICommunityLearningWorld world, string teacher, string student, SkillLesson lesson) => lesson.Stage == "requested" &&
        ReadyForLesson(world, student) && CanMentor(world, teacher, lesson.Skill) && FreeToMentor(world, teacher, student) &&
        world.CanReach(teacher, world.SharedStoragePosition, ReachPurpose.Learning, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable) &&
        world.CanReach(student, world.SharedStoragePosition, ReachPurpose.Learning, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable);

    internal static ActionCheck CheckLearningActionPlan(ICommunityLearningWorld world, LearningActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? LearningCapabilities);
        if (!capability.Allowed) return capability;
        if (!world.PhysicalAgents.Living.TryGetValue(plan.StudentId, out var student) || !world.PhysicalAgents.Living.ContainsKey(plan.TeacherId) || world.SharedStoragePosition != plan.Site)
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
            LearningChoiceKind.Request => MayRequestLesson(world, plan.StudentId, plan.TeacherId, plan.Skill),
            LearningChoiceKind.Accept => current is { } lesson && plan.Actor == plan.TeacherId && MayAcceptLesson(world, plan.TeacherId, plan.StudentId, lesson),
            LearningChoiceKind.Decline => current is { } lesson && ActiveLesson(lesson) && plan.Actor == plan.TeacherId,
            LearningChoiceKind.Cancel => ActiveLesson(current) && plan.Actor == plan.StudentId,
            _ => CanContinueLesson(world, plan.Actor, plan.WaitingForModel) &&
                (plan.Actor == plan.StudentId || ActiveStudent(world, plan.Actor)?.AgentId == plan.StudentId),
        };
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that lesson choice is no longer available");
    }

}
