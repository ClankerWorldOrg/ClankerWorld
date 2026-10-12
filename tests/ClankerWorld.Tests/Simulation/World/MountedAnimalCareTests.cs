using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ARiderCanPrepareAndCompleteCareForAnotherAnimal(bool dismountFirst, bool fullMountedLoad)
    {
        var (state, actor, home, yard) = CreateYard("animal-horse-cargo");
        var tiles = WorldContentSimulationRules.Footprint(state.WorldContent!.Buildings.Single(definition =>
            definition.CanonicalId == yard.DefinitionId), yard).ToArray();
        var cowPosition = tiles.First(point => point != yard.Position && !state.Agents.Any(person => person.AgentId != actor && person.Position == point));
        var day = state.WorldSystems!.Config.TicksPerDay;
        var horse = new AnimalState("mounted-care-horse", "Moss", "horse", "female", -(long)AnimalRules.Definition("horse").AdultDays * day,
            yard.Position, "household:" + home, home, yard.InstanceId, CareUntilTick: state.Society.Society.WorldTick + day);
        var cow = new AnimalState("mounted-care-cow", "Fern", "cow", "female", -(long)AnimalRules.Definition("cow").AdultDays * day,
            cowPosition, "household:" + home, home, yard.InstanceId);
        var inventory = state.Society.Society.Inventory;
        inventory = inventory with { Lots = inventory.Lots.Where(lot => lot.OwnerId != actor).ToArray() };
        if (fullMountedLoad)
            inventory = inventory with
            {
                Lots = inventory.Lots.Where(lot => lot.OwnerId != home ||
                !AnimalRules.IsFeed(lot.ItemKind) && lot.ItemKind != "fresh_water").ToArray()
            };
        inventory = InventoryAuthority.AddLot(inventory, "mounted-care-saddle", "saddle", home, 1, groundPosition: new(yard.Position.X, yard.Position.Y));
        inventory = InventoryAuthority.AddLot(inventory, "mounted-care-feed", "grain", actor, 2);
        inventory = InventoryAuthority.AddLot(inventory, "mounted-care-jug", "water_jug", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "mounted-care-water", "fresh_water", actor, 2, containerLotId: "mounted-care-jug");
        state = At(state, actor, yard.Position, inventory, [horse, cow]) with
        { Agents = At(state, actor, yard.Position, inventory, [horse, cow]).Agents.Select(person => person.AgentId == actor ? person with { Equipment = null } : person).ToArray() };
        using var preparing = RestoreMountedCare(state, actor);
        var saddle = preparing.SubmitInstruction(new("mounted-care-saddle", "owner", actor, OwnerInstructionKind.Order, "saddle Moss"));
        await Until(preparing, () => preparing.Animals.Single(animal => animal.Id == horse.Id).SaddleLotId is not null);
        Assert.Equal("finished", preparing.ExportState().Instructions!.Single(item => item.InstructionId == saddle.InstructionId).Task!.Status);
        Assert.Equal("mounted-care-saddle", preparing.Animals.Single(animal => animal.Id == horse.Id).SaddleLotId);
        Assert.Equal(0, Math.Max(0, InventoryRules.AvailableQuantity(InventoryIndex.For(preparing.Society.Inventory), preparing.Society.Inventory.GetLot("mounted-care-saddle"))));
        var mount = preparing.SubmitInstruction(new("mounted-care-mount", "owner", actor, OwnerInstructionKind.Order, "mount Moss"));
        await Until(preparing, () => preparing.Animals.Single(animal => animal.Id == horse.Id).RiderId == actor);
        Assert.Equal("finished", preparing.ExportState().Instructions!.Single(item => item.InstructionId == mount.InstructionId).Task!.Status);
        Assert.Empty(preparing.ExportState().AnimalWorld.SupplyTrips);
        if (dismountFirst)
        {
            preparing.SubmitInstruction(new("mounted-care-dismount", "owner", actor, OwnerInstructionKind.Order, "dismount Moss"));
            await Until(preparing, () => preparing.Animals.Single(animal => animal.Id == horse.Id).RiderId is null);
        }
        var care = preparing.SubmitInstruction(new("mounted-care-cow", "owner", actor, OwnerInstructionKind.Order, "care for Fern"));
        var checkpoint = preparing.ExportState();
        if (fullMountedLoad)
            checkpoint = checkpoint with
            {
                Society = checkpoint.Society with
                {
                    Society = checkpoint.Society.Society with
                    {
                        Inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "mounted-care-stone", "stone", actor, 11)
                    }
                }
            };
        var bytes = WorldRuntimeCodec.Encode(checkpoint);
        using var world = RestoreMountedCare(WorldRuntimeCodec.Decode(bytes), actor);
        using var replay = RestoreMountedCare(WorldRuntimeCodec.Decode(bytes), actor);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < (fullMountedLoad ? 4 : 60) && world.ExportState().Instructions!.Single(item => item.InstructionId == care.InstructionId).Task!.Status != "finished"; tick++)
        {
            var wasRiding = world.Animals.Single(animal => animal.Id == horse.Id).RiderId == actor;
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            if (wasRiding && world.Animals.Single(animal => animal.Id == horse.Id).RiderId is null)
            {
                Assert.Equal(0, world.ExportState().Instructions!.Single(item => item.InstructionId == care.InstructionId).Task!.CompletedUnits);
                Assert.Equal(0, world.Animals.Single(animal => animal.Id == cow.Id).CareUntilTick);
                Assert.Equal(2, world.Society.Inventory.GetLot("mounted-care-feed").Quantity);
                Assert.Equal(2, world.Society.Inventory.GetLot("mounted-care-water").Quantity);
                Assert.Equal(horse.Position, world.Agents.Single(person => person.AgentId == actor).Position);
            }
        }
        var order = world.ExportState().Instructions!.Single(item => item.InstructionId == care.InstructionId).Task!;
        if (fullMountedLoad)
        {
            Assert.NotEqual("finished", order.Status);
            Assert.Equal(0, order.CompletedUnits);
            Assert.Equal(0, world.Animals.Single(animal => animal.Id == cow.Id).CareUntilTick);
            Assert.Null(world.Animals.Single(animal => animal.Id == horse.Id).RiderId);
            var ground = new InventoryGroundPosition(horse.Position.X, horse.Position.Y);
            Assert.Equal((actor, 2, ground), (world.Society.Inventory.GetLot("mounted-care-feed").OwnerId,
                world.Society.Inventory.GetLot("mounted-care-feed").Quantity, world.Society.Inventory.GetLot("mounted-care-feed").GroundPosition));
            Assert.Equal((actor, ground), (world.Society.Inventory.GetLot("mounted-care-jug").OwnerId,
                world.Society.Inventory.GetLot("mounted-care-jug").GroundPosition));
            Assert.Equal(2, world.Society.Inventory.GetLot("mounted-care-water").Quantity);
            Assert.Equal(11, world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "stone" && lot.OwnerId == actor).Sum(lot => lot.Quantity));
            Assert.Equal(8, PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, actor, null));
            using var loaded = RestoreMountedCare(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), actor);
            loaded.Validate();
            world.Validate();
            return;
        }
        Assert.Equal(("finished", 1), (order.Status, order.CompletedUnits));
        var cared = world.Animals.Single(animal => animal.Id == cow.Id);
        var parked = world.Animals.Single(animal => animal.Id == horse.Id);
        Assert.True(cared.CareUntilTick > world.WorldTick);
        Assert.True(parked.CareUntilTick > world.WorldTick);
        Assert.Null(parked.RiderId);
        Assert.Equal(horse.Position, parked.Position);
        Assert.Equal(cowPosition, cared.Position);
        Assert.Equal(cowPosition, world.Agents.Single(person => person.AgentId == actor).Position);
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id is "mounted-care-feed" or "mounted-care-water");
        Assert.Equal((actor, 1), (world.Society.Inventory.GetLot("mounted-care-jug").OwnerId, world.Society.Inventory.GetLot("mounted-care-jug").Quantity));
        Assert.Equal((home, 1, new InventoryGroundPosition(parked.Position.X, parked.Position.Y)),
            (world.Society.Inventory.GetLot("mounted-care-saddle").OwnerId, world.Society.Inventory.GetLot("mounted-care-saddle").Quantity,
                world.Society.Inventory.GetLot("mounted-care-saddle").GroundPosition));
        Assert.Empty(world.ExportState().AnimalWorld.SupplyTrips);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "agent_moved" && item.Detail.StartsWith(actor + ":", StringComparison.Ordinal));
        bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var reload = RestoreMountedCare(WorldRuntimeCodec.Decode(bytes), actor);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reload.ExportState()));
        world.Validate();
        reload.Validate();
    }

    private static WorldRuntime RestoreMountedCare(WorldRuntimeState state, string actor) =>
        WorldRuntime.Restore(state, id => id == actor ? new AnimalChooser("animal_order") : new AnimalChooser());
}
