using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed class FoodStewardshipTests
{
    private static readonly Lazy<Task<byte[]>> PreparedCheckpoint = new(CreatePreparedCheckpoint);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StewardCannotChangeFoodAccessWithoutIndependentMajorityAcrossRestart(bool reject)
    {
        var state = await PreparedState();
        var steward = state.FoodStewardship!.StewardId!;
        using var world = WorldRuntime.Restore(state, id => new PrefixChoiceProvider(id == steward ? "food_policy_propose:" : "safe_idle"));
        await world.AdvanceOneTickAsync();
        Assert.Equal("open", world.ExportState().FoodStewardship!.FoodPolicy);
        Assert.Equal([steward], world.ExportState().FoodStewardship!.Ballot!.Approvals);
        world.Pause();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            _ => new PrefixChoiceProvider(reject ? "food_policy_vote_no" : "food_policy_vote_yes"));
        restored.Resume();
        await restored.AdvanceOneTickAsync();
        Assert.Null(restored.ExportState().FoodStewardship!.Ballot);
        Assert.Equal(reject ? "open" : "essential_first", restored.ExportState().FoodStewardship!.FoodPolicy);
        var visible = new OwnerWorldObservationStore(restored).GetSnapshot().FoodStewardship!;
        Assert.NotNull(visible.StewardName);
        Assert.Equal(restored.ExportState().FoodStewardship!.FoodPolicy, visible.FoodPolicy);
    }

    [Theory]
    [InlineData("open", 6_000, true)]
    [InlineData("essential_first", 6_000, false)]
    public async Task AdoptedPolicyChangesSharedFoodAccessButProtectsHungryMembers(string policy, int hunger, bool allowed)
    {
        var state = await PreparedState();
        var collector = state.Agents[1].AgentId;
        state = state with
        {
            FoodStewardship = state.FoodStewardship! with { FoodPolicy = policy },
            Agents = state.Agents.Select(person => person.AgentId == collector
                ? person with { FullnessBasisPoints = hunger, LastDecisionContext = null } : person).ToArray(),
        };
        // Food-policy access is independent of weather's urgent shelter decisions.
        state = WeatherTestFixture.WithWeather(state, WeatherKind.Clear);
        using var world = WorldRuntime.Restore(state, id => new PrefixChoiceProvider(id == collector ? "collect_shared_food" : "safe_idle"));
        for (var tick = 0; tick < 40; tick++)
        {
            await world.AdvanceOneTickAsync();
        }
        Assert.Equal(allowed, world.ExportState().Events.Any(item => item.Kind == "household_food_collected" &&
            item.Detail.StartsWith(collector + ":", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task StewardDeathPreservesPolicyAndSelectsALivingSuccessor()
    {
        var state = await PreparedState();
        var departed = state.FoodStewardship!.StewardId!;
        using var society = SocietyRuntime.Restore(state.Society);
        society.Apply(checkpoint => SocietyRules.Kill(checkpoint, departed, SocietyDeathCause.Accident, checkpoint.WorldTick));
        state = state with
        {
            Society = society.ExportState(),
            Agents = state.Agents.Where(person => person.AgentId != departed).ToArray(),
            FoodStewardship = state.FoodStewardship with { FoodPolicy = "essential_first" },
        };
        using var world = WorldRuntime.Restore(state, _ => new PrefixChoiceProvider("safe_idle"));
        await world.AdvanceOneTickAsync();
        var foodStewardship = world.ExportState().FoodStewardship!;
        Assert.Equal("essential_first", foodStewardship.FoodPolicy);
        Assert.NotNull(foodStewardship.StewardId);
        Assert.NotEqual(departed, foodStewardship.StewardId);
        Assert.Equal(SocietyAgentStatus.Active, world.Society.GetAgent(foodStewardship.StewardId!).Status);
        var accepted = world.ExportState();
        var entry = accepted.Events.Last(item => item.Kind == "food_steward_changed");
        Assert.Equal(foodStewardship.StewardId, entry.Payload.ActorId);
        Assert.Equal(accepted.Agents.Single(person => person.AgentId == foodStewardship.StewardId).Position, entry.Position);
        Assert.Equal(foodStewardship.StewardId, entry.Detail);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(foodStewardship, restored.ExportState().FoodStewardship);
        Assert.Equal(entry, restored.ExportState().Events.Single(item => item.EventId == entry.EventId));
    }

    [Fact]
    public async Task CouncilJournalReportsPolicyTransitionWithoutFreeTextNames()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-council-log-");
        try
        {
            var state = await PreparedState();
            var steward = state.FoodStewardship!.StewardId!;
            state = state with
            {
                Society = state.Society with
                {
                    Society = state.Society.Society with
                    {
                        Agents = state.Society.Society.Agents.Select(person => person.Id == steward
                            ? person with { Name = "council-free-text-secret" } : person).ToArray(),
                    },
                },
            };
            using var world = WorldRuntime.Restore(state, id => new PrefixChoiceProvider(id == steward ? "food_policy_propose:" : "safe_idle"));
            var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(30));
            presence.RecordAuthenticatedReconnect("owner");
            var logger = new RecordingLogger<WorldRuntimeService>();
            using var service = new WorldRuntimeService(world, new WorldStateFile(Path.Combine(directory.FullName, "world.json")), presence, logger);
            Assert.True(await service.TryAdvanceOnceAsync());
            Assert.Contains(logger.Messages, message => message.Contains("food_stewardship", StringComparison.Ordinal) &&
                message.Contains("event=food_policy_proposed", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message => message.Contains("council-free-text-secret", StringComparison.Ordinal));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ForgedBallotsFailClosed()
    {
        var state = await PreparedState();
        var actor = state.Agents[0].AgentId;
        var invalid = state with
        {
            FoodStewardship = state.FoodStewardship! with
            {
                Ballot = new("essential_first", state.Society.Society.WorldTick, state.Society.Society.WorldTick + 120,
                    [actor], [actor], [actor]),
            },
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(invalid));
    }

    private static async Task<WorldRuntimeState> PreparedState() =>
        WorldRuntimeCodec.Decode(await PreparedCheckpoint.Value);

    private static async Task<byte[]> CreatePreparedCheckpoint()
    {
        using var seed = new WorldRuntime("household-council-test", _ => new PrefixChoiceProvider("safe_idle"));
        seed.StageStarterContent();
        for (var tick = 0; tick < 305; tick++)
        {
            await seed.AdvanceOneTickAsync();
        }
        var state = seed.ExportState();
        var food = state.Society.Society.Inventory.Lots.First(lot => lot.ItemKind == "food" && lot.OwnerId == "household:first");
        return WorldRuntimeCodec.Encode(state with
        {
            FoodStewardship = new(state.Agents[0].AgentId, "open", 0),
            Agents = state.Agents.Select(person => person with
            {
                FullnessBasisPoints = 7_000,
                Survival = new(),
            }).ToArray(),
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Inventory = state.Society.Society.Inventory with
                    {
                        Lots = state.Society.Society.Inventory.Lots.Select(lot => lot.Id == food.Id ? lot with { Quantity = 3 } : lot).ToArray(),
                    },
                },
            },
        });
    }

}
