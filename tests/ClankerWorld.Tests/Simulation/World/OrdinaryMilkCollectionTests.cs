using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{
    [Fact]
    public async Task NativeCowMilkCanBeCollectedWithoutAJugAndReloadedAsHouseholdGoods()
    {
        var (state, actor, home, yard) = CreateYard("ordinary-milk-collection");
        var day = state.WorldSystems!.Config.TicksPerDay;
        var tick = state.Society.Society.WorldTick;
        var cow = new AnimalState("ordinary-milk-cow", "Moss", "cow", "female",
            -(long)AnimalRules.Definition("cow").AdultDays * day, yard.Position,
            "household:" + home, home, yard.InstanceId,
            CareUntilTick: tick + day,
            ProductProgressTicks: AnimalRules.Definition("cow").ProductDays * day - 1);
        var inventory = state.Society.Society.Inventory;
        inventory = inventory with { Lots = inventory.Lots.Where(lot => lot.OwnerId != actor).ToArray() };
        state = At(state, actor, yard.Position, inventory, [cow]) with
        {
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == actor
                ? person with { Position = yard.Position, HungerBasisPoints = 9500, Equipment = null }
                : person).ToArray(),
        };
        using var producing = WorldRuntime.Restore(state, _ => new AnimalChooser());
        Assert.True((await producing.AdvanceOneTickAsync()).Advanced);
        var productId = Assert.Single(producing.Animals).ReadyProductLotId;
        Assert.NotNull(productId);
        Assert.DoesNotContain(producing.Society.Inventory.Lots, lot =>
            lot.ItemKind == InventoryContainerRules.WaterJug &&
            PersonalEquipmentRules.IsPhysicallyCarried(producing.Society.Inventory, lot, actor));

        using var world = RestoreAnimalOrder(producing.ExportState(), actor);
        world.SubmitInstruction(new("ordinary-milk-collect", "owner", actor,
            OwnerInstructionKind.MustDo, "collect from Moss"));
        for (var step = 0; step < 8 && world.Animals.Single().ReadyProductLotId is not null; step++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        Assert.Null(Assert.Single(world.Animals).ReadyProductLotId);
        var milk = world.Society.Inventory.GetLot(productId);
        Assert.Equal(("milk", home, 2, actor), (milk.ItemKind, milk.OwnerId, milk.Quantity, milk.CarrierId));
        Assert.Null(milk.ContainerLotId);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "animal_product_collected");
        Assert.DoesNotContain(world.ExportState().AnimalWorld.SupplyTrips, trip => trip.ActorId == actor);
        world.Validate();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reload.ExportState()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryMilkSpoilsAtItsExistingRateAndKeepsWaterSeparateAcrossReplay(bool foodPot)
    {
        using var generated = NormalPathWorld.CreateGenerated("ordinary-milk-spoilage", _ => new AnimalChooser());
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var state = generated.ExportState();
        var actor = state.Inhabitants[0].InhabitantId;
        var inventory = state.Society.Society.Inventory;
        if (foodPot) inventory = InventoryAuthority.AddLot(inventory, "spoilage-pot", "storage_pot", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "spoilage-milk", "milk", actor, 2,
            freshnessBasisPoints: 56, containerLotId: foodPot ? "spoilage-pot" : null);
        inventory = InventoryAuthority.AddLot(inventory, "spoilage-water-jug", "water_jug", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "spoilage-water", "fresh_water", actor, 1,
            containerLotId: "spoilage-water-jug");
        state = state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
        var initialTick = state.Society.Society.WorldTick;
        using var world = WorldRuntime.Restore(state, _ => new AnimalChooser());
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(state with
        { SchemaVersion = WorldRuntime.StateSchemaVersion - 1 }));
        for (var step = 1; step <= 4; step++)
        {
            var bytes = WorldRuntimeCodec.Encode(world.ExportState());
            using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new AnimalChooser());
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(replay.ExportState()));
            Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            var cadence = foodPot ? 2 : 1;
            var loss = ((initialTick + step) / cadence - initialTick / cadence) * 28;
            Assert.Equal(Math.Max(0, 56 - loss), world.Society.Inventory.GetLot("spoilage-milk").FreshnessBasisPoints);
            Assert.Equal((actor, 2), (world.Society.Inventory.GetLot("spoilage-milk").OwnerId,
                world.Society.Inventory.GetLot("spoilage-milk").Quantity));
            Assert.Equal((actor, 1, "spoilage-water-jug", 10_000),
                (world.Society.Inventory.GetLot("spoilage-water").OwnerId,
                 world.Society.Inventory.GetLot("spoilage-water").Quantity,
                 world.Society.Inventory.GetLot("spoilage-water").ContainerLotId,
                 world.Society.Inventory.GetLot("spoilage-water").FreshnessBasisPoints));
            Assert.Equal((actor, 1, 10_000), (world.Society.Inventory.GetLot("spoilage-water-jug").OwnerId,
                world.Society.Inventory.GetLot("spoilage-water-jug").Quantity,
                world.Society.Inventory.GetLot("spoilage-water-jug").ConditionBasisPoints));
            world.Validate();
        }
        Assert.Single(world.Society.Inventory.Events, item => item.Kind == "lot_spoiled" && item.Detail == "spoilage-milk");
    }

    [Fact]
    public async Task PaidNativeMarketSellsAndRecoversOrdinaryMilkWithoutVessels()
    {
        await MarketAnimalMilkScenario.AssertPipelineAsync(await PaidMarketWorld.StateAsync());
    }

}
