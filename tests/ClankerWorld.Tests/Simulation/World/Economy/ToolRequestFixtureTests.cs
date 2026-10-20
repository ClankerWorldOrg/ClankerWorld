using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class ToolMakingRequestTests
{
    [Fact]
    public void PreparedRequestsKeepIndependentPeopleStockAndTerrain()
    {
        var first = Prepared();
        var expected = WorldRuntimeCodec.Encode(first.State);
        var second = Prepared();
        Assert.Equal((first.Buyer, first.Seller, first.Shop.InstanceId), (second.Buyer, second.Seller, second.Shop.InstanceId));
        Assert.Equal(expected, WorldRuntimeCodec.Encode(second.State));
        Assert.NotSame(first.State.Agents, second.State.Agents);
        Assert.NotSame(first.State.Society.Society.Inventory.Lots, second.State.Society.Society.Inventory.Lots);
        Assert.NotSame(first.State.Map.ElevationLevels, second.State.Map.ElevationLevels);
        Assert.Contains("blacksmith", second.State.WorldContent!.Buildings.Single(item => item.CanonicalId == second.Shop.DefinitionId).Tags);
        var payment = second.State.Society.Society.Inventory.GetLot("request-payment");
        Assert.Equal((second.Buyer, "wood", 3), (payment.OwnerId, payment.ItemKind, payment.Quantity));

        var people = Assert.IsAssignableFrom<IList<AgentState>>(first.State.Agents);
        people[0] = people[0] with { FullnessBasisPoints = 1 };
        var stock = Assert.IsAssignableFrom<IList<InventoryLot>>(first.State.Society.Society.Inventory.Lots);
        stock[0] = stock[0] with { Quantity = stock[0].Quantity + 1 };
        first.State.Map.ElevationLevels![^1] ^= 1;
        Assert.Equal(expected, WorldRuntimeCodec.Encode(second.State));
        Assert.Equal(expected, WorldRuntimeCodec.Encode(Prepared().State));
        using var restored = WorldRuntime.Restore(second.State, _ => new RequestChoices());
        restored.Validate();
        Assert.Equal(expected, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Fact]
    public void RepeatedRequestSetupFitsItsAllocationBudget()
    {
        _ = Prepared();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var fixture = Prepared();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 1, 16L * 1024 * 1024);
        GC.KeepAlive(fixture.State);
    }
}
