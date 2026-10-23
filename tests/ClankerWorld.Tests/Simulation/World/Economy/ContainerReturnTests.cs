using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class ContainerReturnTests
{
    private const string Alpha = "household:first";
    private const string House = "first-town-house-a";
    private const string Clinic = "container-return-clinic";
    private const string Vessel = "container-return-vessel";
    private const string UsedContents = "container-return-used-contents";
    private const string UsedReservation = "container-return-used-input";

    [Theory]
    [InlineData(InventoryContainerRules.StoragePot)]
    [InlineData(InventoryContainerRules.WaterJug)]
    public async Task EmptyHouseholdVesselIsWalkedFromItsWorkplaceAndDeliveredHomeAcrossReload(string kind)
    {
        var state = Prepared(kind);
        var actor = Actor(state);
        var house = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == House);
        var workplace = state.WorldSimulation.Buildings.Single(building => building.InstanceId == Clinic);
        var original = state.Society.Society.Inventory.GetLot(Vessel);
        var consumed = state.Society.Society.Inventory.GetReservation(UsedReservation);
        var provider = new VesselChoices("return_empty_vessel:", "haul_household_stock");
        using var world = Restore(state, actor, provider);
        Assert.Equal(house.Position, world.Agents.Single(person => person.AgentId == actor).Position);
        Assert.NotEqual(house.Position, workplace.Position);
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == UsedContents);
        Assert.Equal(InventoryReservationState.Completed, world.Society.Inventory.GetReservation(UsedReservation).State);
        await AdvanceUntil(world, () => world.Society.Inventory.GetLot(Vessel).OwnerId == actor, 120);

        Assert.Contains("return_empty_vessel:" + Vessel, provider.Offered);
        Assert.Equal(workplace.Position, world.Agents.Single(person => person.AgentId == actor).Position);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "agent_moved" &&
            item.Detail.StartsWith(actor + ":", StringComparison.Ordinal));
        Assert.Single(world.ExportState().Events, item => item.Kind == "empty_vessel_picked_up" &&
            item.Detail == $"{actor}:{Vessel}:{Clinic}:{House}");
        var carried = world.Society.Inventory.GetLot(Vessel);
        Assert.Equal(original with
        {
            OwnerId = actor,
            StorageBuildingId = null,
            DeliveryBuildingId = House,
            Holds = [new(InventoryHoldPurpose.Delivery, House)],
            LastProcessedTick = carried.LastProcessedTick
        }, carried);
        Assert.Equal(1, world.Society.Inventory.Lots.Where(lot => lot.Id == Vessel || lot.ContainerLotId == Vessel)
            .Sum(lot => lot.Quantity));

        using var replay = Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())),
            actor, new VesselChoices("return_empty_vessel:", "haul_household_stock"));
        for (var tick = 0; tick < 120 && world.Society.Inventory.GetLot(Vessel).StorageBuildingId != House; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        var returned = world.Society.Inventory.GetLot(Vessel);
        Assert.Equal((Alpha, House, null, 1), (returned.OwnerId, returned.StorageBuildingId, returned.DeliveryBuildingId, returned.Quantity));
        Assert.Equal(house.Position, world.Agents.Single(person => person.AgentId == actor).Position);
        Assert.Equal(original.Id, returned.Id);
        Assert.Equal(original.ProvenanceLotId, returned.ProvenanceLotId);
        Assert.Equal(original.ConditionBasisPoints, returned.ConditionBasisPoints);
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.ContainerLotId == Vessel);
        Assert.Single(world.ExportState().Events, item => item.Kind == "household_stock_delivered" &&
            item.Detail == $"{actor}:{Vessel}:1:{House}");
        Assert.Equal(InventoryReservationState.Completed, world.Society.Inventory.GetReservation(UsedReservation).State);
        Assert.Equal(consumed, world.Society.Inventory.GetReservation(UsedReservation));
    }

    [Theory]
    [InlineData("foreign-adult")]
    [InlineData("remaining-contents")]
    [InlineData("full-hands")]
    [InlineData("full-house")]
    [InlineData("promised-house-space")]
    [InlineData("broken-vessel")]
    public async Task UnavailableVesselOrPhysicalRoomDoesNotOfferOrMoveAnEmptyReturn(string boundary)
    {
        var state = Prepared(InventoryContainerRules.WaterJug);
        var actor = Actor(state);
        var inventory = state.Society.Society.Inventory;
        if (boundary == "foreign-adult")
            actor = state.Society.Society.Agents.First(person => person.HouseholdId != Alpha).Id;
        if (boundary == "full-hands")
        {
            var person = state.Agents.Single(item => item.AgentId == actor);
            var free = PersonalEquipmentRules.FreeCapacity(inventory, actor, person.Equipment);
            Assert.True(free > 0);
            inventory = InventoryAuthority.AddLot(inventory, "container-return-ballast", "test_cargo", actor, free);
            Assert.Equal(0, PersonalEquipmentRules.FreeCapacity(inventory, actor, person.Equipment));
        }
        if (boundary is "full-house" or "promised-house-space")
        {
            var house = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == House);
            var definition = state.WorldContent!.Buildings.Single(item => item.CanonicalId == house.DefinitionId);
            var free = BuildingStorageRules.Capacity(definition, house)!.Value -
                inventory.Lots.Where(lot => lot.StorageBuildingId == House).Sum(lot => lot.Quantity);
            var incoming = boundary == "promised-house-space" ? 1 : 0;
            Assert.True(free > incoming);
            inventory = InventoryAuthority.AddLot(inventory, "container-return-full-house", "test_storage", Alpha,
                free - incoming, storageBuildingId: House);
            if (incoming != 0)
            {
                var deliverer = state.Society.Society.Agents.First(person => person.HouseholdId == Alpha && person.Id != actor).Id;
                inventory = InventoryAuthority.AddLot(inventory, "container-return-incoming", "wood", Alpha, 1);
                inventory = InventoryAuthority.Transfer(inventory, "container-return-incoming-pickup", Alpha,
                    deliverer, "container-return-incoming", 1, "household_input_picked_up",
                    destinationDeliveryBuildingId: House);
            }
        }
        if (boundary == "remaining-contents")
            inventory = InventoryAuthority.AddLot(inventory, "container-return-live-water", InventoryContainerRules.FreshWater,
                Alpha, 1, storageBuildingId: Clinic, containerLotId: Vessel);
        if (boundary == "broken-vessel")
            inventory = InventoryAuthority.WearSingleUnit(inventory, Vessel, 10_000);
        state = WithInventory(state, inventory);
        var unchanged = state.Society.Society.Inventory.GetLot(Vessel);
        var provider = new VesselChoices("return_empty_vessel:", "haul_household_stock");
        using var world = Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), actor, provider);
        for (var tick = 0; tick < 3; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.NotEmpty(provider.Offered);
        Assert.DoesNotContain(provider.Offered, candidate => candidate.StartsWith("return_empty_vessel:", StringComparison.Ordinal));
        var actual = world.Society.Inventory.GetLot(Vessel);
        Assert.Equal(unchanged with { LastProcessedTick = actual.LastProcessedTick }, actual);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "empty_vessel_picked_up");
        if (boundary == "remaining-contents")
            Assert.Equal(1, world.Society.Inventory.GetLot("container-return-live-water").Quantity);
        Assert.NotEmpty(WorldRuntimeCodec.Encode(world.ExportState()));
    }

    private static WorldRuntimeState Prepared(string kind)
    {
        using var generated = NormalPathWorld.CreateGenerated("empty-container-return", _ => new VesselChoices());
        var state = generated.ExportState();
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "container-return-clinic-stone", "stone",
            Alpha, 4, storageBuildingId: House);
        using var funded = WorldRuntime.Restore(WithInventory(state, inventory), _ => new VesselChoices());
        var house = funded.WorldSimulation.Buildings.Single(building => building.InstanceId == House);
        Assert.Contains(Enumerable.Range(-8, 17).SelectMany(y => Enumerable.Range(-8, 17).Select(x =>
            new GridPoint(house.Position.X + x, house.Position.Y + y)))
            .OrderBy(point => state.Map.FootDistance(house.Position, point)).ThenBy(point => point.Y).ThenBy(point => point.X), point =>
            point != house.Position && funded.PlaceBuilding(Clinic, CareContent.Clinic1x2().CanonicalId, point, Alpha).Applied);
        state = funded.ExportState();
        inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, Vessel, kind, Alpha, 1, storageBuildingId: Clinic);
        inventory = InventoryAuthority.AddLot(inventory, UsedContents,
            kind == InventoryContainerRules.WaterJug ? InventoryContainerRules.FreshWater : "grain", Alpha, 1,
            storageBuildingId: Clinic, containerLotId: Vessel);
        inventory = InventoryAuthority.Reserve(inventory, UsedReservation, Alpha, UsedContents, 1,
            "consumed_workstation_input", state.Society.Society.WorldTick);
        inventory = InventoryAuthority.ConsumeReservation(inventory, UsedReservation);
        var actor = Actor(state);
        var previous = state.Agents.Single(person => person.AgentId == actor).Position;
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { Position = house.Position, FullnessBasisPoints = 9_000 }
                : person.Position == house.Position ? person with { Position = previous } : person).ToArray(),
        };
        return WithInventory(state, inventory);
    }

    private static string Actor(WorldRuntimeState state) => state.Society.Society.Agents.First(person =>
        person.HouseholdId == Alpha && person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder).Id;

    private static WorldRuntimeState WithInventory(WorldRuntimeState state, InventoryCheckpoint inventory) =>
        state with { Society = state.Society with { Society = state.Society.Society with { Inventory = StockHoldTestState.Custody(inventory) } } };

    private static WorldRuntime Restore(WorldRuntimeState state, string actor, IDecisionProvider provider) =>
        WorldRuntime.Restore(state, id => id == actor ? provider : new VesselChoices());

    private static async Task AdvanceUntil(WorldRuntime world, Func<bool> complete, int budget)
    {
        for (var tick = 0; tick < budget && !complete(); tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True(complete(), $"The real vessel phase did not finish within {budget} ticks at {world.WorldTick}.");
    }

    private sealed class VesselChoices(params string[] prefixes) : IDecisionProvider
    {
        public HashSet<string> Offered { get; } = new(StringComparer.Ordinal);
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            foreach (var candidate in request.Observation.Candidates) Offered.Add(candidate.Id);
            var choice = prefixes.Select(prefix => request.Observation.Candidates.FirstOrDefault(candidate =>
                candidate.Id.StartsWith(prefix, StringComparison.Ordinal))).FirstOrDefault(candidate => candidate is not null)
                ?? request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with
            { Observation = request.Observation with { Candidates = [choice] } }, cancellationToken);
        }
    }
}
