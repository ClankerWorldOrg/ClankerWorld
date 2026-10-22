using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed class SwimmingRuntimeTests
{
    private const string Actor = "founder:00000000000000000000000000000001";


    [Theory]
    [InlineData(TerrainKind.River)]
    [InlineData(TerrainKind.Lake)]
    public async Task LightHealthyAgentCrossesWideWaterSlowlyLosesWarmthAndResumesAcrossReload(TerrainKind water)
    {
        var start = water == TerrainKind.River ? new GridPoint(42, 4) : new GridPoint(4, 2);
        var destination = water == TerrainKind.River ? new GridPoint(46, 4) : new GridPoint(8, 2);
        var firstWater = new GridPoint(start.X + 1, start.Y);
        var state = CrossingState(start);
        using var world = Restore(state);
        world.SubmitInstruction(new("cross-water", "owner:test", Actor, OwnerInstructionKind.Order,
            $"Move to tile ({firstWater.X}, {firstWater.Y})"));
        for (var tick = 0; tick < 64 && Position(world) != firstWater; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(firstWater, Position(world));
        var swimming = world.ExportState();
        var physical = swimming.Agents.Single(person => person.AgentId == Actor);
        Assert.True(physical.TravelCooldownTicks > 2, "Swimming must be much slower than two-tile wading.");
        Assert.Equal("swim", new OwnerWorldObservationStore(world).GetSnapshot().Agents.Single(person => person.Id == Actor).Route.Status);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(firstWater, Position(world));
        swimming = world.ExportState();
        Assert.True(physical.Survival!.WarmthBasisPoints -
            swimming.Agents.Single(person => person.AgentId == Actor).Survival!.WarmthBasisPoints >= 75,
            "The wait between slow swimming steps must also lose warmth.");
        var before = WorldRuntimeCodec.Encode(swimming);
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        using var reloaded = Restore(WorldRuntimeCodec.Decode(before));
        for (var x = firstWater.X + 1; x <= destination.X; x++)
        {
            var next = new GridPoint(x, start.Y);
            var text = $"Move to tile ({next.X}, {next.Y})";
            world.SubmitInstruction(new($"cross-{x}", "owner:test", Actor, OwnerInstructionKind.Order, text));
            reloaded.SubmitInstruction(new($"cross-{x}", "owner:test", Actor, OwnerInstructionKind.Order, text));
            for (var tick = 0; tick < 32 && Position(world) != next; tick++)
            {
                Assert.True((await world.AdvanceOneTickAsync()).Advanced);
                Assert.True((await reloaded.AdvanceOneTickAsync()).Advanced);
                Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
            }
            Assert.Equal(next, Position(world));
        }
        Assert.Equal(destination, Position(world));
        Assert.All(world.ExportState().Instructions!, order =>
        {
            Assert.Equal("finished", order.Task!.Status);
            Assert.Equal(1, order.Task.CompletedUnits);
        });
        Assert.Empty(world.ExportState().DeceasedAgents ?? []);
    }

    [Fact]
    public async Task NativeLandDestinationUsesSwimmingWhenItIsTheCheapestRoute()
    {
        var start = new GridPoint(84, 80);
        var destination = new GridPoint(89, 80);
        using var world = Restore(CrossingState(start, "traffic-bridge-0"));
        world.SubmitInstruction(new("cross-lake", "owner:test", Actor, OwnerInstructionKind.Order, "Move to tile (89, 80)"));
        var sawSwimming = false;
        for (var tick = 0; tick < 96 && Position(world) != destination; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            sawSwimming |= !world.ExportState().Map.IsPassable(Position(world));
        }
        Assert.True(sawSwimming, "The native route to the opposite bank must use this four-tile lake crossing.");
        Assert.Equal(destination, Position(world));
        var order = Assert.Single(world.ExportState().Instructions!).Task!;
        Assert.Equal("finished", order.Status);
        Assert.Equal(1, order.CompletedUnits);
    }

    [Fact]
    public async Task NativeRoadApproachAndSwimmingKeepTheirStepDelaysAcrossReplay()
    {
        var state = CrossingState(new GridPoint(84, 80), "traffic-bridge-0");
        var occupied = state.Map.Resources.Select(item => item.Position)
            .Concat(state.Map.MapObjects.Select(item => item.Position))
            .Concat(state.Agents.Where(person => person.AgentId != Actor).Select(person => person.Position))
            .Concat(state.WorldSimulation!.Buildings.SelectMany(building => WorldContentSimulationRules.Footprint(
                state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building.Position)))
            .ToHashSet();
        var crossing = state.Map.Tiles.SelectMany(tile => new[] { (X: 0, Y: -1), (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0) }
            .Select(direction => (Bank: tile.Position,
                Approach: new GridPoint(tile.Position.X - direction.X, tile.Position.Y - direction.Y),
                Water: new GridPoint(tile.Position.X + direction.X, tile.Position.Y + direction.Y))))
            .First(item => state.Map.IsBuildable(item.Bank) && state.Map.CanFootStep(item.Approach, item.Bank) &&
                SwimmingRules.IsSwimmingWater(state.Map, item.Water) && SwimmingRules.CanStep(state.Map, item.Bank, item.Water) &&
                !occupied.Contains(item.Approach) && !occupied.Contains(item.Bank) && !occupied.Contains(item.Water));
        var (bank, approach, water) = crossing;
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == Actor
                ? person with { Position = approach } : person).ToArray(),
            RoadTiles = state.RoadTiles!.Append(approach).Append(bank).Distinct().ToArray(),
        };
        var baseline = WorldRuntimeCodec.Encode(state);
        using var world = Restore(WorldRuntimeCodec.Decode(baseline));
        using var replay = Restore(WorldRuntimeCodec.Decode(baseline));
        var instruction = new OwnerInstructionRequest("road-into-water", "owner:test", Actor,
            OwnerInstructionKind.Order, $"Move to tile ({water.X}, {water.Y})");
        world.SubmitInstruction(instruction);
        replay.SubmitInstruction(instruction);
        var visited = new List<GridPoint>();
        var previous = approach;
        for (var tick = 0; tick < 16 && Position(world) != water; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            var person = world.Agents.Single(person => person.AgentId == Actor);
            if (person.Position == previous) continue;
            visited.Add(person.Position);
            Assert.Equal(person.Position == bank ? 0 : 7, person.TravelCooldownTicks);
            previous = person.Position;
        }
        Assert.Equal(new[] { bank, water }, visited);
        Assert.Throws<ArgumentOutOfRangeException>(() => world.ExportState().Map.FootStepCost(approach, water));
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var loaded = Restore(WorldRuntimeCodec.Decode(saved));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(loaded.ExportState()));
    }

    [Theory]
    [InlineData(1_000, 0, 0)]
    [InlineData(10_000, 3_000, 0)]
    [InlineData(10_000, 0, 5)]
    public async Task ColdIllOrHeavilyLoadedAgentDoesNotStartSwimming(int warmth, int illness, int load)
    {
        var start = new GridPoint(4, 2);
        var state = CrossingState(start);
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == Actor
                ? person with { Survival = new(warmth, illness) } : person).ToArray(),
        };
        if (load > 0)
            state = WithLoad(state, load);
        using var world = Restore(state);
        world.SubmitInstruction(new("unsafe-swim", "owner:test", Actor, OwnerInstructionKind.Order, "Move to tile (5, 2)"));
        for (var tick = 0; tick < 8; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True(world.ExportState().Map.IsPassable(Position(world)));
        }
        Assert.NotEqual("finished", Assert.Single(world.ExportState().Instructions!).Task!.Status);
        Assert.Empty(world.ExportState().DeceasedAgents ?? []);
    }

    [Fact]
    public async Task SeaStillRequiresABoatAndCannotBeSavedAsAnUnaccompaniedSwimmer()
    {
        var map = GeographyCandidateSelector.GenerateCandidate(new GeographyOptions("swimming-native", WorldSizePreset.Small));
        var bank = map.Tiles.Select(tile => tile.Position).First(point => map.IsBuildable(point) &&
            map.Contains(new(point.X + 1, point.Y)) && map.HydrologyAt(new(point.X + 1, point.Y)) == WaterKind.Ocean);
        var sea = new GridPoint(bank.X + 1, bank.Y);
        using var world = Restore(CrossingState(bank));
        world.SubmitInstruction(new("sea-swim", "owner:test", Actor, OwnerInstructionKind.Order,
            $"Move to tile ({sea.X}, {sea.Y})"));
        for (var tick = 0; tick < 8; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True(map.IsPassable(Position(world)));
        }
        var state = world.ExportState();
        Assert.NotEqual("finished", Assert.Single(state.Instructions!).Task!.Status);
        Assert.Throws<InvalidDataException>(() => Restore(state with
        {
            Agents = state.Agents.Select(person => person.AgentId == Actor
                ? person with { Position = sea } : person).ToArray(),
        }));
    }

    [Theory]
    [InlineData(1_000, 0, 0, "interrupted")]
    [InlineData(10_000, 3_000, 0, "finished")]
    [InlineData(10_000, 0, 5, "finished")]
    public async Task SwimmerCanStillLeaveWaterAfterTheirConditionChangesAcrossReload(int warmth, int illness, int load, string expectedOrderStatus)
    {
        using var setup = Restore(CrossingState(new(4, 2)));
        setup.SubmitInstruction(new("enter-water", "owner:test", Actor, OwnerInstructionKind.Order, "Move to tile (5, 2)"));
        for (var tick = 0; tick < 16 && Position(setup) != new GridPoint(5, 2); tick++)
            Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(new GridPoint(5, 2), Position(setup));
        var state = setup.ExportState();
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == Actor
                ? person with { Survival = new(warmth, illness) } : person).ToArray(),
        };
        if (load > 0) state = WithLoad(state, load);
        using var world = Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)));
        world.SubmitInstruction(new("leave-water", "owner:test", Actor, OwnerInstructionKind.Order, "Move to tile (4, 2)"));
        for (var tick = 0; tick < 24 && !world.ExportState().Map.IsPassable(Position(world)); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True(world.ExportState().Map.IsPassable(Position(world)));
        Assert.Equal(new GridPoint(4, 2), Position(world));
        Assert.Equal(expectedOrderStatus, Assert.Single(world.ExportState().Instructions!,
            order => order.IdempotencyKey == "leave-water").Task!.Status);
        Assert.Empty(world.ExportState().DeceasedAgents ?? []);
    }

    [Fact]
    public async Task ColdSwimmerReachesShoreRatherThanWaitingBesideAPublicShelterAcrossReload()
    {
        const string actor = "agent:00000000000000000000000000000042";
        var shore = new GridPoint(4, 2);
        var water = new GridPoint(5, 2);
        using var setup = Restore(CrossingState(new(3, 2)));
        var household = setup.AddAgent(actor, shore);
        Assert.Equal("household:" + actor, household);
        Assert.DoesNotContain(setup.WorldSimulation.Buildings, building => building.HouseholdId == household);
        setup.SubmitInstruction(new("shelter-enter-water", "owner:test", actor, OwnerInstructionKind.Order, "Move to tile (5, 2)"));
        for (var tick = 0; tick < 16 && Position(setup, actor) != water; tick++)
            Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(water, Position(setup, actor));
        var shelter = setup.WorldContent.Buildings.Single(building => building.LocalId == "shelter");
        var placed = setup.PlaceBuilding("swimmer-public-shelter", shelter.CanonicalId, shore);
        Assert.True(placed.Applied, placed.Failure);
        Assert.Null(setup.WorldSimulation.Buildings.Single(building => building.InstanceId == placed.InstanceId).HouseholdId);
        var state = setup.ExportState();
        Assert.True(SwimmingRules.IsSwimmingWater(state.Map, water));
        // A real unhoused swimmer is urgently cold, with public cover one tile away.
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { Survival = new SurvivalCondition(1_000) } : person).ToArray(),
        };
        state = WeatherTestFixture.WithWeather(state, WeatherKind.Rain);
        var provider = new WarmthProvider(actor);
        using var world = Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), provider);
        world.SubmitInstruction(new("shelter-seek-warmth", "owner:test", actor, OwnerInstructionKind.Suggestion, "Consider seeking nearby warmth."));
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = Restore(WorldRuntimeCodec.Decode(before), new WarmthProvider(actor));
        for (var tick = 0; tick < 24 && !world.ExportState().Map.IsPassable(Position(world, actor)); tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(provider.ChoseWarmth, "The real personal decision must choose the offered native warmth action.");
        // The shelter's south doorway is dry ground; its east wall faces the water.
        Assert.Equal(new GridPoint(shore.X, shore.Y + 1), Position(world, actor));
        var warmth = world.Agents.Single(person => person.AgentId == actor).Survival!.WarmthBasisPoints;
        for (var tick = 0; tick < 3; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(world.Agents.Single(person => person.AgentId == actor).Survival!.WarmthBasisPoints > warmth);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "agent_moved" && item.Detail.EndsWith(":warmth", StringComparison.Ordinal));
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var loaded = Restore(WorldRuntimeCodec.Decode(saved));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(loaded.ExportState()));
        Assert.Empty(world.ExportState().DeceasedAgents ?? []);
    }

    private static WorldRuntimeState WithLoad(WorldRuntimeState state, int units) => state with
    {
        Society = state.Society with
        {
            Society = state.Society.Society with
            {
                Inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "swim-cargo", "wood", Actor, units),
            },
        },
    };

    private static WorldRuntimeState CrossingState(GridPoint start, string seed = "swimming-native")
    {
        var options = new GeographyOptions(seed, WorldSizePreset.Small);
        using var setup = new WorldRuntime(options.Seed, _ => new IdleProvider(),
            startPace: WorldStartPace.FounderSetup, geographyOptions: options);
        setup.InitializeFirstTownContent();
        var map = setup.ExportState().Map;
        setup.AcceptFirstTownLayout(map.GetResource("berry-patch").Position);
        map = setup.ExportState().Map;
        Assert.True(map.IsBuildable(start));
        Assert.DoesNotContain(map.Resources, resource => resource.Position == start);
        Assert.DoesNotContain(map.MapObjects, item => item.Position == start);
        var definitions = setup.WorldContent.Buildings.ToDictionary(item => item.CanonicalId);
        var occupied = map.Resources.Select(item => item.Position)
            .Concat(map.MapObjects.Select(item => item.Position)).Concat(setup.RoadTiles)
            .Concat(setup.WorldSimulation.Buildings.SelectMany(building =>
                WorldContentSimulationRules.Footprint(definitions[building.DefinitionId], building.Position))).ToHashSet();
        var otherStarts = map.Tiles.Select(tile => tile.Position)
            .Where(point => map.IsBuildable(point) && point != start && !occupied.Contains(point))
            .OrderBy(point => map.FootDistance(point, map.GetResource("berry-patch").Position))
            .ThenBy(point => point.Y).ThenBy(point => point.X).Take(3).ToArray();
        GridPoint[] starts = [start, .. otherStarts];
        for (var index = 0; index < starts.Length; index++)
            setup.PlaceFounder($"founder:0000000000000000000000000000000{index + 1}", starts[index]);
        setup.StartWorld();
        var state = setup.ExportState();
        return WeatherTestFixture.WithWeather(state with
        {
            Survival = new SurvivalState(0, []),
            Agents = state.Agents.Select(person => person with
            {
                FullnessBasisPoints = 10_000,
                Survival = new SurvivalCondition(WarmthBasisPoints: 10_000),
            }).ToArray(),
        }, WeatherKind.Clear);
    }

    private static WorldRuntime Restore(WorldRuntimeState state, IDecisionProvider? provider = null) =>
        WorldRuntime.Restore(state, _ => provider ?? new IdleProvider());
    private static GridPoint Position(WorldRuntime world, string actor = Actor) => world.Agents.Single(person => person.AgentId == actor).Position;

    private sealed class WarmthProvider(string actor) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        internal bool ChoseWarmth { get; private set; }

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var choice = request.Observation.Candidates.Any(candidate => candidate.Id == "seek_warmth") ? "seek_warmth" : "safe_idle";
            if (request.Observation.AgentId == actor && choice == "seek_warmth") ChoseWarmth = true;
            return new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with { Candidates = request.Observation.Candidates.Where(candidate => candidate.Id == choice).ToArray() },
            }, cancellationToken);
        }
    }

    private sealed class IdleProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default) =>
            new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with { Candidates = request.Observation.Candidates.Where(item => item.Id == "safe_idle").ToArray() },
            }, cancellationToken);
    }
}
