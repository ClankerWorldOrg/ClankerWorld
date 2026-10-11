using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Viewer.Observation;

namespace ClankerWorld.Simulation.Tests;

public sealed partial class GrownAgentHelperMemoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedNativeLessonReachesPersonalRecallAndContinuesAfterHostedReload(bool nativeBorn)
    {
        // Existing fixture grows genuine births and adults with explicit calendar,
        // birth food and lifespan preparation. Keep their skills and positions.
        var state = PrivateWorldRuntimeCodec.Decode(await LaterAdult.Value);
        var teacher = state.Inhabitants.Single(person => (person.Skills ?? [])
            .Any(skill => skill.Kind == SettlementSkillKind.Crafting)).InhabitantId;
        var student = nativeBorn ? state.Society.Society.Births.OrderBy(birth => birth.CommittedTick).Last().ChildId :
            state.Inhabitants.First(person => person.InhabitantId != teacher && (person.Skills ?? []).Count == 0 &&
                person.InhabitantId.StartsWith("founder:", StringComparison.Ordinal)).InhabitantId;
        Assert.InRange(student.Length, 1, DerivedId.MaximumLength);
        var beforeTrust = state.Inhabitants.Single(person => person.InhabitantId == student)
            .SocialStanding?.SingleOrDefault(item => item.SubjectId == teacher)?.Trust ?? 0;
        state = state with { RoutineHelper = RoutineHelperSettings.Off };
        var learning = new NativeLessonChoices(true);
        var teaching = new NativeLessonChoices(false);
        using var world = PrivateWorldRuntime.Restore(state,
            id => id == student ? learning : id == teacher ? teaching : new QuietProvider());
        world.Resume();
        using var host = new NativeLessonCheckpointHost(world);
        for (var tick = 0; tick < 200 && world.Inhabitants.Single(person => person.InhabitantId == student)
                 .Lesson?.Stage != "completed"; tick++) await host.Step();
        var completed = world.Inhabitants.Single(person => person.InhabitantId == student);
        Assert.Equal("completed", completed.Lesson?.Stage);
        Assert.Equal(20, completed.Lesson!.Progress);
        Assert.Contains(learning.Choices, choice => choice == "learn:crafting:" + teacher);
        Assert.Contains(teaching.Choices, choice => choice == "lesson_accept:" + student);
        var skill = Assert.Single(completed.Skills!, item => item.Kind == SettlementSkillKind.Crafting);
        Assert.Equal(teacher, skill.TeacherId);
        Assert.Equal(completed.Lesson.LastTransitionTick, skill.LearnedTick);
        Assert.Equal(beforeTrust + 2, completed.SocialStanding!.Single(item => item.SubjectId == teacher).Trust);
        var memory = Assert.Single(world.Society.Memories, item => item.OwnerId == student && item.SubjectId == teacher &&
            item.Id.StartsWith("lesson-gratitude:", StringComparison.Ordinal));
        Assert.Equal("Learned crafting with " + world.Society.GetInhabitant(teacher).Name + ".", memory.Summary);
        Assert.Equal("public", memory.Visibility);
        Assert.Equal(SocietyMemoryKind.Skill, memory.Kind);
        Assert.Equal(skill.LearnedTick, memory.SourceTick);
        if (!nativeBorn) Assert.Equal($"lesson-gratitude:{student}:{teacher}:Crafting", memory.Id);

        // Set both continuations at the public external-call cancellation boundary.
        // Nothing is filtered from the saved bytes or inserted into the memory ledger.
        world.CancelPendingHostedDecisions();
        var bytes = host.Checkpoint();
        var after = new NativeLessonChoices(true);
        var afterTeacher = new NativeLessonChoices(false);
        var repeated = new NativeLessonChoices(true);
        var repeatedTeacher = new NativeLessonChoices(false);
        using var reload = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes),
            id => id == student ? after : id == teacher ? afterTeacher : new QuietProvider());
        using var replay = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes),
            id => id == student ? repeated : id == teacher ? repeatedTeacher : new QuietProvider());
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(reload.ExportState()));
        using var reloadHost = new NativeLessonCheckpointHost(reload);
        using var replayHost = new NativeLessonCheckpointHost(replay);
        for (var tick = 0; tick < 30 && !after.Observations.Any(item => item.WorldTick > memory.SourceTick); tick++)
        {
            await reloadHost.Step();
            await replayHost.Step();
            Assert.Equal(reloadHost.Saved(), replayHost.Saved());
        }
        var observation = after.Observations.First(item => item.WorldTick > memory.SourceTick);
        observation.Validate();
        var excerpt = Assert.Single(observation.RetrievedMemories ?? [], item => item.Id == memory.Id);
        Assert.Equal(student, excerpt.OwnerId);
        Assert.Equal(teacher, excerpt.SubjectId);
        Assert.Equal(memory.Summary, excerpt.Summary);
        Assert.Equal(memory.SourceTick, excerpt.SourceTick);
        Assert.Equal(memory.Visibility, excerpt.Visibility);
        Assert.InRange(memory.Id.Length, 1, 128);
        for (var tick = 0; tick < 2; tick++)
        {
            await reloadHost.Step();
            await replayHost.Step();
            Assert.Equal(reloadHost.Saved(), replayHost.Saved());
        }
        var continued = reload.Inhabitants.Single(person => person.InhabitantId == student);
        Assert.Equal(completed.Skills, continued.Skills);
        Assert.Equal(beforeTrust + 2, continued.SocialStanding!.Single(item => item.SubjectId == teacher).Trust);
        Assert.Equal(memory, Assert.Single(reload.Society.Memories, item => item.Id == memory.Id));
        Assert.Single(reload.Society.Memories, item => item.OwnerId == student && item.SubjectId == teacher &&
            item.Id.StartsWith("lesson-gratitude:", StringComparison.Ordinal));
    }

    private sealed class NativeLessonChoices(bool student) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public ConcurrentQueue<InhabitantObservation> Observations { get; } = new();
        public ConcurrentQueue<string> Choices { get; } = new();
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            Observations.Enqueue(observation);
            var choice = observation.Candidates.FirstOrDefault(item => student
                ? item.Id.StartsWith("learn:crafting:", StringComparison.Ordinal) || item.Id == "lesson_attend"
                : item.Id.StartsWith("lesson_accept:", StringComparison.Ordinal) ||
                    item.Id.StartsWith("lesson_teach:", StringComparison.Ordinal));
            choice ??= observation.Candidates.Single(item => item.Id == "safe_idle");
            Choices.Enqueue(choice.Id);
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.InhabitantId,
                Kind, ProviderEpoch, observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest,
                choice.Id, 1, observation.Candidates.ToDictionary(item => item.Id, item => item.Id == choice.Id ? 1d : 0d)));
        }
    }

    private sealed class NativeLessonCheckpointHost : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("native-lesson-");
        private readonly PrivateWorldRuntime world;
        private readonly PrivateWorldStateFile file;
        private readonly PrivateWorldRuntimeService service;
        public NativeLessonCheckpointHost(PrivateWorldRuntime world)
        {
            this.world = world;
            file = new(Path.Combine(directory.FullName, "world.json"));
            file.Save(world);
            var presence = new OwnerClientPresenceLease(TimeSpan.FromHours(1));
            presence.RecordAuthenticatedReconnect("test-owner");
            service = new(world, file, presence);
        }
        public byte[] Saved() => File.ReadAllBytes(file.Path);
        public byte[] Checkpoint() { file.Save(world); return Saved(); }
        public async Task Step()
        {
            await WaitForRequests(world);
            Assert.True(await service.TryAdvanceOnceAsync());
            await WaitForRequests(world);
            var saved = Saved();
            Assert.Equal(saved, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
            using var reloaded = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(saved));
            Assert.Equal(saved, PrivateWorldRuntimeCodec.Encode(reloaded.ExportState()));
        }
        public void Dispose() { service.Dispose(); directory.Delete(recursive: true); }
    }
}
