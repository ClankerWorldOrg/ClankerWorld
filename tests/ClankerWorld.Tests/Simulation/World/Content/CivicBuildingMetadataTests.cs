using System.Text.Json;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class CivicBuildingMetadataTests
{
    [Theory]
    [InlineData("hall")]
    [InlineData("market")]
    [InlineData("stall")]
    public void RepeatedFixedBuildingMetadataFitsItsAllocationBudget(string kind)
    {
        _ = Definition(kind);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var definition = Definition(kind);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 0, 256L);
        definition.Validate();
        Assert.NotEmpty(definition.BuildCosts);
        GC.KeepAlive(definition);
    }

    [Theory]
    [InlineData("hall")]
    [InlineData("market")]
    [InlineData("stall")]
    public void FixedBuildingCostsAndTagsCannotChangeAnotherReadersMetadata(string kind)
    {
        var definition = Definition(kind);
        var expected = JsonSerializer.Serialize(definition);
        var costs = Assert.IsAssignableFrom<IList<ContentQuantity>>(definition.BuildCosts);
        Assert.NotEmpty(costs);
        Assert.Throws<NotSupportedException>(() => costs[0] = costs[0] with { Amount = 1 });
        var tags = Assert.IsAssignableFrom<IList<string>>(definition.Tags);
        Assert.NotEmpty(tags);
        Assert.Throws<NotSupportedException>(() => tags[0] = "changed");
        Assert.Equal(expected, JsonSerializer.Serialize(definition));
        Assert.Equal(expected, JsonSerializer.Serialize(Definition(kind)));
    }

    private static BuildingDefinition Definition(string kind) => kind switch
    {
        "hall" => TownHallContent.Hall3x4(),
        "market" => MarketContent.Hall2x2(),
        "stall" => MarketContent.Stall1x1(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
