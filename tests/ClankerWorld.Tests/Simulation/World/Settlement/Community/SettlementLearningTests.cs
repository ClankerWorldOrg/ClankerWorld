using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed class SettlementLearningTests
{
    [Theory]
    [InlineData("unskilled", false)]
    [InlineData("hungry", false)]
    [InlineData("busy", false)]
    public async Task OnlyAFreeReadySkilledMentorIsOffered(string condition, bool expected)
    {
        var state = await PreparedState();
        const string teacher = "founder-ilya";
        var learner = state.Society.Society.Inhabitants.Single(person => person.CurrentRole == SocietyWorkRole.Trader).Id;
        var building = state.WorldContent!.Buildings[0];
        state = state with
        {
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == teacher ? person with
            {
                Skills = condition == "unskilled" ? null : person.Skills,
                HungerBasisPoints = condition == "hungry" ? 1_000 : 9_000,
                Project = condition == "busy" ? new("build:building:" + building.CanonicalId, building.DisplayName,
                    state.Society.Society.WorldTick, "acquiring", LastTransitionTick: state.Society.Society.WorldTick) : null,
            } : person).ToArray(),
        };
        var provider = new LessonProvider("safe_idle");
        using var world = WorldRuntime.Restore(state, id => id == learner ? provider : new LessonProvider("safe_idle"));
        await world.AdvanceOneTickAsync();
        Assert.Equal(expected, provider.CandidateIds.Contains("learn:building:" + teacher));
        Assert.Equal(SocietyWorkRole.Unassigned, world.Society.GetInhabitant(teacher).CurrentRole);
    }

    [Fact]
    public async Task SavedSkillsRejectUnknownTeachersAndFutureLearningTimes()
    {
        var state = await PreparedState();
        using var valid = WorldRuntime.Restore(state);
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(state with { SchemaVersion = 30 }));
        var actor = state.Inhabitants[0].InhabitantId;
        foreach (var skill in new[]
        {
            new SettlementSkill(SettlementSkillKind.Building, state.Society.Society.WorldTick, "missing-teacher"),
            new SettlementSkill(SettlementSkillKind.Building, state.Society.Society.WorldTick + 1),
        })
        {
            Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(state with
            {
                Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == actor
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
            var beforeRole = world.Society.GetInhabitant("founder-scout").CurrentRole;
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            file.Save(world);
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(5));
            presence.RecordAuthenticatedReconnect("test-owner");
            using var service = new WorldRuntimeService(world, file, presence);
            for (var tick = 0; tick < 3; tick++) Assert.True(await service.TryAdvanceOnceAsync());
            Assert.Equal("declined", world.Inhabitants.Single(person => person.InhabitantId == "founder-scout").Lesson!.Stage);
            Assert.Equal(beforeRole, world.Society.GetInhabitant("founder-scout").CurrentRole);
            Assert.False(world.Society.IsPaused);
            Assert.True(await service.TryAdvanceOnceAsync());
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
        var learner = state.Society.Society.Inhabitants.Single(person => person.CurrentRole == SocietyWorkRole.Trader).Id;
        using var requesting = WorldRuntime.Restore(state, actor => new LessonProvider(actor == learner ? "learn:building:" : "safe_idle"));
        await requesting.AdvanceOneTickAsync();
        state = requesting.ExportState();
        var teacher = state.Inhabitants.Single(person => person.InhabitantId == learner).Lesson!.TeacherId;
        var definition = state.WorldContent!.Buildings[0];
        state = state with
        {
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == teacher ? person with
            {
                Project = new("build:building:" + definition.CanonicalId, definition.DisplayName, requesting.WorldTick, "acquiring",
                LastTransitionTick: requesting.WorldTick),
            } : person).ToArray()
        };
        using var world = WorldRuntime.Restore(state, _ => new LessonProvider("lesson_accept:"));
        await world.AdvanceOneTickAsync();
        Assert.Equal("requested", world.Inhabitants.Single(person => person.InhabitantId == learner).Lesson!.Stage);
        Assert.Empty(world.Inhabitants.Single(person => person.InhabitantId == learner).Skills ?? []);
    }

    [Fact]
    public async Task AcceptedTrainingSurvivesPauseAndRestartAndSavesTheSkillAndTeacher()
    {
        var state = await PreparedState();
        var learner = state.Society.Society.Inhabitants.Single(person => person.CurrentRole == SocietyWorkRole.Trader).Id;
        IDecisionProvider Provider(string actor) => new LessonProvider(actor == learner ? "learn:building:" : "lesson_accept:");
        using var world = WorldRuntime.Restore(state, Provider);
        await world.AdvanceOneTickAsync();
        Assert.Equal("requested", world.Inhabitants.Single(person => person.InhabitantId == learner).Lesson!.Stage);
        Assert.Equal(SocietyWorkRole.Trader, world.Society.GetInhabitant(learner).CurrentRole);
        for (var tick = 0; tick < 80 && world.Inhabitants.Single(person => person.InhabitantId == learner).Lesson!.Progress < 3; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        var lesson = world.Inhabitants.Single(person => person.InhabitantId == learner).Lesson!;
        Assert.InRange(lesson.Progress, 3, 19);
        Assert.Equal(SocietyWorkRole.Trader, world.Society.GetInhabitant(learner).CurrentRole);
        Assert.Equal(lesson.Progress, new OwnerWorldObservationStore(world).GetSnapshot().Inhabitants.Single(person => person.Id == learner).Lesson!.Progress);
        world.Pause();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), Provider);
        restored.Resume();
        for (var tick = 0; tick < 100 && restored.Inhabitants.Single(person => person.InhabitantId == learner).Lesson!.Stage != "completed"; tick++)
        {
            await restored.AdvanceOneTickAsync();
        }
        Assert.Equal(SocietyWorkRole.Trader, restored.Society.GetInhabitant(learner).CurrentRole);
        Assert.Equal("completed", restored.Inhabitants.Single(person => person.InhabitantId == learner).Lesson!.Stage);
        Assert.Contains(restored.Society.Memories, memory => memory.OwnerId == learner && memory.Id.StartsWith("lesson-gratitude:", StringComparison.Ordinal));
        var completed = restored.Inhabitants.Single(person => person.InhabitantId == learner);
        var skill = Assert.Single(completed.Skills!);
        Assert.Equal(SettlementSkillKind.Building, skill.Kind);
        Assert.Equal(completed.Lesson!.TeacherId, skill.TeacherId);
        Assert.Equal(completed.Lesson.LastTransitionTick, skill.LearnedTick);
        Assert.Null(completed.Proficiency);
        var shown = Assert.Single(new OwnerWorldObservationStore(restored).GetSnapshot().Inhabitants.Single(person => person.Id == learner).Skills);
        Assert.Equal(skill.TeacherId, shown.TeacherId);
        Assert.Equal(restored.Society.GetInhabitant(skill.TeacherId!).Name, shown.TeacherName);
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var wire = System.Text.Json.JsonSerializer.Serialize(new OwnerWorldObservationStore(restored).GetSnapshot(), options);
        using (var document = System.Text.Json.JsonDocument.Parse(wire))
        {
            var wireLesson = document.RootElement.GetProperty("inhabitants").EnumerateArray()
                .Single(person => person.GetProperty("id").GetString() == learner).GetProperty("lesson");
            Assert.Equal("building", wireLesson.GetProperty("role").GetString());
            Assert.False(wireLesson.TryGetProperty("skill", out _));
        }
        var client = System.Text.Json.JsonSerializer.Deserialize<ClankerWorld.GodotClient.UI.OwnerWorldSnapshot>(wire, options)!;
        Assert.Equal("building", client.Inhabitants.Single(person => person.Id == learner).Lesson!.Skill);
        Assert.Equal(skill.TeacherId, Assert.Single(client.Inhabitants.Single(person => person.Id == learner).Skills!).TeacherId);
        using var completedReload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(restored.ExportState())));
        Assert.Equal(completed.Skills, completedReload.Inhabitants.Single(person => person.InhabitantId == learner).Skills);
        Assert.Equal(2, completed.SocialStanding!.Single(item => item.SubjectId == completed.Lesson!.TeacherId).Trust);
    }

    [Fact]
    public async Task MentorRefusalDoesNotGrantTheRequestedSkill()
    {
        var state = await PreparedState();
        var learner = state.Society.Society.Inhabitants.Single(person => person.CurrentRole == SocietyWorkRole.Trader).Id;
        using var world = WorldRuntime.Restore(state, actor => new LessonProvider(actor == learner ? "learn:building:" : "lesson_decline:"));
        for (var tick = 0; tick < 5; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        Assert.Equal("declined", world.Inhabitants.Single(person => person.InhabitantId == learner).Lesson!.Stage);
        Assert.Equal(0, world.Inhabitants.Single(person => person.InhabitantId == learner).Lesson!.Progress);
        Assert.Empty(world.Inhabitants.Single(person => person.InhabitantId == learner).Skills ?? []);
        Assert.Equal(SocietyWorkRole.Trader, world.Society.GetInhabitant(learner).CurrentRole);
    }

    [Fact]
    public async Task MentorDeathCancelsTrainingWithoutGrantingAnUnearnedSkill()
    {
        var state = await PreparedState();
        var learner = state.Society.Society.Inhabitants.Single(person => person.CurrentRole == SocietyWorkRole.Trader).Id;
        using var world = WorldRuntime.Restore(state, actor => new LessonProvider(actor == learner ? "learn:building:" : "lesson_accept:"));
        await world.AdvanceOneTickAsync();
        await world.AdvanceOneTickAsync();
        state = world.ExportState();
        var mentor = state.Inhabitants.Single(person => person.InhabitantId == learner).Lesson!.TeacherId;
        using var society = SocietyRuntime.Restore(state.Society);
        society.Apply(checkpoint => SocietyRules.Kill(checkpoint, mentor, SocietyDeathCause.Accident, checkpoint.WorldTick));
        state = state with { Society = society.ExportState(), Inhabitants = state.Inhabitants.Where(person => person.InhabitantId != mentor).ToArray() };
        using var restored = WorldRuntime.Restore(state, _ => new LessonProvider("safe_idle"));
        await restored.AdvanceOneTickAsync();
        Assert.Equal("cancelled", restored.Inhabitants.Single(person => person.InhabitantId == learner).Lesson!.Stage);
        Assert.Empty(restored.Inhabitants.Single(person => person.InhabitantId == learner).Skills ?? []);
        Assert.Equal(SocietyWorkRole.Trader, restored.Society.GetInhabitant(learner).CurrentRole);
    }

    [Fact]
    public async Task HungerInterruptsTrainingWithoutLosingProgressOrGrantingTheSkill()
    {
        var state = await PreparedState();
        var learner = state.Society.Society.Inhabitants.Single(person => person.CurrentRole == SocietyWorkRole.Trader).Id;
        using var world = WorldRuntime.Restore(state, actor => new LessonProvider(actor == learner ? "learn:building:" : "lesson_accept:"));
        for (var tick = 0; tick < 80 && world.Inhabitants.Single(person => person.InhabitantId == learner).Lesson?.Progress is not >= 3; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        state = world.ExportState();
        var progress = state.Inhabitants.Single(person => person.InhabitantId == learner).Lesson!.Progress;
        Assert.InRange(progress, 3, 19);
        state = state with
        {
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == learner ? person with { HungerBasisPoints = 1_000 } : person).ToArray(),
        };
        using var restored = WorldRuntime.Restore(state, _ => new LessonProvider("safe_idle"));
        for (var tick = 0; tick < 10; tick++)
        {
            await restored.AdvanceOneTickAsync();
        }
        Assert.Equal(progress, restored.Inhabitants.Single(person => person.InhabitantId == learner).Lesson!.Progress);
        Assert.Empty(restored.Inhabitants.Single(person => person.InhabitantId == learner).Skills ?? []);
        Assert.Equal(SocietyWorkRole.Trader, restored.Society.GetInhabitant(learner).CurrentRole);
    }

    [Fact]
    public async Task LessonJournalReportsBoundedStagesWithoutNames()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-lesson-log-");
        try
        {
            var state = await PreparedState();
            var learner = state.Society.Society.Inhabitants.Single(person => person.CurrentRole == SocietyWorkRole.Trader).Id;
            state = state with
            {
                Society = state.Society with
                {
                    Society = state.Society.Society with
                    {
                        Inhabitants = state.Society.Society.Inhabitants.Select(person => person.Id == learner
                            ? person with { Name = "lesson-free-text-secret" } : person).ToArray(),
                    },
                },
            };
            using var world = WorldRuntime.Restore(state, actor => new LessonProvider(actor == learner ? "learn:building:" : "safe_idle"));
            var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(30));
            presence.RecordAuthenticatedReconnect("owner");
            var logger = new RecordingLogger<WorldRuntimeService>();
            using var service = new WorldRuntimeService(world, new WorldStateFile(Path.Combine(directory.FullName, "world.json")), presence, logger);
            Assert.True(await service.TryAdvanceOnceAsync());
            Assert.Contains(logger.Messages, message => message.Contains("settlement_lesson", StringComparison.Ordinal) &&
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
        var learner = state.Inhabitants[0].InhabitantId;
        var mentor = state.Inhabitants[1].InhabitantId;
        var withLesson = state with
        {
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == learner
                ? person with { Lesson = new(mentor, SettlementSkillKind.Building, "training", 21, 0, 0) } : person).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(withLesson));
        withLesson = withLesson with
        {
            SchemaVersion = 7,
            Inhabitants = withLesson.Inhabitants.Select(person => person.Lesson is { } lesson
                ? person with { Lesson = lesson with { Progress = 1 } } : person).ToArray(),
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(withLesson));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(true, 4000)]
    public async Task APendingPersonalReplyContinuesAnAcceptedLessonWithoutAnsweringAPartnership(bool delayedStartup, int entryDelayMilliseconds)
    {
        var state = await PreparedState();
        var learner = state.Society.Society.Inhabitants.Single(person => person.CurrentRole == SocietyWorkRole.Trader).Id;
        using var setup = WorldRuntime.Restore(state,
            actor => new LessonProvider(actor == learner ? "learn:building:" : "lesson_accept:"));
        for (var tick = 0; tick < 80 && setup.Inhabitants.Single(person => person.InhabitantId == learner).Lesson?.Progress is not >= 3; tick++)
            Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        state = setup.ExportState();
        var lesson = state.Inhabitants.Single(person => person.InhabitantId == learner).Lesson!;
        Assert.InRange(lesson.Progress, 3, 10);
        var teacher = lesson.TeacherId;
        var proposal = SocietyRules.ProposeRelationship(state.Society.Society,
            new("waiting-lesson-partnership", 1, SocietyRelationshipType.Partnership, learner, teacher, setup.WorldTick));
        Assert.Equal(SocietyRelationshipState.Proposed,
            proposal.Checkpoint.Relationships.Single(item => item.Id == "waiting-lesson-partnership").State);
        state = state with { Society = state.Society with { Society = proposal.Checkpoint } };
        var provider = new HeldPhysicalTaskDecisionProvider();
        using var world = WorldRuntime.Restore(state,
            actor => actor == teacher ? new DelayedLessonEntryProvider(provider, entryDelayMilliseconds) : new LessonProvider("safe_idle"));
        var startAfter = world.WorldTick + 3;
        provider.CanStart = () => !delayedStartup || world.WorldTick >= startAfter;
        world.SubmitInstruction(new("waiting-lesson-guidance", "owner:test", teacher,
            OwnerInstructionKind.Suggestive, "Think about your next task."));
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        var hosted = Assert.IsAssignableFrom<System.Collections.IDictionary>(typeof(WorldRuntime)
            .GetField("pendingHosted", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(world));
        var call = hosted[teacher]!;
        Assert.NotNull(call);
        var request = Assert.IsType<CognitionDecisionRequest>(call.GetType().GetProperty("Request")!.GetValue(call));
        var task = Assert.IsAssignableFrom<Task>(call.GetType().GetProperty("Task")!.GetValue(call));
        // Keep the six-tick physical bound, then observe actual provider entry
        // at a frozen checkpoint without spending lesson progress on scheduling.
        for (var tick = 0; tick < 6 && !provider.Started.Task.IsCompleted; tick++)
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        var entering = WorldRuntimeCodec.Encode(world.ExportState());
        await Task.WhenAny(provider.Started.Task, task).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(provider.Started.Task.IsCompleted, $"Held lesson request {request.RequestId} finished before provider entry.");
        Assert.Same(call, hosted[teacher]);
        Assert.Same(task, call.GetType().GetProperty("Task")!.GetValue(call));
        Assert.False(task.IsCompleted);
        Assert.Same(request.Observation, Assert.Single(provider.Requests));
        Assert.Equal(entering, WorldRuntimeCodec.Encode(world.ExportState()));
        if (delayedStartup) Assert.True(world.WorldTick >= startAfter);
        Assert.Contains(Assert.Single(provider.Requests).Candidates, candidate => candidate.Id.StartsWith("partner_accept:", StringComparison.Ordinal));
        var before = world.Inhabitants.Single(person => person.InhabitantId == learner).Lesson!;
        Assert.InRange(before.Progress, 3, 17);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickNonBlockingAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 3; tick++) Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        Assert.Equal(before.Progress + 3, world.Inhabitants.Single(person => person.InhabitantId == learner).Lesson!.Progress);
        Assert.Equal(SocietyRelationshipState.Proposed,
            world.Society.Relationships.Single(item => item.Id == "waiting-lesson-partnership").State);
        Assert.Single(provider.Requests);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "hosted_decision_completed");
        world.Validate();
        Assert.False(task.IsCompleted);
        world.Pause();
        bytes = WorldRuntimeCodec.Encode(world.ExportState());
        await task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(hosted.Contains(teacher));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    private sealed class DelayedLessonEntryProvider(HeldPhysicalTaskDecisionProvider inner, int delayMilliseconds) : IDecisionProvider
    {
        public DecisionProviderKind Kind => inner.Kind;
        public long ProviderEpoch => inner.ProviderEpoch;
        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            while (inner.CanStart?.Invoke() == false) await Task.Delay(1, cancellationToken);
            if (delayMilliseconds > 0) await Task.Delay(delayMilliseconds, cancellationToken);
            return await inner.DecideAsync(request, cancellationToken);
        }
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
                    Inhabitants = state.Society.Society.Inhabitants.Select(person => person.Id == "founder-ilya"
                        ? person with { CurrentRole = SocietyWorkRole.Unassigned } : person).ToArray(),
                }
            },
            Inhabitants = state.Inhabitants.Select(person => person with
            {
                LastDecisionContext = null,
                Skills = person.InhabitantId == "founder-ilya"
                    ? [new(SettlementSkillKind.Building, state.Society.Society.WorldTick),
                        new(SettlementSkillKind.Farming, state.Society.Society.WorldTick)] : null,
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
