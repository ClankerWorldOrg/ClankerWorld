using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class ShopTradeTests
{
    [Theory]
    [InlineData("blacksmith")]
    [InlineData("tailor")]
    [InlineData("store")]
    [InlineData("farmhouse")]
    public void ShopFixtureCopiesKeepTheirOwnStockMapAndPeople(string kind)
    {
        var first = CreateShopState(kind);
        var expected = WorldRuntimeCodec.Encode(first.State);
        var second = CreateShopState(kind);
        Assert.Equal((first.Buyer, first.Seller, first.ShopId), (second.Buyer, second.Seller, second.ShopId));
        Assert.Equal(expected, WorldRuntimeCodec.Encode(second.State));
        Assert.NotSame(first.State.Agents, second.State.Agents);
        Assert.NotSame(first.State.Society.Society.Inventory.Lots, second.State.Society.Society.Inventory.Lots);
        Assert.NotSame(first.State.Map.ElevationLevels, second.State.Map.ElevationLevels);
        var shop = Assert.Single(second.State.WorldSimulation!.Buildings, item => item.InstanceId == second.ShopId);
        Assert.Contains(kind, second.State.WorldContent!.Buildings.Single(item => item.CanonicalId == shop.DefinitionId).Tags);

        var people = Assert.IsAssignableFrom<IList<AgentState>>(first.State.Agents);
        people[0] = people[0] with { FullnessBasisPoints = 1 };
        var stock = Assert.IsAssignableFrom<IList<InventoryLot>>(first.State.Society.Society.Inventory.Lots);
        stock[0] = stock[0] with { Quantity = stock[0].Quantity + 1 };
        first.State.Map.ElevationLevels![0] ^= 1;
        Assert.Equal(expected, WorldRuntimeCodec.Encode(second.State));
        Assert.Equal(expected, WorldRuntimeCodec.Encode(CreateShopState(kind).State));
        using var restored = WorldRuntime.Restore(second.State, _ => new ShopProvider("safe_idle"));
        restored.Validate();
    }

    [Theory]
    [InlineData("blacksmith")]
    [InlineData("tailor")]
    [InlineData("store")]
    [InlineData("farmhouse")]
    public void RepeatedShopSetupFitsItsAllocationBudget(string kind)
    {
        _ = CreateShopState(kind);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var fixture = CreateShopState(kind);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 1, 16L * 1024 * 1024);
        GC.KeepAlive(fixture.State);
    }
}
