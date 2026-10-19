using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class WaterJugReturnSelectionTests
{
    private const string House = "first-town-house-a";
    private const string Claim = "held-other-jug";
    private static readonly Lazy<byte[]> Baseline = new(() =>
    {
        using var setup = NormalPathWorld.CreateGenerated("jug-return-selection-audit", _ => new ReturnChoices(false));
        return WorldRuntimeCodec.Encode(ShelterOrderTestFixture.WithClearWeather(setup.ExportState()));
    });

    [Theory]
    [InlineData(true, "contents", false, false, -1)]
    [InlineData(false, "contents", false, false, -1)]
    [InlineData(true, "none", false, false, -1)]
    [InlineData(true, "contents", true, false, -1)]
    [InlineData(true, "contents", false, true, -1)]
    [InlineData(true, "contents", false, false, 2)]
    public async Task AnOfferedReturnMovesItsEligibleJugAndPreservesTheOtherFamilyAcrossReload(
        bool otherFirst, string heldPart, bool returnedFull, bool walk, int houseRoom)
    {
        var (state, actor, other, returned) = Prepared(otherFirst, heldPart, returnedFull, walk, houseRoom);
        var policy = new ReturnChoices();
        var saved = WorldRuntimeCodec.Encode(state);
        using var world = Restore(WorldRuntimeCodec.Decode(saved), actor, policy);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
        var permitted = true;
        var prepared = false;
        Assert.False((await world.TickModelWorkAsync(() => permitted, (proposed, events) =>
        {
            prepared = true;
            Assert.Contains(events, item => item.Kind == (walk ? "agent_moved" : "water_jug_returned") &&
                item.Detail.StartsWith(actor + ":", StringComparison.Ordinal));
            permitted = false;
            return [];
        })).Advanced);
        Assert.True(prepared);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
        var original = state.Society.Society.Inventory;
        var quantity = original.Lots.Sum(lot => lot.Quantity);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Contains("return_water_jug", policy.Offered);
        if (!walk) Assert.Equal(House, world.Society.Inventory.GetLot(returned).StorageBuildingId);
        if (walk)
        {
            Assert.Contains(world.ExportState().Events, item => item.Kind == "agent_moved" && item.Detail.StartsWith(actor + ":", StringComparison.Ordinal));
            Assert.Equal(actor, world.Society.Inventory.GetLot(returned).OwnerId);
        }
        using var replay = Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), actor, new ReturnChoices());
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        for (var tick = 0; tick < (heldPart == "none" ? 0 : 3); tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        var home = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == House).HouseholdId;
        foreach (var id in new[] { returned, returned + "-water" })
        {
            var actual = world.Society.Inventory.GetLot(id);
            Assert.Equal(original.GetLot(id) with { OwnerId = home!, StorageBuildingId = House, LastProcessedTick = actual.LastProcessedTick }, actual);
        }
        foreach (var id in new[] { other, other + "-water" })
        {
            var actual = world.Society.Inventory.GetLot(id);
            Assert.Equal(original.GetLot(id) with { LastProcessedTick = actual.LastProcessedTick }, actual);
        }
        if (heldPart != "none") Assert.Equal(original.GetReservation(Claim), world.Society.Inventory.GetReservation(Claim));
        Assert.Equal(quantity, world.Society.Inventory.Lots.Sum(lot => lot.Quantity));
        Assert.Single(world.ExportState().Events, item => item.Kind == "water_jug_returned" && item.Detail == $"{actor}:{returned}:{House}");
        world.Validate();
    }

    [Theory]
    [InlineData("all-reserved")]
    [InlineData("full-house")]
    public async Task AReturnWithoutAnUnreservedFamilyOrHouseRoomMovesNothing(string boundary)
    {
        var (state, actor, _, returned) = Prepared(true, "contents", false, false, boundary == "full-house" ? 0 : -1);
        var inventory = state.Society.Society.Inventory;
        if (boundary == "all-reserved") inventory = InventoryAuthority.Reserve(inventory, "held-return-jug", actor,
            returned + "-water", 1, "other_work", state.Society.Society.WorldTick + 1_000);
        state = WithInventory(state, inventory);
        var policy = new ReturnChoices();
        using var world = Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), actor, policy);
        for (var tick = 0; tick < 3; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.NotEmpty(policy.Offered);
        Assert.DoesNotContain("return_water_jug", policy.Offered);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "water_jug_returned");
        foreach (var lot in inventory.Lots.Where(lot => lot.OwnerId == actor))
        {
            var actual = world.Society.Inventory.GetLot(lot.Id);
            Assert.Equal(lot with { LastProcessedTick = actual.LastProcessedTick }, actual);
        }
        Assert.Equal(inventory.GetReservation(Claim), world.Society.Inventory.GetReservation(Claim));
        if (boundary == "all-reserved")
            Assert.Equal(inventory.GetReservation("held-return-jug"), world.Society.Inventory.GetReservation("held-return-jug"));
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var reload = Restore(WorldRuntimeCodec.Decode(bytes), actor, new ReturnChoices());
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reload.ExportState()));
        world.Validate();
    }

    [Fact]
    public void ReturnFixtureCopiesKeepIndependentPeopleStockAndTerrain()
    {
        var first = Prepared(true, "contents", false, false, -1);
        var expected = WorldRuntimeCodec.Encode(first.State);
        var second = Prepared(true, "contents", false, false, -1);
        Assert.Equal((first.Actor, first.Other, first.Returned), (second.Actor, second.Other, second.Returned));
        Assert.Equal(expected, WorldRuntimeCodec.Encode(second.State));
        Assert.NotSame(first.State.Agents, second.State.Agents);
        Assert.NotSame(first.State.Society.Society.Inventory.Lots, second.State.Society.Society.Inventory.Lots);
        Assert.NotSame(first.State.Map.ElevationLevels, second.State.Map.ElevationLevels);
        Assert.Equal((first.Actor, 1), (second.State.Society.Society.Inventory.GetLot(second.Returned + "-water").OwnerId,
            second.State.Society.Society.Inventory.GetLot(second.Returned + "-water").Quantity));
        Assert.Contains(second.State.Society.Society.Inventory.Reservations, item => item.Id == Claim && item.Quantity == 1);

        var people = Assert.IsAssignableFrom<IList<AgentState>>(first.State.Agents);
        people[0] = people[0] with { FullnessBasisPoints = 1 };
        var stock = Assert.IsAssignableFrom<IList<InventoryLot>>(first.State.Society.Society.Inventory.Lots);
        stock[0] = stock[0] with { Quantity = stock[0].Quantity + 1 };
        first.State.Map.ElevationLevels![^1] ^= 1;
        Assert.Equal(expected, WorldRuntimeCodec.Encode(second.State));
        Assert.Equal(expected, WorldRuntimeCodec.Encode(Prepared(true, "contents", false, false, -1).State));
        using var restored = Restore(second.State, second.Actor, new ReturnChoices(false));
        Assert.Equal(expected, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Fact]
    public void RepeatedReturnSetupFitsItsAllocationBudget()
    {
        _ = Prepared(true, "contents", false, false, -1);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var fixture = Prepared(true, "contents", false, false, -1);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 1, 24L * 1024 * 1024);
        GC.KeepAlive(fixture.State);
    }

    private static (WorldRuntimeState State, string Actor, string Other, string Returned) Prepared(
        bool otherFirst, string heldPart, bool returnedFull, bool walk, int houseRoom)
    {
        var state = WorldRuntimeCodec.Decode(Baseline.Value);
        var house = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == House);
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == house.HouseholdId &&
            person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder).Id;
        var origin = walk ? state.Map.FootNeighbors(house.Position).First(point =>
            (point.X == house.Position.X || point.Y == house.Position.Y) && state.Map.IsPassable(point) &&
            state.Agents.All(person => person.Position != point)) : house.Position;
        var previous = state.Agents.Single(person => person.AgentId == actor).Position;
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                Position = origin,
                FullnessBasisPoints = 10_000,
                Equipment = null,
                Survival = person.Survival is { } survival
                    ? survival with { WarmthBasisPoints = 10_000, NutritionBasisPoints = 10_000 } : null
            } : person.Position == origin ? person with { Position = previous } : person).ToArray()
        };
        var other = otherFirst ? "jug-a" : "jug-b";
        var returned = otherFirst ? "jug-b" : "jug-a";
        var inventory = state.Society.Society.Inventory;
        foreach (var (id, water) in new[] { (other, returnedFull ? 1 : 4), (returned, returnedFull ? 4 : 1) })
        {
            inventory = InventoryAuthority.AddLot(inventory, id, InventoryContainerRules.WaterJug, actor, 1);
            inventory = InventoryAuthority.AddLot(inventory, id + "-water", InventoryContainerRules.FreshWater, actor, water, containerLotId: id);
        }
        var free = PersonalEquipmentRules.FreeCapacity(inventory, actor, null);
        if (free > 0) inventory = InventoryAuthority.AddLot(inventory, "carried-stone", "stone", actor, free);
        Assert.Equal(0, PersonalEquipmentRules.FreeCapacity(inventory, actor, null));
        if (heldPart != "none") inventory = InventoryAuthority.Reserve(inventory, Claim, actor,
            other + "-water", 1, "other_work", state.Society.Society.WorldTick + 1_000);
        var definition = state.WorldContent!.Buildings.Single(item => item.CanonicalId == house.DefinitionId);
        var room = BuildingStorageRules.Capacity(definition, house)!.Value - inventory.Lots.Where(lot => lot.StorageBuildingId == House).Sum(lot => lot.Quantity);
        Assert.True(room >= 5);
        if (houseRoom >= 0) inventory = InventoryAuthority.AddLot(inventory, "house-stone", "stone", house.HouseholdId!, room - houseRoom, storageBuildingId: House);
        return (WithInventory(state, inventory), actor, other, returned);
    }

    private static WorldRuntimeState WithInventory(WorldRuntimeState state, InventoryCheckpoint inventory) =>
        state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };

    private static WorldRuntime Restore(WorldRuntimeState state, string actor, ReturnChoices policy) =>
        WorldRuntime.Restore(state, id => id == actor ? policy : new ReturnChoices(false));

    private sealed class ReturnChoices(bool chooseReturn = true) : IDecisionProvider
    {
        public ConcurrentBag<string> Offered { get; } = [];
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            foreach (var candidate in request.Observation.Candidates) Offered.Add(candidate.Id);
            var choice = chooseReturn ? request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id == "return_water_jug") : null;
            choice ??= request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with
            { Observation = request.Observation with { Candidates = [choice] } }, cancellationToken);
        }
    }
}
