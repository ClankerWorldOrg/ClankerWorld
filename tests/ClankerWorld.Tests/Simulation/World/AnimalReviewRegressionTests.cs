using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{


    [Theory]
    [InlineData("sheep")]
    [InlineData("cow")]
    public async Task LeadingMovesTheExactNewlyReadyProductAndItsReservationAcrossReload(string species)
    {
        var (state, actor, home, yard) = CreateYard("animal-led-product-" + species);
        var day = state.WorldSystems!.Config.TicksPerDay;
        var occupied = state.Agents.Where(person => person.AgentId != actor).Select(person => person.Position).ToHashSet();
        var position = MovementTestWorld.OpenGround(state, actor, yard.Position, 3, allowed: point =>
        {
            if (state.Map.FootDistance(point, yard.Position) != 3) return false;
            using var routes = MovementTestWorld.Routes(state, point, occupied);
            return routes.RouteTo(yard.Position, 0).Count > 1;
        });
        var definition = AnimalRules.Definition(species);
        var animal = new AnimalState("led", "Moss", species, "female", -(long)definition.AdultDays * day,
            position, "household:" + home, home, yard.InstanceId, CareUntilTick: day,
            ProductProgressTicks: definition.ProductDays * day - 1, LeaderId: actor, LeadDestination: yard.Position);
        using var world = WorldRuntime.Restore(At(state, actor, position, state.Society.Society.Inventory, [animal]),
            id => id == actor ? new AnimalChooser("animal:lead_home:") : new AnimalChooser());
        await Until(world, () => world.Animals.Single().Position != position && world.Animals.Single().ReadyProductLotId is not null);
        var moved = world.Animals.Single();
        var lot = world.Society.Inventory.GetLot(moved.ReadyProductLotId!);
        Assert.Equal((definition.Product, home, definition.ProductQuantity), (lot.ItemKind, lot.OwnerId, lot.Quantity));
        Assert.Equal(new InventoryGroundPosition(moved.Position.X, moved.Position.Y), lot.GroundPosition);
        var reservation = world.Society.Inventory.GetReservation(moved.ReadyProductReservationId!);
        Assert.Equal((lot.Id, home, lot.Quantity, InventoryReservationState.Reserved),
            (reservation.LotId, reservation.OwnerId, reservation.Quantity, reservation.State));
        world.Validate();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            id => id == actor ? new AnimalChooser("animal:lead_home:") : new AnimalChooser());
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutsiderCareProtectsTheAggregateReserveOfTheHouseholdSupplyingItsGreens(bool enough)
    {
        var (state, owner, home, yard) = CreateYard("animal-outsider-feed");
        var outsider = state.Society.Society.Agents.First(person => person.Status == SocietyAgentStatus.Active &&
            person.AgeBand == SocietyAgeBand.Adult && person.HouseholdId != home).Id;
        var outsiderHome = state.Society.Society.GetAgent(outsider).HouseholdId!;
        var family = state.Society.Society.Agents.Where(person => person.Status == SocietyAgentStatus.Active &&
            person.HouseholdId == outsiderHome).Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var inventory = state.Society.Society.Inventory with
        { Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != outsiderHome && !family.Contains(lot.OwnerId)).ToArray() };
        inventory = InventoryAuthority.AddLot(inventory, "00-feed", "wild_greens", outsider, 1);
        inventory = InventoryAuthority.AddLot(inventory, "01-feed", "wild_greens", outsider, 1);
        inventory = InventoryAuthority.AddLot(inventory, "family-food", "berries", outsiderHome,
            family.Count * 2 - 1 + (enough ? 1 : 0), groundPosition: new(yard.Position.X, yard.Position.Y));
        inventory = InventoryAuthority.AddLot(inventory, "animal-family-food", "berries", home, 20,
            groundPosition: new(yard.Position.X, yard.Position.Y));
        inventory = InventoryAuthority.AddLot(inventory, "water-jug", "water_jug", outsider, 1);
        inventory = InventoryAuthority.AddLot(inventory, "water", "fresh_water", outsider, 2, containerLotId: "water-jug");
        var day = state.WorldSystems!.Config.TicksPerDay;
        var animal = new AnimalState("cow", "Moss", "cow", "female", -7L * day, yard.Position,
            "household:" + home, home, yard.InstanceId)
        { CarePermissions = [outsider] };
        using var world = WorldRuntime.Restore(At(state, outsider, yard.Position, inventory, [animal]),
            id => id == outsider ? new AnimalChooser("animal:care:") : new AnimalChooser());
        if (enough) await Until(world, () => world.Animals.Single().CareUntilTick > world.WorldTick);
        else for (var tick = 0; tick < 4; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(enough ? 0 : 2, world.Society.Inventory.Lots.Where(lot => lot.Id is "00-feed" or "01-feed").Sum(lot => lot.Quantity));
        Assert.Equal(enough ? 0 : 2, world.Society.Inventory.Lots.Where(lot => lot.Id == "water").Sum(lot => lot.Quantity));
        Assert.Equal(enough, world.ExportState().Events.Any(item => item.Kind == "animal_cared"));
        world.Validate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MilkNeedsNoVesselButABrokenFoodPotCannotSupplyIt(bool broken)
    {
        var (state, actor, _, yard) = CreateYard("animal-broken-milk");
        var inventory = state.Society.Society.Inventory;
        if (broken) inventory = InventoryAuthority.AddLot(inventory, "milk-pot", "storage_pot", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "milk", "milk", actor, 1, containerLotId: broken ? "milk-pot" : null);
        if (broken) inventory = inventory with
        { Lots = inventory.Lots.Select(lot => lot.Id == "milk-pot" ? lot with { ConditionBasisPoints = 0 } : lot).ToArray() };
        state = At(state, actor, yard.Position, inventory, []) with
        { Agents = At(state, actor, yard.Position, inventory, []).Agents.Select(person => person.AgentId == actor ? person with { FullnessBasisPoints = 3000 } : person).ToArray() };
        using var world = WorldRuntime.Restore(state, id => id == actor ? new AnimalChooser("drink_milk") : new AnimalChooser());
        if (!broken) await Until(world, () => world.ExportState().Events.Any(item => item.Kind == "milk_drunk"));
        else for (var tick = 0; tick < 4; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(broken ? 1 : 0, world.Society.Inventory.Lots.Where(lot => lot.Id == "milk").Sum(lot => lot.Quantity));
        if (broken)
        {
            Assert.Equal(actor, world.Society.Inventory.GetLot("milk-pot").OwnerId);
            Assert.Equal(0, world.Society.Inventory.GetLot("milk-pot").ConditionBasisPoints);
        }
        else Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == "milk");
        world.Validate();
    }
}
