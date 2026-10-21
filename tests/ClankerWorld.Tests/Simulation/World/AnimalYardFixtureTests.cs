using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class AnimalYardFixtureTests
{
    [Theory]
    [InlineData("cart-set-unfinished")]
    [InlineData("animal-horse-cargo")]
    public void RepeatedNativeYardPreparationKeepsAllocationBounded(string seed)
    {
        _ = AnimalPipelineTests.CreateYard(seed);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var prepared = AnimalPipelineTests.CreateYard(seed);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(seed, prepared.State.WorldSeed);
        const long budget = 64L * 1024 * 1024;
        Assert.True(allocated <= budget,
            $"Repeated yard preparation allocated {allocated:N0} bytes; budget {budget:N0}.");
    }

    [Theory]
    [InlineData("cart-set-unfinished")]
    [InlineData("animal-horse-cargo")]
    public void RepeatedNativeYardsHaveIndependentMutableStateAndIdenticalCheckpoints(string seed)
    {
        var first = AnimalPipelineTests.CreateYard(seed);
        var expected = WorldRuntimeCodec.Encode(first.State);
        var second = AnimalPipelineTests.CreateYard(seed);
        Assert.Equal(expected, WorldRuntimeCodec.Encode(second.State));
        Assert.Equal((first.Actor, first.Home, first.Yard), (second.Actor, second.Home, second.Yard));
        Assert.NotSame(first.State, second.State);
        Assert.NotSame(first.State.Map.ElevationLevels, second.State.Map.ElevationLevels);
        Assert.NotSame(first.State.Society.Society.Agents, second.State.Society.Society.Agents);
        Assert.NotSame(first.State.Society.Society.Inventory.Lots, second.State.Society.Society.Inventory.Lots);
        first.State.Map.ElevationLevels![0] ^= 1;
        var third = AnimalPipelineTests.CreateYard(seed);
        Assert.Equal(expected, WorldRuntimeCodec.Encode(third.State));
    }

    [Fact]
    public void TheSameSeedKeepsSeparateNativeCalendarChoices()
    {
        var shortDay = AnimalPipelineTests.CreateYard("animal-old-age", ticksPerDay: 2);
        var longerDay = AnimalPipelineTests.CreateYard("animal-old-age", ticksPerDay: 4);
        Assert.Equal(2, shortDay.State.WorldSystems!.Config.TicksPerDay);
        Assert.Equal(4, longerDay.State.WorldSystems!.Config.TicksPerDay);
        Assert.Equal(2, shortDay.State.Society.Society.Config.TicksPerWorldDay);
        Assert.Equal(4, longerDay.State.Society.Society.Config.TicksPerWorldDay);
    }
}
