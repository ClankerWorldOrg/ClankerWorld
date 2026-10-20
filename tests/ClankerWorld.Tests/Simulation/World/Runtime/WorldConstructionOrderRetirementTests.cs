using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class WorldConstructionOrderTests
{
    [Fact]
    public async Task ConstructionCompletesWhenItsOldOrderIsRetiredImmediatelyAfterPlacement()
    {
        var setup = await Baseline.Value;
        using var world = Restore(Decode(setup));
        var receipt = Submit(world, setup.Actor, "old-clinic", BuildClinic(setup.Site));
        await ReachWork(world, setup.Actor);
        var buildingId = Order(world, receipt).ConstructionInstanceId;
        AssertInputs(world, 10, 4);
        for (var index = 0; index < OwnerQueuedInstruction.RecentClosedOrderLimitPerAgent; index++)
            Submit(world, setup.Actor, $"newer-closed-order-{index}", "dance", queue: true);
        Assert.Equal("working", Person(world, setup.Actor).Project!.Stage);
        using var replay = Reload(world);
        for (var tick = 0; tick < 40 && !world.WorldSimulation.Buildings.Any(item => item.InstanceId == buildingId); tick++)
            await TickTogether(world, replay);
        var building = Assert.Single(world.WorldSimulation.Buildings, item => item.InstanceId == buildingId);
        Assert.Equal((setup.DefinitionId, setup.Site, Alpha), (building.DefinitionId, building.Position, building.HouseholdId));
        Assert.Equal("completed", Person(world, setup.Actor).Project!.Stage);
        Assert.Null(Person(world, setup.Actor).Project!.OrderInstructionId);
        Assert.DoesNotContain(world.ExportState().Instructions!, item => item.InstructionId == receipt.InstructionId);
        Assert.Empty(world.WorldSimulation.ConstructionReceipts!);
        AssertInputs(world, 0, 0);
        var paid = world.Society.Inventory.Reservations.Where(item => item.LotId is "construction-wood" or "construction-stone").ToArray();
        Assert.Equal(14, paid.Sum(item => item.Quantity));
        Assert.All(paid, item =>
        {
            Assert.Equal(Alpha, item.OwnerId);
            Assert.Equal(InventoryReservationState.Completed, item.State);
        });
        Assert.Single(world.ExportState().Events, item => item.Kind == "build_completed");
        using var completedReplay = Reload(world);
        await TickTogether(world, completedReplay);
        AssertInputs(world, 0, 0);
    }
}
