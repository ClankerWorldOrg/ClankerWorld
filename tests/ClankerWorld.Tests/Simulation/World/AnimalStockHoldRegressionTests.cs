using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{
    [Theory]
    [InlineData("wild_greens")]
    [InlineData("grain")]
    [InlineData("water_jug")]
    public async Task AnActualSupplyTripKeepsItsLoadOutOfUnrelatedGoodsQueriesAcrossReload(string kind)
    {
        var (state, actor, home, yard) = CreateYard("animal-yard-quantity-audit");
        var day = state.WorldSystems!.Config.TicksPerDay;
        var hen = new AnimalState("promise-hen", "Moss", "chicken", "female",
            -(long)AnimalRules.Definition("chicken").AdultDays * day, yard.Position,
            "household:" + home, home, yard.InstanceId, CareUntilTick: state.Society.Society.WorldTick + day);
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor &&
                (lot.OwnerId != home || !AnimalRules.IsFeed(lot.ItemKind))).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "promise-yard-feed", "grain", home, 2, storageBuildingId: yard.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, "promise-source", kind, actor, kind == "water_jug" ? 1 : 4);
        if (kind == "water_jug")
            inventory = InventoryAuthority.AddLot(inventory, "promise-water", "fresh_water", actor, 4, containerLotId: "promise-source");
        state = At(state, actor, yard.Position, inventory, [hen]) with
        {
            Agents = At(state, actor, yard.Position, inventory, [hen]).Agents.Select(person => person.AgentId == actor
                ? person with { Equipment = null } : person).ToArray(),
        };
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), id =>
            id == actor ? new AnimalChooser("animal:supply") : new AnimalChooser());
        await Until(world, () => world.ExportState().AnimalWorld.SupplyTrips.Count == 1);
        var trip = Assert.Single(world.ExportState().AnimalWorld.SupplyTrips);
        var hold = new InventoryHold(InventoryHoldPurpose.SupplyTrip, actor);
        Assert.Contains(hold, world.Society.Inventory.GetLot(trip.LotId).Holds!);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new AnimalChooser());
        var uses = kind == "water_jug" ? new[] { GoodsUse.VesselFor, GoodsUse.ReturnVessel } :
            kind == "wild_greens" ? [GoodsUse.Eat, GoodsUse.Spend, GoodsUse.StorePersonal, GoodsUse.Holdings] :
            [GoodsUse.Spend, GoodsUse.StorePersonal, GoodsUse.Holdings];
        foreach (var use in uses)
        {
            var request = new GoodsRequest(use, actor, GoodsOwners.One(actor), GoodsKinds.One(kind), Explain: true);
            Assert.DoesNotContain(world.FindGoods(request).Matches, match => match.Root.Id == trip.LotId);
            Assert.Null(world.RecheckGoods(request, trip.LotId));
            Assert.Equal(world.FindGoods(request).Matches, replay.FindGoods(request).Matches);
            Assert.Equal(world.FindGoods(request).Excluded, replay.FindGoods(request).Excluded);
        }
        var consumable = kind == "water_jug" ? "fresh_water" : kind;
        var authorized = Assert.Single(world.FindGoods(new(GoodsUse.ConsumeAt, actor,
            GoodsOwners.One(actor), GoodsKinds.One(consumable))).Matches, match => match.Root.Id == trip.LotId);
        Assert.Contains(hold, authorized.RequiredHolds);
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Reserve(world.Society.Inventory,
            "unrelated-input", actor, authorized.Lot.Id, 1, "unrelated", world.WorldTick + 10));
        var reserved = InventoryAuthority.Reserve(world.Society.Inventory, "trip-input", actor, authorized.Lot.Id, 1,
            "animal_care", world.WorldTick + 10, holdAccess: InventoryHoldRules.SupplyTripFor(actor));
        Assert.Equal(hold, Assert.Single(reserved.GetReservation("trip-input").HoldAccess!));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(replay.ExportState()));
    }

    [Fact]
    public async Task ATripConsumesItsSpareGreensWithoutCountingTheirRemovalTwiceAgainstFamilyMeals()
    {
        var (state, actor, home, yard) = CreateYard("animal-broken-family-food-reserve");
        var members = state.Society.Society.GetHousehold(home).MemberIds;
        var house = state.WorldSimulation!.Buildings.First(building => building.HouseholdId == home &&
            state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId).Tags.Contains("house"));
        var reserve = members.Count * 2;
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor &&
                (!InventoryContainerRules.IsFood(lot.ItemKind) || lot.OwnerId != home && !members.Contains(lot.OwnerId))).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "promise-family-meals", "berries", home, reserve, storageBuildingId: house.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, "promise-care-greens", "cultivated_greens", home, 2,
            groundPosition: new(house.Position.X, house.Position.Y));
        inventory = InventoryAuthority.AddLot(inventory, "promise-care-jug", "water_jug", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "promise-care-water", "fresh_water", actor, 2, containerLotId: "promise-care-jug");
        var day = state.WorldSystems!.Config.TicksPerDay;
        var cow = new AnimalState("promise-cow", "Moss", "cow", "female",
            -(long)AnimalRules.Definition("cow").AdultDays * day, yard.Position, "household:" + home, home, yard.InstanceId);
        state = At(state, actor, house.Position, inventory, [cow]) with
        {
            Agents = At(state, actor, house.Position, inventory, [cow]).Agents.Select(person => person.AgentId == actor
                ? person with { Equipment = null } : person).ToArray(),
        };
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), id =>
            id == actor ? new AnimalChooser("animal:supply") : new AnimalChooser());
        await Until(world, () => world.ExportState().AnimalWorld.SupplyTrips.Count == 1);
        var trip = Assert.Single(world.ExportState().AnimalWorld.SupplyTrips);
        Assert.Equal(("care", cow.Id, 2), (trip.Action, trip.AnimalId, world.Society.Inventory.GetLot(trip.LotId).Quantity));
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), id =>
            id == actor ? new AnimalChooser("animal:supply") : new AnimalChooser());
        for (var tick = 0; tick < 80 && world.Animals.Single().CareUntilTick <= world.WorldTick; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(world.Animals.Single().CareUntilTick > world.WorldTick);
        Assert.Equal(reserve, world.Society.Inventory.GetLot("promise-family-meals").Quantity);
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == trip.LotId || lot.Id == "promise-care-water");
        Assert.Empty(world.ExportState().AnimalWorld.SupplyTrips);
        world.Validate();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ForcedDismountCancelsOnlyTheTripWhoseLoadItPartlyDrops(bool dropTripLoad)
    {
        var (state, actor, home, yard) = CreateYard("animal-horse-cargo");
        var day = state.WorldSystems!.Config.TicksPerDay;
        var horse = new AnimalState("promise-horse", "Moss", "horse", "female",
            -(long)AnimalRules.Definition("horse").AdultDays * day, yard.Position,
            "household:" + home, home, yard.InstanceId, CareUntilTick: state.Society.Society.WorldTick + day);
        var inventory = state.Society.Society.Inventory with
        { Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor).ToArray() };
        inventory = InventoryAuthority.AddLot(inventory, "promise-saddle", "saddle", home, 1,
            groundPosition: new(yard.Position.X, yard.Position.Y));
        state = At(state, actor, yard.Position, inventory, [horse]) with
        {
            Agents = At(state, actor, yard.Position, inventory, [horse]).Agents.Select(person => person.AgentId == actor
                ? person with { Equipment = null } : person).ToArray(),
        };
        using var mounted = RestoreMountedCare(state, actor);
        mounted.SubmitInstruction(new("promise-saddle-order", "owner", actor, OwnerInstructionKind.Order, "saddle Moss"));
        await Until(mounted, () => mounted.Animals.Single().SaddleLotId is not null);
        mounted.SubmitInstruction(new("promise-mount-order", "owner", actor, OwnerInstructionKind.Order, "mount Moss"));
        await Until(mounted, () => mounted.Animals.Single().RiderId == actor);
        state = mounted.ExportState();
        inventory = state.Society.Society.Inventory;
        var feedId = dropTripLoad ? "a-promise-feed" : "z-promise-feed";
        var stoneId = dropTripLoad ? "z-other-stone" : "a-other-stone";
        inventory = InventoryAuthority.AddLot(inventory, feedId, "grain", actor, 4);
        inventory = InventoryAuthority.AddLot(inventory, stoneId, "stone", actor, 6);
        inventory = InventoryAuthority.Reserve(inventory, "unrelated-stone-input", actor, stoneId, 1, "other_work", long.MaxValue);
        state = state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
        using var supplying = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), id =>
            id == actor ? new AnimalChooser("animal:supply:") : new AnimalChooser());
        await Until(supplying, () => supplying.ExportState().AnimalWorld.SupplyTrips.Count == 1);
        var trip = Assert.Single(supplying.ExportState().AnimalWorld.SupplyTrips);
        Assert.Equal((feedId, 4), (trip.LotId, supplying.Society.Inventory.GetLot(trip.LotId).Quantity));
        state = supplying.ExportState();
        state = state with
        {
            AnimalWorld = state.AnimalWorld with
            { Animals = state.AnimalWorld.Animals.Select(animal => animal with { CareUntilTick = supplying.WorldTick + 1 }).ToArray() },
            // Isolate automatic dismount from repeating the saved pickup choice.
            Society = state.Society with
            {
                Cognition = state.Society.Cognition with
                {
                    Runtimes = state.Society.Cognition.Runtimes.Select(runtime => runtime.AgentId == actor
                        ? runtime with { CurrentIntention = null } : runtime).ToArray(),
                },
            },
        };
        var bytes = WorldRuntimeCodec.Encode(state);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new AnimalChooser());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new AnimalChooser());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        var final = world.ExportState();
        var finalBytes = WorldRuntimeCodec.Encode(final);
        Assert.Equal(finalBytes, WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.Null(world.Animals.Single().RiderId);
        Assert.Equal(8, PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, actor, null));
        Assert.Equal(4, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == actor && lot.ItemKind == "grain").Sum(lot => lot.Quantity));
        Assert.Equal(6, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == actor && lot.ItemKind == "stone").Sum(lot => lot.Quantity));
        Assert.Equal(supplying.Society.Inventory.GetReservation("unrelated-stone-input"),
            world.Society.Inventory.GetReservation("unrelated-stone-input"));
        var ground = new InventoryGroundPosition(yard.Position.X, yard.Position.Y);
        Assert.Equal(2, world.Society.Inventory.Lots.Where(lot => lot.GroundPosition == ground &&
            lot.ItemKind == (dropTripLoad ? "grain" : "stone")).Sum(lot => lot.Quantity));
        var hold = new InventoryHold(InventoryHoldPurpose.SupplyTrip, actor);
        if (dropTripLoad)
        {
            Assert.Empty(final.AnimalWorld.SupplyTrips);
            Assert.DoesNotContain(world.Society.Inventory.Lots, lot => (lot.Holds ?? []).Contains(hold));
        }
        else
        {
            Assert.Equal(trip, Assert.Single(final.AnimalWorld.SupplyTrips));
            Assert.Contains(hold, world.Society.Inventory.GetLot(trip.LotId).Holds!);
        }
        using var loaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(finalBytes), _ => new AnimalChooser());
        Assert.Equal(finalBytes, WorldRuntimeCodec.Encode(loaded.ExportState()));
        world.Validate();
    }
}
