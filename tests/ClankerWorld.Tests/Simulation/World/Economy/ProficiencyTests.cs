using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed class ProficiencyTests
{
    [Theory]
    [InlineData(0, false, false)]
    [InlineData(10, false, false)]
    [InlineData(0, true, false)]
    [InlineData(30, true, false)]
    [InlineData(0, true, true)]
    public async Task PracticeImprovesWorkAndOnlySuccessfulCompletionEarnsCredit(int experience, bool finish, bool knownSkill)
    {
        using var seed = new WorldRuntime("practice-work", _ => new IdleProvider());
        seed.StageStarterContent();
        for (var tick = 0; tick < 3; tick++) await seed.AdvanceOneTickAsync();
        var state = seed.ExportState();
        var actor = state.Agents[0];
        var definition = seed.WorldContent.Buildings.Single(building => building.LocalId == "fire");
        var site = MovementTestWorld.BuildingSite(state, definition, actor.AgentId);
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "practice-tool", "wooden_hammer", actor.AgentId, 1),
                    Agents = state.Society.Society.Agents.Select(person => person with { Name = $"sk-private-practice-name-{person.Id}" }).ToArray(),
                }
            },
            Agents = state.Agents.Select(person => person == actor ? person with
            {
                Position = site,
                FullnessBasisPoints = 9_000,
                Proficiency = new(experience),
                Skills = knownSkill ? [new(SkillKind.Building, 0)] : null,
                Project = new(TownConstructionCandidateIds.Building(definition.CanonicalId, site), definition.DisplayName, seed.WorldTick, "working",
                    finish ? 10 : 0, LastTransitionTick: seed.WorldTick),
            } : person).ToArray(),
        };
        using var world = WorldRuntime.Restore(state, _ => new IdleProvider());
        var directory = Directory.CreateTempSubdirectory("practice-log-");
        try
        {
            var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(30));
            presence.RecordAuthenticatedReconnect("owner");
            var logger = new RecordingLogger<WorldRuntimeService>();
            using var service = new WorldRuntimeService(world, new WorldStateFile(Path.Combine(directory.FullName, "world.json")), presence, logger);
            Assert.True(await service.TryAdvanceOnceAsync());
            var result = world.Agents.Single(person => person.AgentId == actor.AgentId);
            Assert.Equal(finish ? Math.Min(30, experience + 1) : experience, result.Proficiency!.Building);
            Assert.Equal(finish ? 10 : 2 + experience / 10, result.Project!.WorkDone);
            Assert.Equal(finish ? "completed" : "working", result.Project.Stage);
            if (finish || knownSkill)
            {
                var skill = Assert.Single(result.Skills!);
                Assert.Equal(SkillKind.Building, skill.Kind);
                Assert.Null(skill.TeacherId);
                Assert.Equal(knownSkill ? 0 : world.WorldTick, skill.LearnedTick);
            }
            else Assert.Empty(result.Skills ?? []);
            Assert.Equal(finish && experience < 30, logger.Messages.Any(message => message.Contains("work_practice tick=", StringComparison.Ordinal)));
            Assert.DoesNotContain(logger.Messages, message => message.Contains("sk-private-practice-name", StringComparison.Ordinal));
            var projection = new OwnerWorldObservationStore(world).GetSnapshot().Agents.Single(person => person.Id == actor.AgentId);
            Assert.Equal(result.Proficiency.Building, projection.Proficiency!.Building);
            Assert.Equal(finish || knownSkill ? 1 : 0, projection.Skills.Count);
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), _ => new IdleProvider());
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(restored.ExportState()));
            if (finish)
            {
                await restored.AdvanceOneTickAsync();
                Assert.Equal(result.Proficiency, restored.Agents.Single(person => person.AgentId == actor.AgentId).Proficiency);
                Assert.Equal(result.Skills, restored.Agents.Single(person => person.AgentId == actor.AgentId).Skills);
            }
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed class IdleProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default) =>
            new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with { Candidates = request.Observation.Candidates.Where(candidate => candidate.Id == "safe_idle").ToArray() },
            }, cancellationToken);
    }
}
