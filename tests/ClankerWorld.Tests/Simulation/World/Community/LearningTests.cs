using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed partial class LearningTests
{
    [Theory]
    [InlineData("unskilled", false)]
    [InlineData("hungry", false)]
    [InlineData("busy", false)]
    public async Task OnlyAFreeReadySkilledMentorIsOffered(string condition, bool expected)
    {
        var state = await PreparedState();
        const string teacher = "founder-ilya";
        var learner = state.Society.Society.Agents.Single(person => person.CurrentRole == SocietyWorkRole.Trader).Id;
        var building = state.WorldContent!.Buildings[0];
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == teacher ? person with
            {
                Skills = condition == "unskilled" ? null : person.Skills,
                FullnessBasisPoints = condition == "hungry" ? 1_000 : 9_000,
                Project = condition == "busy" ? new("build:building:" + building.CanonicalId, building.DisplayName,
                    state.Society.Society.WorldTick, "acquiring", LastTransitionTick: state.Society.Society.WorldTick) : null,
            } : person).ToArray(),
        };
        var provider = new LessonProvider("safe_idle");
        using var world = WorldRuntime.Restore(state, id => id == learner ? provider : new LessonProvider("safe_idle"));
        await world.AdvanceOneTickAsync();
        Assert.Equal(expected, provider.CandidateIds.Contains("learn:building:" + teacher));
        Assert.Equal(SocietyWorkRole.Unassigned, world.Society.GetAgent(teacher).CurrentRole);
    }

    [Fact]
    public async Task SavedSkillsRejectUnknownTeachersAndFutureLearningTimes()
    {
        var state = await PreparedState();
        using var valid = WorldRuntime.Restore(state);
        var actor = state.Agents[0].AgentId;
        foreach (var skill in new[]
        {
            new LearnedSkill(SkillKind.Building, state.Society.Society.WorldTick, "missing-teacher"),
            new LearnedSkill(SkillKind.Building, state.Society.Society.WorldTick + 1),
        })
        {
            Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(state with
            {
                Agents = state.Agents.Select(person => person.AgentId == actor
                    ? person with { Skills = [skill] } : person).ToArray(),
            }));
        }
    }

    [Fact]
    public async Task SimultaneousDeclineAndCancelDoesNotHaltTheRuntimeService()
    {
        var directory = Directory.CreateTempSubdirectory("lesson-terminal-");
        try
        {
            using var world = WorldRuntime.Restore(await PreparedState(), actor => new CancellationRaceProvider(actor));
            var beforeRole = world.Society.GetAgent("founder-scout").CurrentRole;
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            file.Save(world);
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(5));
            presence.RecordAuthenticatedReconnect("test-owner");
            using var service = new WorldRuntimeService(world, file, presence);
            for (var tick = 0; tick < 3; tick++) Assert.True(await service.AdvanceWithModelWorkAsync(world));
            Assert.Equal("declined", world.Agents.Single(person => person.AgentId == "founder-scout").Lesson!.Stage);
            Assert.Equal(beforeRole, world.Society.GetAgent("founder-scout").CurrentRole);
            Assert.False(world.Society.IsPaused);
            Assert.True(await service.AdvanceWithModelWorkAsync(world));
            using var reloaded = file.LoadOrCreate(world.ExportState().WorldSeed);
            Assert.Equal(world.WorldTick, reloaded.WorldTick);
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed class CancellationRaceProvider(string actor) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidates = request.Observation.Candidates;
            var choice = actor == "founder-scout"
                ? candidates.FirstOrDefault(item => item.Id == "lesson_cancel") ?? candidates.FirstOrDefault(item => item.Id == "learn:farming:founder-ilya")
                : actor == "founder-ilya" ? candidates.FirstOrDefault(item => item.Id == "lesson_decline:founder-scout") : null;
            choice ??= candidates.Single(item => item.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with { Observation = request.Observation with { Candidates = [choice] } }, cancellationToken);
        }
    }

    [Fact]
    public async Task BusyMentorCannotAcceptUntilTheirWorkStops()
    {
        var state = await PreparedState();
        var learner = state.Society.Society.Agents.Single(person => person.CurrentRole == SocietyWorkRole.Trader).Id;
        using var requesting = WorldRuntime.Restore(state, actor => new LessonProvider(actor == learner ? "learn:building:" : "safe_idle"));
        await requesting.AdvanceOneTickAsync();
        state = requesting.ExportState();
        var teacher = state.Agents.Single(person => person.AgentId == learner).Lesson!.TeacherId;
        var definition = state.WorldContent!.Buildings[0];
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == teacher ? person with
            {
                Project = new("build:building:" + definition.CanonicalId, definition.DisplayName, requesting.WorldTick, "acquiring",
                LastTransitionTick: requesting.WorldTick),
            } : person).ToArray()
        };
        using var world = WorldRuntime.Restore(state, _ => new LessonProvider("lesson_accept:"));
        await world.AdvanceOneTickAsync();
        Assert.Equal("requested", world.Agents.Single(person => person.AgentId == learner).Lesson!.Stage);
        Assert.Empty(world.Agents.Single(person => person.AgentId == learner).Skills ?? []);
    }

    [Fact]
    public async Task AcceptedTrainingSurvivesPauseAndRestartAndSavesTheSkillAndTeacher()
    {
        var state = await PreparedState();
        var learner = state.Society.Society.Agents.Single(person => person.CurrentRole == SocietyWorkRole.Trader).Id;
        IDecisionProvider Provider(string actor) => new LessonProvider(actor == learner ? "learn:building:" : "lesson_accept:");
        using var world = WorldRuntime.Restore(state, Provider);
        await world.AdvanceOneTickAsync();
        Assert.Equal("requested", world.Agents.Single(person => person.AgentId == learner).Lesson!.Stage);
        Assert.Equal(SocietyWorkRole.Trader, world.Society.GetAgent(learner).CurrentRole);
        for (var tick = 0; tick < 80 && world.Agents.Single(person => person.AgentId == learner).Lesson!.Progress < 3; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        var lesson = world.Agents.Single(person => person.AgentId == learner).Lesson!;
        Assert.InRange(lesson.Progress, 3, 19);
        Assert.Equal(SocietyWorkRole.Trader, world.Society.GetAgent(learner).CurrentRole);
        Assert.Equal(lesson.Progress, new OwnerWorldObservationStore(world).GetSnapshot().Agents.Single(person => person.Id == learner).Lesson!.Progress);
        world.Pause();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), Provider);
        restored.Resume();
        for (var tick = 0; tick < 100 && restored.Agents.Single(person => person.AgentId == learner).Lesson!.Stage != "completed"; tick++)
        {
            await restored.AdvanceOneTickAsync();
        }
        Assert.Equal(SocietyWorkRole.Trader, restored.Society.GetAgent(learner).CurrentRole);
        Assert.Equal("completed", restored.Agents.Single(person => person.AgentId == learner).Lesson!.Stage);
        Assert.Contains(restored.Society.Memories, memory => memory.OwnerId == learner && memory.Id.Value.StartsWith("lesson-gratitude:", StringComparison.Ordinal));
        var completed = restored.Agents.Single(person => person.AgentId == learner);
        var skill = Assert.Single(completed.Skills!);
        Assert.Equal(SkillKind.Building, skill.Kind);
        Assert.Equal(completed.Lesson!.TeacherId, skill.TeacherId);
        Assert.Equal(completed.Lesson.LastTransitionTick, skill.LearnedTick);
        Assert.Null(completed.Proficiency);
        var shown = Assert.Single(new OwnerWorldObservationStore(restored).GetSnapshot().Agents.Single(person => person.Id == learner).Skills);
        Assert.Equal(skill.TeacherId, shown.TeacherId);
        Assert.Equal(restored.Society.GetAgent(skill.TeacherId!).Name, shown.TeacherName);
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var wire = System.Text.Json.JsonSerializer.Serialize(new OwnerWorldObservationStore(restored).GetSnapshot(), options);
        using (var document = System.Text.Json.JsonDocument.Parse(wire))
        {
            var wireLesson = document.RootElement.GetProperty("agents").EnumerateArray()
                .Single(person => person.GetProperty("id").GetString() == learner).GetProperty("lesson");
            Assert.Equal("building", wireLesson.GetProperty("role").GetString());
            Assert.False(wireLesson.TryGetProperty("skill", out _));
        }
        var client = System.Text.Json.JsonSerializer.Deserialize<ClankerWorld.Contracts.OwnerWorldSnapshot>(wire, options)!;
        Assert.Equal("building", client.Agents.Single(person => person.Id == learner).Lesson!.Skill);
        Assert.Equal(skill.TeacherId, Assert.Single(client.Agents.Single(person => person.Id == learner).Skills!).TeacherId);
        using var completedReload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(restored.ExportState())));
        Assert.Equal(completed.Skills, completedReload.Agents.Single(person => person.AgentId == learner).Skills);
        Assert.Equal(2, completed.SocialStanding!.Single(item => item.SubjectId == completed.Lesson!.TeacherId).Trust);
    }

    [Fact]
    public async Task MentorRefusalDoesNotGrantTheRequestedSkill()
    {
        var state = await PreparedState();
        var learner = state.Society.Society.Agents.Single(person => person.CurrentRole == SocietyWorkRole.Trader).Id;
        using var world = WorldRuntime.Restore(state, actor => new LessonProvider(actor == learner ? "learn:building:" : "lesson_decline:"));
        for (var tick = 0; tick < 5; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        Assert.Equal("declined", world.Agents.Single(person => person.AgentId == learner).Lesson!.Stage);
        Assert.Equal(0, world.Agents.Single(person => person.AgentId == learner).Lesson!.Progress);
        Assert.Empty(world.Agents.Single(person => person.AgentId == learner).Skills ?? []);
        Assert.Equal(SocietyWorkRole.Trader, world.Society.GetAgent(learner).CurrentRole);
    }

    [Fact]
    public async Task MentorDeathCancelsTrainingWithoutGrantingAnUnearnedSkill()
    {
        var state = await PreparedState();
        var learner = state.Society.Society.Agents.Single(person => person.CurrentRole == SocietyWorkRole.Trader).Id;
        using var world = WorldRuntime.Restore(state, actor => new LessonProvider(actor == learner ? "learn:building:" : "lesson_accept:"));
        await world.AdvanceOneTickAsync();
        await world.AdvanceOneTickAsync();
        state = world.ExportState();
        var mentor = state.Agents.Single(person => person.AgentId == learner).Lesson!.TeacherId;
        using var society = SocietyRuntime.Restore(state.Society);
        society.Apply(checkpoint => SocietyRules.Kill(checkpoint, mentor, SocietyDeathCause.Accident, checkpoint.WorldTick));
        state = state with { Society = society.ExportState(), Agents = state.Agents.Where(person => person.AgentId != mentor).ToArray() };
        using var restored = WorldRuntime.Restore(state, _ => new LessonProvider("safe_idle"));
        await restored.AdvanceOneTickAsync();
        Assert.Equal("cancelled", restored.Agents.Single(person => person.AgentId == learner).Lesson!.Stage);
        Assert.Empty(restored.Agents.Single(person => person.AgentId == learner).Skills ?? []);
        Assert.Equal(SocietyWorkRole.Trader, restored.Society.GetAgent(learner).CurrentRole);
    }

    [Fact]
    public async Task HungerInterruptsTrainingWithoutLosingProgressOrGrantingTheSkill()
    {
        var state = await PreparedState();
        var learner = state.Society.Society.Agents.Single(person => person.CurrentRole == SocietyWorkRole.Trader).Id;
        using var world = WorldRuntime.Restore(state, actor => new LessonProvider(actor == learner ? "learn:building:" : "lesson_accept:"));
        for (var tick = 0; tick < 80 && world.Agents.Single(person => person.AgentId == learner).Lesson?.Progress is not >= 3; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        state = world.ExportState();
        var progress = state.Agents.Single(person => person.AgentId == learner).Lesson!.Progress;
        Assert.InRange(progress, 3, 19);
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == learner ? person with { FullnessBasisPoints = 1_000 } : person).ToArray(),
        };
        using var restored = WorldRuntime.Restore(state, _ => new LessonProvider("safe_idle"));
        for (var tick = 0; tick < 10; tick++)
        {
            await restored.AdvanceOneTickAsync();
        }
        Assert.Equal(progress, restored.Agents.Single(person => person.AgentId == learner).Lesson!.Progress);
        Assert.Empty(restored.Agents.Single(person => person.AgentId == learner).Skills ?? []);
        Assert.Equal(SocietyWorkRole.Trader, restored.Society.GetAgent(learner).CurrentRole);
    }

    [Fact]
    public async Task LessonJournalReportsBoundedStagesWithoutNames()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-lesson-log-");
        try
        {
            var state = await PreparedState();
            var learner = state.Society.Society.Agents.Single(person => person.CurrentRole == SocietyWorkRole.Trader).Id;
            state = state with
            {
                Society = state.Society with
                {
                    Society = state.Society.Society with
                    {
                        Agents = state.Society.Society.Agents.Select(person => person.Id == learner
                            ? person with { Name = "lesson-free-text-secret" } : person).ToArray(),
                    },
                },
            };
            using var world = WorldRuntime.Restore(state, actor => new LessonProvider(actor == learner ? "learn:building:" : "safe_idle"));
            var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(30));
            presence.RecordAuthenticatedReconnect("owner");
            var logger = new RecordingLogger<WorldRuntimeService>();
            using var service = new WorldRuntimeService(world, new WorldStateFile(Path.Combine(directory.FullName, "world.json")), presence, logger);
            Assert.True(await service.AdvanceWithModelWorkAsync(world));
            Assert.Contains(logger.Messages, message => message.Contains("lesson_activity", StringComparison.Ordinal) &&
                message.Contains("event=lesson_requested", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message => message.Contains("lesson-free-text-secret", StringComparison.Ordinal));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task InvalidTrainingProgressAndOldSchemaFailClosed()
    {
        var state = await PreparedState();
        var learner = state.Agents[0].AgentId;
        var mentor = state.Agents[1].AgentId;
        var withLesson = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == learner
                ? person with { Lesson = new(mentor, SkillKind.Building, "training", 21, 0, 0) } : person).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(withLesson));
        withLesson = withLesson with
        {
            SchemaVersion = 7,
            Agents = withLesson.Agents.Select(person => person.Lesson is { } lesson
                ? person with { Lesson = lesson with { Progress = 1 } } : person).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(withLesson));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task APendingPersonalReplyContinuesAnAcceptedLessonWithoutAnsweringAPartnership(bool delayedStartup, bool deferSchedulerEntry)
    {
        var state = await PreparedState();
        var learner = state.Society.Society.Agents.Single(person => person.CurrentRole == SocietyWorkRole.Trader).Id;
        using var setup = WorldRuntime.Restore(state,
            actor => new LessonProvider(actor == learner ? "learn:building:" : "lesson_accept:"));
        for (var tick = 0; tick < 80 && setup.Agents.Single(person => person.AgentId == learner).Lesson?.Progress is not >= 3; tick++)
            Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        state = setup.ExportState();
        var lesson = state.Agents.Single(person => person.AgentId == learner).Lesson!;
        Assert.InRange(lesson.Progress, 3, 10);
        var teacher = lesson.TeacherId;
        var proposal = SocietyRules.ProposeRelationship(state.Society.Society,
            new("waiting-lesson-partnership", 1, SocietyRelationshipType.Partnership, learner, teacher, setup.WorldTick));
        Assert.Equal(SocietyRelationshipState.Proposed,
            proposal.Checkpoint.Relationships.Single(item => item.Id == "waiting-lesson-partnership").State);
        state = state with { Society = state.Society with { Society = proposal.Checkpoint } };
        var provider = new HeldPhysicalTaskDecisionProvider();
        using var world = WorldRuntime.Restore(state,
            actor => actor == teacher ? provider : new LessonProvider("safe_idle"));
        var startAfter = world.WorldTick + 3;
        var harness = ModelWorkHarness.For(world);
        var entryAllowed = !deferSchedulerEntry;
        harness.Scheduler.CanStart = () => entryAllowed && (!delayedStartup || world.WorldTick >= startAfter);
        world.SubmitInstruction(new("waiting-lesson-guidance", "owner:test", teacher,
            OwnerInstructionKind.Suggestion, "Think about your next task."));
        Assert.True((await harness.TickAsync()).Advanced);
        var call = Assert.Single(world.PendingModelWork, item => item.Transition.Kind == ModelWorkKind.Decision && item.Transition.ActorId == teacher);
        var request = Assert.IsType<CognitionDecisionRequest>(call.DecisionRequest);
        var task = call.Completion;
        // Keep the six-tick physical bound, then observe actual provider entry
        // at a frozen checkpoint without spending lesson progress on scheduling.
        for (var tick = 0; tick < 6 && !provider.Started.Task.IsCompleted; tick++)
            Assert.True((await world.TickModelWorkAsync()).Advanced);
        var entering = WorldRuntimeCodec.Encode(world.ExportState());
        if (deferSchedulerEntry) Assert.False(provider.Started.Task.IsCompleted);
        entryAllowed = true;
        harness.RunPendingWork();
        await ModelWorkHarness.WaitForSignalAsync(Task.WhenAny(provider.Started.Task, task));
        Assert.True(provider.Started.Task.IsCompleted, $"Held lesson request {request.RequestId} finished before provider entry.");
        Assert.Same(task, Assert.Single(world.PendingModelWork, item => item.Transition.ActorId == teacher).Completion);
        Assert.Same(task, call.Completion);
        Assert.False(task.IsCompleted);
        Assert.Same(request.Observation, Assert.Single(provider.Requests));
        Assert.Equal(entering, WorldRuntimeCodec.Encode(world.ExportState()));
        if (delayedStartup) Assert.True(world.WorldTick >= startAfter);
        Assert.Contains(Assert.Single(provider.Requests).Candidates, candidate => candidate.Id.StartsWith("partner_accept:", StringComparison.Ordinal));
        var before = world.Agents.Single(person => person.AgentId == learner).Lesson!;
        Assert.InRange(before.Progress, 3, 17);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.TickModelWorkAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 3; tick++) Assert.True((await world.TickModelWorkAsync()).Advanced);
        Assert.Equal(before.Progress + 3, world.Agents.Single(person => person.AgentId == learner).Lesson!.Progress);
        Assert.Equal(SocietyRelationshipState.Proposed,
            world.Society.Relationships.Single(item => item.Id == "waiting-lesson-partnership").State);
        Assert.Single(provider.Requests);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "hosted_decision_completed");
        world.Validate();
        Assert.False(task.IsCompleted);
        world.Pause();
        bytes = WorldRuntimeCodec.Encode(world.ExportState());
        await ModelWorkHarness.WaitForSignalAsync(task);
        Assert.DoesNotContain(world.PendingModelWork, item => item.Transition.Kind == ModelWorkKind.Decision && item.Transition.ActorId == teacher);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    private static async Task<WorldRuntimeState> PreparedState()
    {
        using var world = new WorldRuntime("settlement-learning", _ => new LessonProvider("safe_idle"));
        world.StageStarterContent();
        for (var tick = 0; tick < 5; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        var state = world.ExportState();
        return state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Agents = state.Society.Society.Agents.Select(person => person.Id == "founder-ilya"
                        ? person with { CurrentRole = SocietyWorkRole.Unassigned } : person).ToArray(),
                }
            },
            Agents = state.Agents.Select(person => person with
            {
                LastDecisionContext = null,
                Skills = person.AgentId == "founder-ilya"
                    ? [new(SkillKind.Building, state.Society.Society.WorldTick),
                        new(SkillKind.Farming, state.Society.Society.WorldTick)] : null,
            }).ToArray(),
        };
    }

    private sealed class LessonProvider(string prefix) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public IReadOnlyList<string> CandidateIds { get; private set; } = [];
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            CandidateIds = request.Observation.Candidates.Select(item => item.Id).ToArray();
            var candidate = request.Observation.Candidates.FirstOrDefault(item => item.Id.StartsWith(prefix, StringComparison.Ordinal))
                ?? (prefix == "learn:building:" ? request.Observation.Candidates.FirstOrDefault(item => item.Id.StartsWith("build:building:", StringComparison.Ordinal)) : null)
                ?? request.Observation.Candidates.Single(item => item.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with { Candidates = [candidate] },
            }, cancellationToken);
        }
    }
}
