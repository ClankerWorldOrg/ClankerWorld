using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;

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
        var sameRequest = current is not null && plan.Original is { } original &&
            current.TeacherId == original.TeacherId && current.Skill == original.Skill && current.RequestedTick == original.RequestedTick;
        var sameLesson = plan.Choice.Kind switch
        {
            LearningChoiceKind.Attend or LearningChoiceKind.Teach => sameRequest && current is { Stage: "accepted" or "training" },
            LearningChoiceKind.Cancel or LearningChoiceKind.Decline => sameRequest && ActiveLesson(current),
            _ => current == plan.Original,
        };
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
    internal int LearnArtifactFacts(
        string recipientId,
        string sourceAgentId,
        AgentKnowledgeArtifact artifact,
        string acquisition, long tick)
    {
        var facts = State.Facts.ToList();
        var learned = 0;
        foreach (var fact in artifact.Facts)
        {
            if (facts.Any(existing => existing.OwnerId == recipientId && existing.Position == fact.Position) ||
                facts.Count(existing => existing.OwnerId == recipientId) >= AgentKnowledgeRules.MaximumFactsPerAgent)
                continue;

            facts.Add(fact with
            {
                Id = KnowledgeFactId(recipientId, fact.Position),
                OwnerId = recipientId,
                LearnedTick = tick,
                Acquisition = acquisition,
                SourceAgentId = sourceAgentId,
                SourceArtifactId = artifact.Id,
            });
            learned++;
        }
        if (learned > 0)
        {
            Commit(State with { Facts = facts.ToArray() });
        }
        return learned;
    }

    internal static string KnowledgeFactId(string ownerId, GridPoint position)
    {
        // Existing short IDs stay unchanged. Descendant IDs contain ancestry and
        // can exceed the ledger's limit; hash the complete key, never truncate it.
        return DerivedId.Create($"knowledge-fact:{ownerId}:{position.X}:{position.Y}", "knowledge-fact-sha256:");
    }

    internal int LearnArtifactRecipes(string actor, string sourceActor, AgentKnowledgeArtifact artifact, string acquisition, long tick)
    {
        var recipes = State.Recipes.ToList();
        var count = 0;
        foreach (var source in artifact.Recipes)
        {
            if (recipes.Any(item => item.OwnerId == actor && item.RecipeId == source.RecipeId) ||
                recipes.Count(item => item.OwnerId == actor) >= AgentKnowledgeRules.MaximumRecipesPerAgent) continue;
            recipes.Add(source with
            {
                Id = RecipeKnowledgeId(actor, source.RecipeId),
                OwnerId = actor,
                LearnedTick = tick,
                Acquisition = acquisition,
                SourceAgentId = sourceActor,
                SourceArtifactId = artifact.Id,
            });
            count++;
        }
        if (count > 0) Commit(State with { Recipes = recipes.ToArray() });
        return count;
    }

    internal static string RecipeKnowledgeId(string actor, string recipeId) =>
        DerivedId.Hash($"{actor}|{recipeId}", "recipe-knowledge:");
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
        world.AppendEvent(new SkillLearnedEventPayload(actor, skill.ToString().ToLowerInvariant(), teacher ?? "work"));
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
            world.AppendEvent(new WorkPracticeEarnedEventPayload(actor));
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
    internal static int TrustScore(ICommunitySkillWorld world, string owner, string subject)
    {
        var saved = world.PhysicalAgents.Living[owner].SocialStanding?.FirstOrDefault(item => item.SubjectId == subject);
        if (saved is not null)
        {
            return saved.Trust;
        }

        // Durable cooperation memories provide the projection until a direct
        // trust transition materializes this bounded snapshot.
        return Math.Min(10, world.Society.Memories.Where(memory =>
                memory.OwnerId == owner && memory.SubjectId == subject && memory.TombstonedTick is null)
            .Sum(memory => memory.Id.Value.StartsWith("project-gratitude:", StringComparison.Ordinal) ? 2
                : memory.Id.Value.StartsWith("lesson-gratitude:", StringComparison.Ordinal) ? 2
                : memory.Id.Value.StartsWith("trust-memory:", StringComparison.Ordinal) ? 1 : 0));
    }

    internal static void IncreaseTrust(ICommunitySkillWorld world, string owner, string subject, int amount, string reason)
    {
        if (owner == subject || amount <= 0 || !world.PhysicalAgents.Living.TryGetValue(owner, out var person) || !world.PhysicalAgents.Living.ContainsKey(subject))
        {
            return;
        }

        var previous = TrustScore(world, owner, subject);
        var next = Math.Min(10, checked(previous + amount));
        if (next == previous)
        {
            return;
        }

        var standing = (person.SocialStanding ?? []).Where(item => item.SubjectId != subject)
            .Append(new SocialStandingEntry(subject, next, world.WorldTick))
            .OrderBy(item => item.SubjectId, StringComparer.Ordinal).ToArray();
        world.PhysicalAgents.RecordSocialStanding(owner, person, standing);
        world.MarkCurrentSchema();
        world.AppendEvent(new SocialStandingChangedEventPayload(owner, subject, reason));
    }

    internal static void ValidateLocalStanding(string actor, IReadOnlyList<SocialStandingEntry> standing)
    {
        if (standing.Select(item => item.SubjectId).Distinct(StringComparer.Ordinal).Count() != standing.Count ||
            standing.Any(item => item.SubjectId == actor || string.IsNullOrWhiteSpace(item.SubjectId) ||
                item.Trust is < 1 or > 10 || item.LastChangedTick < 0))
            throw new InvalidDataException("Community social standing has invalid local state.");
    }
}
