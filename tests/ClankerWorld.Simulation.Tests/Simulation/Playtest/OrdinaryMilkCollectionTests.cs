using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;

namespace ClankerWorld.Simulation.Tests;

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
        using var producing = PrivateWorldRuntime.Restore(state, _ => new AnimalChooser());
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
        var bytes = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        using var reload = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(reload.ExportState()));
    }
}
