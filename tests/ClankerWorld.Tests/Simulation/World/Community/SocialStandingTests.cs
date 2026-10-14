using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed class SocialStandingTests
{
    [Fact]
    public void CooperationMemoriesProjectAsTrustWithoutRewritingCurrentCheckpoint()
    {
        using var seed = new WorldRuntime("cooperation-social-standing");
        var state = seed.ExportState();
        var owner = state.Agents[0].AgentId;
        var subject = state.Agents[1].AgentId;
        using var society = SocietyRuntime.Restore(state.Society);
        society.Apply(checkpoint => SocietyRules.RecordSocialMemory(checkpoint, new(
            Identity.Create($"project-gratitude:{owner}:{subject}"), owner, subject, "Prior material help.", "public", checkpoint.WorldTick)));
        state = state with { Society = society.ExportState() };
        using var world = WorldRuntime.Restore(state);
        var checkpointBeforeProjection = WorldRuntimeCodec.Encode(world.ExportState());
        var projected = new OwnerWorldObservationStore(world).GetSnapshot().Agents.Single(person => person.Id == owner);
        Assert.Equal(2, projected.SocialStanding.Single(item => item.SubjectId == subject).Trust);
        Assert.Equal(WorldRuntime.StateSchemaVersion, world.ExportState().SchemaVersion);
        Assert.Equal(checkpointBeforeProjection, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.All(world.Agents, person => Assert.Null(person.SocialStanding));
    }

    [Fact]
    public void TrustSurvivesTheSubjectsDeathAsHistoricalContext()
    {
        using var seed = new WorldRuntime("historical-social-standing");
        var state = seed.ExportState();
        var owner = state.Agents[0].AgentId;
        var subject = state.Agents[1].AgentId;
        using var society = SocietyRuntime.Restore(state.Society);
        society.Apply(checkpoint => SocietyRules.Kill(checkpoint, subject, SocietyDeathCause.Accident, checkpoint.WorldTick));
        state = state with
        {
            SchemaVersion = WorldRuntime.StateSchemaVersion,
            Society = society.ExportState(),
            Agents = state.Agents.Where(person => person.AgentId != subject).Select(person => person.AgentId == owner
                ? person with { SocialStanding = [new(subject, 3, state.Society.Society.WorldTick)] }
                : person).ToArray(),
        };
        using var restored = WorldRuntime.Restore(state);
        Assert.Equal(3, restored.Agents.Single(person => person.AgentId == owner).SocialStanding!
            .Single(item => item.SubjectId == subject).Trust);
    }

    [Fact]
    public async Task CompletedExchangeLogsBoundedTrustWithoutPrivateNames()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-standing-log-");
        try
        {
            using var seed = new WorldRuntime("social-standing-log");
            var state = seed.ExportState();
            var first = state.Agents[0].AgentId;
            var second = state.Agents[1].AgentId;
            state = DirectBarterTests.AtTradeMeeting(state, first, second);
            state = state with
            {
                Agents = state.Agents.Select(person => person with
                { FullnessBasisPoints = 7_500 }).ToArray(),
                Society = state.Society with
                {
                    Society = state.Society.Society with
                    {
                        Agents = state.Society.Society.Agents.Select(person => person.Id == second
                            ? person with { Name = "private-standing-name" } : person).ToArray(),
                        Inventory = InventoryAuthority.AddLot(
                        InventoryAuthority.AddLot(state.Society.Society.Inventory, "standing-food", "food", first, 4),
                        "standing-clothes", "clothing", second, 2),
                    }
                },
            };
            IDecisionProvider Provider(string actor) => new ChoiceProvider(actor == first ? "trade_propose:" : actor == second
                ? "trade_accept:" : "safe_idle");
            using var world = WorldRuntime.Restore(state, Provider);
            var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(30));
            presence.RecordAuthenticatedReconnect("owner");
            var logger = new RecordingLogger<WorldRuntimeService>();
            using var service = new WorldRuntimeService(world,
                new WorldStateFile(Path.Combine(directory.FullName, "world.json")), presence, logger);
            for (var tick = 0; tick < 120 && !logger.Messages.Any(message => message.Contains("social_standing", StringComparison.Ordinal)); tick++)
            {
                Assert.True(await service.TryAdvanceOnceAsync());
            }
            Assert.Contains(logger.Messages, message => message.Contains("social_standing", StringComparison.Ordinal) &&
                message.Contains("trust=1", StringComparison.Ordinal) && message.Contains("reason=barter_completed", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message => message.Contains("private-standing-name", StringComparison.Ordinal));
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
            Assert.Equal(1, restored.Agents.Single(person => person.AgentId == first).SocialStanding!
                .Single(item => item.SubjectId == second).Trust);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private sealed class ChoiceProvider(string prefix) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.Candidates.FirstOrDefault(item => item.Id.StartsWith(prefix, StringComparison.Ordinal))
                ?? request.Observation.Candidates.Single(item => item.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with { Candidates = [candidate] },
            }, cancellationToken);
        }
    }
}
