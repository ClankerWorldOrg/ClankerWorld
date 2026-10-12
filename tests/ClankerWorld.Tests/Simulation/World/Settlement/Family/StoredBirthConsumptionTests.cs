using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class SettlementParenthoodTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BirthConsumesReservedFamilyFoodInPlaceDespiteFullCargoAndKeepsBrokenVesselControl(bool broken)
    {
        var prepared = await CookedBirthFixture.PrepareAsync(inPot: true);
        using var planning = CookedBirthFixture.Restore(prepared);
        for (var tick = 0; tick < 40 && planning.Agents.Single(person => person.AgentId == prepared.First)
                 .Parenthood?.Stage != "preparing"; tick++)
            Assert.True((await planning.AdvanceOneTickAsync()).Advanced);
        var plan = planning.Agents.Single(person => person.AgentId == prepared.First).Parenthood!;
        Assert.Equal("preparing", plan.Stage);
        // Keep the actual native plan and move only the fixture clock past idle waiting.
        PositionFamilyFixtureAt(planning, plan.LastTransitionTick + 599);
        planning.Pause();
        var state = planning.ExportState();
        var caregiver = plan.PrimaryCaregiverId!;
        var house = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == prepared.House);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "stored-birth-extra-pot", "storage_pot",
            prepared.Household, 1, storageBuildingId: house.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, "stored-birth-extra-food", "food", prepared.Household, 1,
            storageBuildingId: house.InstanceId, containerLotId: "stored-birth-extra-pot");
        inventory = InventoryAuthority.Reserve(inventory, "stored-birth-other-meal", prepared.Household,
            prepared.OutputIds[0], 1, "another_meal", long.MaxValue);
        var person = state.Agents.Single(person => person.AgentId == caregiver);
        var room = PersonalEquipmentRules.FreeCapacity(inventory, caregiver, person.Equipment);
        if (room > 0) inventory = InventoryAuthority.AddLot(inventory, "stored-birth-ballast", "stone", caregiver, room);
        if (broken) inventory = inventory with
        {
            Lots = inventory.Lots.Select(lot => lot.Id is CookedBirthFixture.PotId or "stored-birth-extra-pot"
                ? lot with { ConditionBasisPoints = 0 } : lot).ToArray(),
        };
        var away = state.Map.Tiles.First(tile => state.Map.IsPassable(tile.Position) &&
            state.Map.FootDistance(tile.Position, house.Position) > 3 &&
            state.Agents.All(agent => agent.Position != tile.Position)).Position;
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(agent => agent with
            {
                Position = agent.AgentId == caregiver ? away : agent.Position,
                HungerBasisPoints = 10_000,
                LastDecisionContext = null
            }).ToArray(),
        };
        using var world = WorldRuntime.Restore(state, _ => new ParentProvider("safe_idle"));
        Assert.Equal(0, PersonalEquipmentRules.FreeCapacity(inventory, caregiver, person.Equipment));
        var readiness = WorldRuntime.ParenthoodFoodReadiness(world.Society, caregiver, state.Towns!);
        Assert.Equal(broken ? 0 : 8, readiness.AvailablePortions);
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(before), _ => new ParentProvider("safe_idle"));
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        world.Resume();
        replay.Resume();
        for (var tick = 0; tick < 3; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.Equal(broken ? 0 : 1, world.Society.Births.Count);
        var retainedMeals = world.Society.Inventory.Lots.Where(lot => prepared.OutputIds.Contains(lot.Id, StringComparer.Ordinal)).ToArray();
        Assert.Equal(broken ? 8 : 4, retainedMeals.Sum(lot => lot.Quantity));
        Assert.Equal(broken ? 2 : 1, world.Society.Inventory.GetLot(prepared.OutputIds[0]).Quantity);
        Assert.All(retainedMeals, retained =>
        {
            Assert.Equal((prepared.Household, house.InstanceId, CookedBirthFixture.PotId),
                (retained.OwnerId, retained.StorageBuildingId, retained.ContainerLotId));
        });
        Assert.Equal(1, world.Society.Inventory.GetLot("stored-birth-extra-food").Quantity);
        Assert.Equal(1, world.Society.Inventory.GetReservation("stored-birth-other-meal").Quantity);
        world.Validate();
        var final = WorldRuntimeCodec.Encode(world.ExportState());
        using var loaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(final));
        Assert.Equal(final, WorldRuntimeCodec.Encode(loaded.ExportState()));
    }
}
