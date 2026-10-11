using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;

namespace ClankerWorld.Simulation.Tests;

public sealed partial class AnimalPipelineTests
{
    [Theory]
    [InlineData(2, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task YardWaterPickupKeepsTheWholeJugsLoadedReturn(int stones, bool swim)
    {
        var (state, actor, home, yard) = CreateYard("cart-set-unfinished");
        using (var setup = PrivateWorldRuntime.Restore(state, _ => new AnimalChooser()))
        {
            Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
            state = setup.ExportState();
        }
        var source = swim ? new GridPoint(105, 49) : state.Map.FootNeighbors(yard.Position)
            .First(point => state.Map.IsBuildable(point) && state.Inhabitants.All(person => person.Position != point));
        Assert.Equal(!swim, state.Map.IsReachableOnFoot(source, yard.Position));
        Assert.True(SwimmingRules.IsReachable(state.Map, source, yard.Position));
        var inventory = state.Society.Society.Inventory with { Lots = [], Reservations = [] };
        inventory = InventoryFixture.AddLot(inventory, "yard-return-stones", "stone", actor, stones);
        inventory = InventoryFixture.AddLot(inventory, "yard-return-jug", "water_jug", home, 1, groundPosition: new(source.X, source.Y));
        inventory = InventoryFixture.AddLot(inventory, "yard-return-water", "fresh_water", home, 2, containerLotId: "yard-return-jug");
        var day = state.WorldSystems!.Config.TicksPerDay;
        var cow = new AnimalState("return-cow", "Moss", "cow", "female", -7L * day,
            yard.Position, "household:" + home, home, yard.InstanceId, CareUntilTick: state.Society.Society.WorldTick + day);
        state = At(state, actor, source, inventory, [cow]) with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Inhabitants = At(state, actor, source, inventory, [cow]).Inhabitants.Select(item => item with
            { Survival = new(), Equipment = null }).ToArray(),
        };
        var chooser = new ReturnPickupChooser("animal:supply");
        var bytes = PrivateWorldRuntimeCodec.Encode(state);
        using var world = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes), id => id == actor ? chooser : new AnimalChooser());
        var blocked = swim && stones == 2;
        if (blocked)
        {
            for (var tick = 0; tick < 4; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.DoesNotContain(chooser.Offered.SelectMany(items => items), id => id.StartsWith("animal:supply:", StringComparison.Ordinal));
            Assert.Null(world.Society.Inventory.GetLot("yard-return-jug").CarrierId);
            Assert.Empty(world.ExportState().AnimalWorld!.SupplyTrips);
        }
        else
        {
            await Until(world, () => world.Society.Inventory.GetLot("yard-return-jug").StorageBuildingId == yard.InstanceId);
            Assert.Equal(yard.InstanceId, world.Society.Inventory.GetLot("yard-return-water").StorageBuildingId);
            Assert.Single(world.ExportState().Events, item => item.Kind == "animal_yard_supplied");
            Assert.Empty(world.ExportState().AnimalWorld!.SupplyTrips);
        }
        Assert.Equal((home, 2, "yard-return-jug"), (world.Society.Inventory.GetLot("yard-return-water").OwnerId,
            world.Society.Inventory.GetLot("yard-return-water").Quantity, world.Society.Inventory.GetLot("yard-return-water").ContainerLotId));
        Assert.Equal(stones, world.Society.Inventory.GetLot("yard-return-stones").Quantity);
        bytes = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        using var replay = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes), _ => new AnimalChooser());
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(replay.ExportState()));
        world.Validate(); replay.Validate();
    }
}
