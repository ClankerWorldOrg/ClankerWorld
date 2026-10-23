using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{
    [Theory]
    [InlineData("sheep", false, false)]
    [InlineData("sheep", true, false)]
    [InlineData("cow", false, false)]
    [InlineData("cow", true, false)]
    [InlineData("sheep", false, true)]
    public async Task ARiderCanCollectFromAnotherAnimal(string species, bool dismountFirst, bool fullMountedLoad)
    {
        var (state, actor, home, yard) = CreateYard("animal-horse-cargo");
        var tiles = WorldContentSimulationRules.Footprint(state.WorldContent!.Buildings.Single(definition =>
            definition.CanonicalId == yard.DefinitionId), yard).ToArray();
        var sheepPosition = tiles.First(point => point != yard.Position && !state.Agents.Any(person => person.AgentId != actor && person.Position == point));
        var day = state.WorldSystems!.Config.TicksPerDay;
        var horse = new AnimalState("mounted-collect-horse", "Moss", "horse", "female", -(long)AnimalRules.Definition("horse").AdultDays * day,
            yard.Position, "household:" + home, home, yard.InstanceId, CareUntilTick: state.Society.Society.WorldTick + day);
        var sheep = new AnimalState("mounted-collect-product-animal", "Fern", species, "female", -(long)AnimalRules.Definition(species).AdultDays * day,
            sheepPosition, "household:" + home, home, yard.InstanceId, CareUntilTick: state.Society.Society.WorldTick + day,
            ProductProgressTicks: AnimalRules.Definition(species).ProductDays * day - 1);
        var inventory = state.Society.Society.Inventory;
        inventory = inventory with { Lots = inventory.Lots.Where(lot => lot.OwnerId != actor).ToArray() };
        inventory = InventoryAuthority.AddLot(inventory, "mounted-collect-saddle", "saddle", home, 1, groundPosition: new(yard.Position.X, yard.Position.Y));
        if (species == "cow")
            inventory = InventoryAuthority.Relocate(InventoryAuthority.AddLot(inventory, "mounted-collect-jug", "water_jug", home, 1),
                "mounted-collect-carry-jug", "mounted-collect-jug", home, 1, carrierId: actor);
        state = At(state, actor, yard.Position, inventory, [horse, sheep]);
        state = state with { Agents = state.Agents.Select(person => person.AgentId == actor ? person with { Equipment = null } : person).ToArray() };
        using var preparing = RestoreAnimalOrder(state, actor);
        preparing.SubmitInstruction(new("mounted-collect-saddle", "owner", actor, OwnerInstructionKind.Order, "saddle Moss"));
        await Until(preparing, () => preparing.Animals.Single(animal => animal.Id == horse.Id).SaddleLotId is not null);
        preparing.SubmitInstruction(new("mounted-collect-mount", "owner", actor, OwnerInstructionKind.Order, "mount Moss"));
        await Until(preparing, () => preparing.Animals.Single(animal => animal.Id == horse.Id).RiderId == actor);
        Assert.NotNull(preparing.Animals.Single(animal => animal.Id == sheep.Id).ReadyProductLotId);
        var ready = preparing.Animals.Single(animal => animal.Id == sheep.Id);
        var productId = ready.ReadyProductLotId!;
        var productHeld = ready.ReadyProductReservationId!;
        if (dismountFirst)
        {
            preparing.SubmitInstruction(new("mounted-collect-dismount", "owner", actor, OwnerInstructionKind.Order, "dismount Moss"));
            await Until(preparing, () => preparing.Animals.Single(animal => animal.Id == horse.Id).RiderId is null);
        }
        var collect = preparing.SubmitInstruction(new("mounted-collect-wool", "owner", actor, OwnerInstructionKind.Order, "collect from Fern"));
        var checkpoint = preparing.ExportState();
        if (fullMountedLoad)
            checkpoint = checkpoint with
            {
                Society = checkpoint.Society with
                {
                    Society = checkpoint.Society.Society with
                    {
                        Inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "mounted-collect-stone", "stone", actor, 11)
                    }
                }
            };
        var bytes = WorldRuntimeCodec.Encode(checkpoint);
        using var world = RestoreAnimalOrder(WorldRuntimeCodec.Decode(bytes), actor);
        using var replay = RestoreAnimalOrder(WorldRuntimeCodec.Decode(bytes), actor);
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 8 && world.ExportState().Instructions!.Single(item => item.InstructionId == collect.InstructionId).Task!.Status != "finished"; tick++)
        {
            var wasRiding = world.Animals.Single(animal => animal.Id == horse.Id).RiderId == actor;
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            if (wasRiding && world.Animals.Single(animal => animal.Id == horse.Id).RiderId is null)
            {
                Assert.Equal(0, world.ExportState().Instructions!.Single(item => item.InstructionId == collect.InstructionId).Task!.CompletedUnits);
                Assert.Equal(productId, world.Animals.Single(animal => animal.Id == sheep.Id).ReadyProductLotId);
                Assert.Equal(InventoryReservationState.Reserved, world.Society.Inventory.GetReservation(productHeld).State);
                Assert.Equal(horse.Position, world.Agents.Single(person => person.AgentId == actor).Position);
            }
        }
        var order = world.ExportState().Instructions!.Single(item => item.InstructionId == collect.InstructionId).Task!;
        Assert.Null(world.Animals.Single(animal => animal.Id == horse.Id).RiderId);
        Assert.Equal(horse.Position, world.Animals.Single(animal => animal.Id == horse.Id).Position);
        Assert.Equal((home, new InventoryGroundPosition(horse.Position.X, horse.Position.Y)),
            (world.Society.Inventory.GetLot("mounted-collect-saddle").OwnerId, world.Society.Inventory.GetLot("mounted-collect-saddle").GroundPosition));
        Assert.Equal(InventoryReservationState.Reserved,
            world.Society.Inventory.GetReservation(world.Animals.Single(animal => animal.Id == horse.Id).SaddleReservationId!).State);
        if (fullMountedLoad)
        {
            Assert.NotEqual("finished", order.Status);
            Assert.Equal(0, order.CompletedUnits);
            Assert.Equal(productId, world.Animals.Single(animal => animal.Id == sheep.Id).ReadyProductLotId);
            Assert.Equal(InventoryReservationState.Reserved, world.Society.Inventory.GetReservation(productHeld).State);
            var product = world.Society.Inventory.GetLot(productId);
            Assert.Equal((home, 2, new InventoryGroundPosition(sheepPosition.X, sheepPosition.Y)),
                (product.OwnerId, product.Quantity, product.GroundPosition));
            Assert.Equal(11, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == actor && lot.ItemKind == "stone").Sum(lot => lot.Quantity));
            Assert.Equal(8, PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, actor, null));
            Assert.Equal(3, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == actor && lot.ItemKind == "stone" &&
                lot.GroundPosition == new InventoryGroundPosition(horse.Position.X, horse.Position.Y)).Sum(lot => lot.Quantity));
            using var fullReload = RestoreAnimalOrder(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), actor);
            fullReload.Validate();
            world.Validate();
            return;
        }
        Assert.Equal(("finished", 1), (order.Status, order.CompletedUnits));
        var collected = world.Society.Inventory.GetLot(productId);
        Assert.Equal((home, 2), (collected.OwnerId, collected.Quantity));
        Assert.Equal(AnimalRules.Definition(species).Product, collected.ItemKind);
        Assert.True(PersonalEquipmentRules.IsPhysicallyCarried(world.Society.Inventory, collected, actor));
        Assert.NotEqual(InventoryReservationState.Reserved, world.Society.Inventory.GetReservation(productHeld).State);
        Assert.Null(collected.ContainerLotId);
        if (species == "cow")
        {
            Assert.Equal(home, world.Society.Inventory.GetLot("mounted-collect-jug").OwnerId);
            Assert.Equal(actor, world.Society.Inventory.GetLot("mounted-collect-jug").CarrierId);
            Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.ContainerLotId == "mounted-collect-jug");
        }
        Assert.Null(world.Animals.Single(animal => animal.Id == sheep.Id).ReadyProductLotId);
        Assert.Equal(sheepPosition, world.Agents.Single(person => person.AgentId == actor).Position);
        using var loaded = RestoreAnimalOrder(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), actor);
        loaded.Validate();
        world.Validate();
    }
}
