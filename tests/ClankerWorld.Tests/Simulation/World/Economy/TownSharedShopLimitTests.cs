using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TownSharedShopLimitTests
{
    private const string Alpha = "household:first";
    private const string Beta = "household:second";
    private static readonly string[] ShopKinds = ["store", "tailor", "restaurant", "clinic", "blacksmith"];
    private static readonly int[] Populations = [4, 8, 9, 17];
    private static readonly Lazy<WorldRuntimeState> Baseline = new(() =>
    {
        using var world = NormalPathWorld.CreateGenerated("probe-a", _ => new ActionCoverageRecorder(chooseIdle: true));
        var smith = world.WorldSimulation.Buildings.Single(item => item.InstanceId == "first-town-blacksmith");
        Assert.True(world.RemoveBuilding(smith.InstanceId, smith.TownId, smith.HouseholdId).Applied);
        return world.ExportState();
    });
    private static readonly Dictionary<int, Lazy<WorldRuntimeState>> PopulationStates =
        Populations.ToDictionary(count => count, count => new Lazy<WorldRuntimeState>(() => WithResidents(count)));

    public static IEnumerable<object[]> ShopLimits =>
        from kind in ShopKinds
        from population in Populations
        select new object[] { kind, population, (population + 7) / 8 };

    [Theory]
    [MemberData(nameof(ShopLimits))]
    public async Task OffersAllSizesOnlyBelowTheTownLimitAndKeepsExistingShops(string kind, int population, int limit)
    {
        var state = PopulationStates[population].Value;
        state = Stock(state);
        using var setup = WorldRuntime.Restore(state);
        var definition = setup.WorldContent.Buildings.First(item => HouseholdBuildingKinds.KindOf(item) == kind);
        for (var index = 0; index < limit - 1; index++) PlaceShop(setup, definition, index);
        var below = setup.ExportState();
        var recorder = new ActionCoverageRecorder(chooseIdle: true);
        using (var offered = WorldRuntime.Restore(below, _ => recorder))
        {
            await Observe(offered);
            Assert.NotEmpty(ShopOffers(recorder, offered, kind));
        }

        PlaceShop(setup, definition, limit - 1);
        var atLimit = new ActionCoverageRecorder(chooseIdle: true);
        using (var atCapacity = WorldRuntime.Restore(setup.ExportState(), _ => atLimit))
        {
            await Observe(atCapacity);
            Assert.Empty(ShopOffers(atLimit, atCapacity, kind));
        }
        // An older world may already exceed the new limit. Loading and ticking
        // must preserve those paid buildings while withholding every size.
        PlaceShop(setup, definition, limit);
        var existing = setup.WorldSimulation.Buildings.Where(item => item.DefinitionId == definition.CanonicalId).ToArray();
        var blocked = new ActionCoverageRecorder(chooseIdle: true);
        using var full = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(setup.ExportState())), _ => blocked);
        await Observe(full);
        Assert.Empty(ShopOffers(blocked, full, kind));
        Assert.All(existing, building => Assert.Contains(building, full.WorldSimulation.Buildings));
        Assert.NotEmpty(blocked.OfferedByAgent);
        full.Validate();
    }

    [Theory]
    [InlineData("store")]
    [InlineData("tailor")]
    [InlineData("restaurant")]
    [InlineData("clinic")]
    [InlineData("blacksmith")]
    public async Task TwoHouseholdsReplyingTogetherHoldOnlyOneSlotIncludingDuringConstruction(string kind)
    {
        var state = Stock(Baseline.Value);
        var first = state.Society.Society.Agents.First(person => person.HouseholdId == Alpha).Id;
        var second = state.Society.Society.Agents.First(person => person.HouseholdId == Beta).Id;
        var providers = new[] { new ScriptedProvider(), new ScriptedProvider() };
        var calls = providers.Select(provider => provider.Hold()).ToArray();
        using var world = WorldRuntime.Restore(state, id => id == first ? providers[0] :
            id == second ? providers[1] : new ActionCoverageRecorder(chooseIdle: true));
        var harness = ModelWorkHarness.For(world);
        for (var tick = 0; tick < 4 && calls.Any(call => !call.IsEntered); tick++)
            Assert.True((await harness.TickAsync()).Advanced);
        Assert.All(calls, call => Assert.True(call.IsEntered));
        var choices = calls.Select(call => call.Request.Observation.Candidates.Where(candidate =>
            TownConstructionCandidateIds.TryParse(candidate.Id, out var selection) && selection.IsBuilding &&
            world.WorldContent.Buildings.Any(definition => definition.CanonicalId == selection.DefinitionId &&
                HouseholdBuildingKinds.KindOf(definition) == kind)).ToArray()).ToArray();
        Assert.All(choices, offers => Assert.NotEmpty(offers));
        // Use different sites, so the Town capacity is what refuses the loser.
        var firstChoice = choices[0][0].Id;
        Assert.True(TownConstructionCandidateIds.TryParse(firstChoice, out var firstSelection));
        var firstDesign = world.WorldContent.Buildings.Single(item => item.CanonicalId == firstSelection.DefinitionId);
        var firstFootprint = WorldContentSimulationRules.Footprint(firstDesign, firstSelection.SitePosition!.Value).ToHashSet();
        var secondChoice = choices[1].First(item =>
        {
            if (!TownConstructionCandidateIds.TryParse(item.Id, out var selection)) return false;
            var design = world.WorldContent.Buildings.Single(item => item.CanonicalId == selection.DefinitionId);
            return !WorldContentSimulationRules.Footprint(design, selection.SitePosition!.Value).Any(firstFootprint.Contains);
        }).Id;
        var chosen = new[] { firstChoice, secondChoice };
        calls[0].Reply(chosen[0]);
        calls[1].Reply(chosen[1]);
        await harness.WaitForAllAsync();
        var admissionTick = world.WorldTick + 1;
        Assert.True((await harness.TickAsync()).Advanced);
        var project = Assert.Single(world.ExportState().Agents, person =>
            person.Project is { Stage: not ("cancelled" or "completed") } plan && chosen.Contains(plan.CandidateId));
        Assert.DoesNotContain(world.WorldSimulation.Buildings, building =>
            world.WorldContent.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId &&
                HouseholdBuildingKinds.KindOf(definition) == kind));
        Assert.Contains(harness.Transitions, transition => transition.Stage == ModelWorkStage.Admitted &&
            transition.ActorId == project.AgentId && transition.AdmittedTick == admissionTick);

        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        var recorder = new ActionCoverageRecorder(chooseIdle: true);
        using var continued = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => recorder);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(continued.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new ActionCoverageRecorder(chooseIdle: true));
        var continuedHarness = ModelWorkHarness.For(continued);
        var replayHarness = ModelWorkHarness.For(replay);
        for (var tick = 0; tick < 4; tick++) await continuedHarness.AdvanceTogetherAsync(replayHarness);
        var otherHousehold = continued.Society.GetAgent(project.AgentId).HouseholdId == Alpha ? Beta : Alpha;
        Assert.Contains(recorder.OfferedByAgent.Keys,
            actor => continued.Society.GetAgent(actor).HouseholdId == otherHousehold);
        Assert.Empty(ShopOffers(recorder, continued, kind, otherHousehold));
        Assert.NotEqual("cancelled", continued.ExportState().Agents.Single(person => person.AgentId == project.AgentId).Project!.Stage);
    }

    private static WorldRuntimeState WithResidents(int population)
    {
        var state = population == 4 ? Baseline.Value : PopulationStates[Populations.Last(count => count < population)].Value;
        for (var index = state.Towns!.Single().ResidentIds.Count; index < population; index++)
            state = SecondGenerationFixture.Add(state, key: "shop-" + index).State;
        Assert.Equal(population, Assert.Single(state.Towns!).ResidentIds.Count);
        return state;
    }

    private static WorldRuntimeState Stock(WorldRuntimeState state)
    {
        var inventory = state.Society.Society.Inventory;
        foreach (var household in new[] { Alpha, Beta })
        {
            foreach (var kind in state.WorldContent!.Buildings.SelectMany(item => item.BuildCosts)
                .Select(cost => cost.ResourceId).Distinct())
                inventory = InventoryAuthority.AddLot(inventory, DerivedId.Create($"shop-test:{household}:{kind}"),
                    kind, household, 100);
            // Supply actual work inputs so these cases isolate the Town cap.
            var house = state.WorldSimulation!.Buildings.Single(item => item.HouseholdId == household &&
                item.InstanceId.Contains("house-", StringComparison.Ordinal));
            foreach (var (item, quantity) in new[]
            {
                ("fiber", 3), ("medicinal_herbs", 2), ("water_jug", 1), ("bread", 1), ("cultivated_greens", 1),
                ("food", state.Towns!.Single().ResidentIds.Count * FarmFieldRules.MealsPerPersonPerDay),
            })
                inventory = InventoryAuthority.AddLot(inventory, DerivedId.Create($"shop-work:{household}:{item}"),
                    item, household, quantity, storageBuildingId: house.InstanceId);
            inventory = InventoryAuthority.AddLot(inventory, DerivedId.Create($"shop-work:{household}:water"),
                "fresh_water", household, 1, storageBuildingId: house.InstanceId,
                containerLotId: DerivedId.Create($"shop-work:{household}:water_jug"));
        }
        return state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
    }

    private static void PlaceShop(WorldRuntime world, BuildingDefinition definition, int index)
    {
        var id = "shop-test-" + index;
        var map = world.ExportState().Map;
        var town = world.Towns.Single();
        Assert.Contains(map.Tiles.OrderBy(tile => map.FootDistance(tile.Position, town.OriginSite!.Value)),
            tile => world.PlaceBuilding(id, definition.CanonicalId, tile.Position, Alpha).Applied);
        Assert.Equal(world.Towns.Single().Id, world.WorldSimulation.Buildings.Single(item => item.InstanceId == id).TownId);
    }

    private static IEnumerable<string> ShopOffers(ActionCoverageRecorder recorder, WorldRuntime world, string kind,
        string household = Beta) => world.Society.Agents.Where(person => person.HouseholdId == household)
        .SelectMany(person => recorder.OfferedByAgent.TryGetValue(person.Id, out var offers) ? offers.Keys : [])
        .Where(id => TownConstructionCandidateIds.TryParse(id, out var selection) && selection.IsBuilding &&
            world.WorldContent.Buildings.Any(definition => definition.CanonicalId == selection.DefinitionId &&
                HouseholdBuildingKinds.KindOf(definition) == kind));

    private static async Task Observe(WorldRuntime world)
    {
        var harness = ModelWorkHarness.For(world);
        for (var tick = 0; tick < 4; tick++) Assert.True((await harness.TickAsync()).Advanced);
    }
}
