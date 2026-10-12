using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    public async Task MilkCollectionNeedsRoomForTheWholeBatchButNoVessel(int stone, bool expected)
    {
        var (state, actor, home, yard) = CreateYard("animal-milk-sale");
        var day = state.WorldSystems!.Config.TicksPerDay;
        var tick = state.Society.Society.WorldTick;
        var cow = new AnimalState("capacity-cow", "Moss", "cow", "female",
            -(long)AnimalRules.Definition("cow").AdultDays * day, yard.Position, "household:" + home, home, yard.InstanceId,
            CareUntilTick: tick + day, ProductProgressTicks: AnimalRules.Definition("cow").ProductDays * day - 1);
        var inventory = state.Society.Society.Inventory;
        inventory = inventory with { Lots = inventory.Lots.Where(lot => lot.OwnerId != actor).ToArray() };
        state = At(state, actor, yard.Position, inventory, [cow]);
        state = state with
        {
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == actor ? person with { Equipment = null } : person).ToArray(),
        };
        using var producing = WorldRuntime.Restore(state, _ => new AnimalChooser());
        Assert.True((await producing.AdvanceOneTickAsync()).Advanced);
        var productId = producing.Animals.Single().ReadyProductLotId;
        Assert.NotNull(productId);
        Assert.Equal(("milk", home, 2), (producing.Society.Inventory.GetLot(productId).ItemKind,
            producing.Society.Inventory.GetLot(productId).OwnerId, producing.Society.Inventory.GetLot(productId).Quantity));
        state = producing.ExportState();
        inventory = state.Society.Society.Inventory;
        if (stone > 0) inventory = InventoryAuthority.AddLot(inventory, "capacity-stone", "stone", actor, stone);
        state = At(state, actor, yard.Position, inventory, state.AnimalWorld.Animals.ToArray());
        var bytes = WorldRuntimeCodec.Encode(state);
        using var world = RestoreAnimalOrder(WorldRuntimeCodec.Decode(bytes), actor);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        world.SubmitInstruction(new("capacity-collect", "owner", actor, OwnerInstructionKind.MustDo, "collect from Moss"));
        for (var step = 0; step < 40 && world.Animals.Single().ReadyProductLotId is not null; step++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(2, world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "milk").Sum(lot => lot.Quantity));
        Assert.Equal(stone, world.Society.Inventory.Lots.Where(lot => lot.Id == "capacity-stone").Sum(lot => lot.Quantity));
        var milk = world.Society.Inventory.GetLot(productId);
        Assert.Equal((home, "milk", 2), (milk.OwnerId, milk.ItemKind, milk.Quantity));
        Assert.Null(milk.ContainerLotId);
        Assert.DoesNotContain(world.ExportState().AnimalWorld.SupplyTrips, trip => trip.ActorId == actor);
        if (expected)
        {
            Assert.Null(world.Animals.Single().ReadyProductLotId);
            Assert.Equal(actor, milk.CarrierId);
            Assert.Contains(world.ExportState().Events, item => item.Kind == "animal_product_collected");
        }
        else
        {
            Assert.Equal(productId, world.Animals.Single().ReadyProductLotId);
            Assert.Null(milk.CarrierId);
            Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "animal_product_collected");
        }
        world.Validate();
        bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reload.ExportState()));
    }
}
