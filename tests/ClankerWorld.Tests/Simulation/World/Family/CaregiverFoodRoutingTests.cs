using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed partial class CaregiverFoodRoutingTests
{
    [Theory]
    [InlineData("berries", 2_000)]
    [InlineData("wild_greens", 1_500)]
    [InlineData("cultivated_greens", 4_000)]
    public async Task FeedingAnInfantKeepsTheNamedFoodsNourishmentAcrossReload(string kind, int nourishment)
    {
        var (state, actor, childId) = PreparedChildCareState(kind);
        var bytes = WorldRuntimeCodec.Encode(state);
        var provider = new CareProvider(actor);
        using var caring = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => provider);
        using var waiting = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new CareProvider("no-caregiver"));
        var step = await caring.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        var plan = Assert.IsType<PracticalCareActionPlan>(provider.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == actor).ActionPlan);
        Assert.Equal(childId, plan.Choice.DependentId);
        Assert.Equal("named-child-serving", Assert.IsType<ConsumeFoodActionPlan>(plan.Food).StockId);
        Assert.True((await waiting.AdvanceOneTickAsync()).Advanced);
        var child = caring.Agents.Single(person => person.AgentId == childId);
        Assert.Equal(nourishment, child.FullnessBasisPoints - waiting.Agents.Single(person => person.AgentId == childId).FullnessBasisPoints);
        Assert.DoesNotContain(caring.Society.Inventory.Lots, lot => lot.Id == "named-child-serving");
        Assert.Equal(1, waiting.Society.Inventory.GetLot("named-child-serving").Quantity);
        Assert.Contains(caring.ExportState().Events, item => item.Kind == "child_cared_for" && item.Detail == childId);
        using var restored = FarmFieldTests.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(caring.ExportState())));
        Assert.Equal(child.FullnessBasisPoints, restored.Agents.Single(person => person.AgentId == childId).FullnessBasisPoints);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CaregiverFeedsInfantWhenStoredFoodCannotBeReached(bool blockStorage)
    {
        var options = new GeographyOptions("island-fuel-review-0", WorldSizePreset.Small);
        using var initial = new WorldRuntime(options.Seed, startPace: WorldStartPace.FounderSetup, geographyOptions: options);
        initial.InitializeFirstTownContent();
        initial.AcceptFirstTownLayout(new GridPoint(136, 14));
        var setupMap = initial.ExportState().Map;
        var storagePosition = initial.WorldSimulation.Buildings.Single(building => building.InstanceId == "first-town-house-a").Position;
        var localFood = setupMap.Resources.Where(resource => resource.Kind is "food" or "fruit" &&
            setupMap.IsReachableOnFoot(storagePosition, resource.Position) && setupMap.FootDistance(storagePosition, resource.Position) > 3)
            .OrderBy(resource => setupMap.FootDistance(storagePosition, resource.Position))
            .First(resource => setupMap.Tiles.Any(tile => setupMap.IsBuildable(tile.Position) && setupMap.FootDistance(tile.Position, resource.Position) <= 1 &&
                !setupMap.Resources.Any(item => item.Position == tile.Position))).Position;
        var actorPosition = setupMap.Tiles.First(tile => setupMap.IsBuildable(tile.Position) &&
            setupMap.FootDistance(tile.Position, storagePosition) > 1 && setupMap.FootDistance(tile.Position, localFood) <= 1 &&
            !setupMap.Resources.Any(resource => resource.Position == tile.Position)).Position;
        var positions = new[] { actorPosition, new GridPoint(131, 9), new GridPoint(132, 9), new GridPoint(133, 9) };
        var ids = Enumerable.Range(1, 4).Select(index => $"founder:{index:D32}").ToArray();
        for (var index = 0; index < 4; index++) initial.PlaceFounder(ids[index], positions[index]);
        initial.StartWorld();
        var state = blockStorage ? StorageRoutingTestFixture.BlockAccess(initial, storagePosition, 1) : initial.ExportState();
        var household = state.Society.Society.GetAgent(ids[0]).HouseholdId!;
        var society = ChosenBirthNameTestFixture.NameParent(state.Society.Society, ids[0]);
        society = SocietyRules.ProposeRelationship(society, new("care-parents", 1,
            SocietyRelationshipType.Partnership, ids[0], ids[1], society.WorldTick)).Checkpoint;
        society = SocietyRules.AcceptRelationship(society, "care-parents", 1, ids[1]).Checkpoint;
        var birthFood = society.Inventory.Lots.First(lot => lot.OwnerId == household && lot.ItemKind == "food" && lot.Quantity >= 4);
        var birth = SocietyRules.CommitBirth(society, new($"family:{ids[0]}:{society.WorldTick}", 1,
            ids[0], ids[1], household, [ids[0], ids[1]], [ids[0], ids[1]], birthFood.Id, 4, society.WorldTick,
            ChildName: ChosenBirthNameTestFixture.ChildName(society, ids[0], "Ari"), PrimaryCaregiverId: ids[0]));
        Assert.Equal("first-town-house-a", birthFood.StorageBuildingId);
        var childId = Assert.IsType<string>(birth.CreatedId);
        society = birth.Checkpoint;
        var childPosition = state.Map.Tiles.First(tile => state.Map.IsBuildable(tile.Position) &&
            state.Map.FootDistance(tile.Position, actorPosition) == 1 &&
            !state.Agents.Any(person => person.Position == tile.Position)).Position;
        state = state with
        {
            Agents = state.Agents.Append(new AgentState(childId, childPosition, 1_000, 0,
                "curious", "grow", Survival: new SurvivalCondition())).ToArray(),
            Towns = state.Towns!.Select(town => town.ResidentIds.Contains(ids[0])
                ? town with { ResidentIds = town.ResidentIds.Append(childId).Order(StringComparer.Ordinal).ToArray() } : town).ToArray(),
        };
        var storedFood = society.Inventory.Lots.Where(lot => lot.OwnerId == household && lot.ItemKind == "food").Sum(lot => lot.Quantity);
        Assert.True(storedFood > 0);
        var systems = state.WorldSystems!;
        state = state with
        {
            Society = state.Society with { Society = society },
            Survival = new SurvivalState(0, []),
            Agents = state.Agents.Select(person => person with
            {
                FullnessBasisPoints = person.AgentId == childId ? 1_000 : 9_000,
                Survival = new SurvivalCondition(WarmthBasisPoints: 10_000),
            }).ToArray(),
            WorldSystems = systems with
            {
                RegionalWeather = null,
                Config = systems.Config with { WeatherProfiles = Enum.GetValues<SeasonKind>().Select(season => new WeatherProfile(season, 1, 0, 0, 0, 0)).ToArray() },
                Climate = systems.Climate with { Weather = WeatherKind.Clear },
            },
        };
        state = WeatherTestFixture.WithWeather(state, WeatherKind.Clear);
        var provider = new CareProvider(ids[0]);
        var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => provider);
        try
        {
            // Named seasonal plants may send the caregiver to a farther ripe
            // source. Allow the real outward and return journey to finish.
            for (var tick = 0; tick < 80 &&
                world.Agents.Single(person => person.AgentId == childId).FullnessBasisPoints < 3_000; tick++)
            {
                Assert.True((await world.AdvanceOneTickAsync()).Advanced);
                if (tick == 5)
                {
                    var saved = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()));
                    world.Dispose();
                    world = WorldRuntime.Restore(saved, _ => provider);
                }
            }
            var result = world.ExportState();
            var child = result.Agents.Single(person => person.AgentId == childId);
            Assert.True(child.FullnessBasisPoints >= 3_000,
                $"child fullness={child.FullnessBasisPoints}; care={result.Events.Count(item => item.Kind == "child_cared_for")}; blocked={result.Events.Count(item => item.Kind == "movement_blocked")}; parent={result.Agents.Single(person => person.AgentId == ids[0]).Position}; care choices={provider.CareChoices}; food site={localFood}; home={storagePosition}\n" +
                string.Join("\n", result.Events.Where(item => item.Detail.Contains(ids[0], StringComparison.Ordinal)).TakeLast(20)
                    .Select(item => $"{item.WorldTick} {item.Kind}: {item.Detail}")));
            Assert.Contains(result.Events, item => item.Kind == "child_cared_for" && item.Detail == childId);
            var remainingFood = result.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId == household && lot.ItemKind == "food").Sum(lot => lot.Quantity);
            if (blockStorage)
            {
                Assert.Equal(storedFood, remainingFood);
                Assert.Contains(result.Events, item => item.Kind == "food_harvested" && item.Detail.StartsWith(ids[0] + ":", StringComparison.Ordinal));
            }
            else Assert.True(remainingFood < storedFood);
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(result)));
            restored.Validate();
        }
        finally { world.Dispose(); }
    }

    private static (WorldRuntimeState State, string Actor, string Child) PreparedChildCareState(string kind)
    {
        var (state, actor, household, point) = FarmFieldTests.PreparedFarmer("named-infant-food");
        var society = ChosenBirthNameTestFixture.NameParent(state.Society.Society, actor);
        var partner = society.Agents.First(person => person.HouseholdId == household && person.Id != actor).Id;
        society = SocietyRules.ProposeRelationship(society, new("named-care-parents", 1,
            SocietyRelationshipType.Partnership, actor, partner, society.WorldTick)).Checkpoint;
        society = SocietyRules.AcceptRelationship(society, "named-care-parents", 1, partner).Checkpoint;
        var birthFood = society.Inventory.Lots.First(lot => lot.OwnerId == household && lot.ItemKind == "food" && lot.Quantity >= 4);
        var birth = SocietyRules.CommitBirth(society, new($"family:{actor}:{society.WorldTick}", 1,
            actor, partner, household, [actor, partner], [actor, partner], birthFood.Id, 4, society.WorldTick,
            ChildName: ChosenBirthNameTestFixture.ChildName(society, actor, "Ari"), PrimaryCaregiverId: actor));
        var childId = Assert.IsType<string>(birth.CreatedId);
        society = birth.Checkpoint;
        var childPosition = state.Map.FootNeighbors(point).First(tile => state.Map.IsBuildable(tile) &&
            !state.Agents.Any(person => person.Position == tile));
        society = society with { Inventory = InventoryAuthority.AddLot(society.Inventory, "named-child-serving", kind, actor, 1) };
        state = state with
        {
            Society = state.Society with { Society = society },
            Survival = new SurvivalState(0, []),
            Agents = state.Agents.Select(person => person with
            {
                FullnessBasisPoints = 9_000,
                Survival = new SurvivalCondition(10_000),
            }).Append(new AgentState(childId, childPosition, 1_000, 0, "curious", "grow",
                Survival: new SurvivalCondition(10_000))).OrderBy(person => person.AgentId, StringComparer.Ordinal).ToArray(),
            Towns = state.Towns!.Select(town => town.ResidentIds.Contains(actor) ? town with
            {
                ResidentIds = town.ResidentIds.Append(childId).Order(StringComparer.Ordinal).ToArray(),
            } : town).ToArray(),
        };
        state = WeatherTestFixture.WithWeather(state, WeatherKind.Clear);
        return (state, actor, childId);
    }

    private sealed class CareProvider(string actor) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public int CareChoices { get; private set; }
        public PracticalCareActionPlan? Plan { get; private set; }
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var selected = request.Observation.AgentId == actor
                ? request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id.StartsWith("care:", StringComparison.Ordinal)) : null;
            if (selected is not null)
            {
                CareChoices++;
                Plan = Assert.IsType<PracticalCareActionPlan>(selected.ActionPlan);
            }
            selected ??= request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with { Candidates = [selected] },
            }, cancellationToken);
        }
    }
}
