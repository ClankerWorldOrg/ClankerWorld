using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TownProjectFixtureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApprovedFixturesKeepWorldsAndPoliciesIndependent(bool initialTownStock)
    {
        using var first = await TownProjectScenario.ApprovedAsync(initialTownStock);
        using var second = await TownProjectScenario.ApprovedAsync(initialTownStock);
        var before = WorldRuntimeCodec.Encode(second.World.ExportState());
        Assert.Equal(before, WorldRuntimeCodec.Encode(first.World.ExportState()));
        Assert.NotSame(first.World, second.World);
        Assert.NotSame(first.Policy, second.Policy);
        Assert.True(second.Policy.Proposed);
        Assert.Equal(!initialTownStock, second.Policy.PersonalSupply);
        first.Policy.Supply = true;
        Assert.False(second.Policy.Supply);
        Assert.True((await first.World.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(second.World.ExportState()));
        using var third = await TownProjectScenario.ApprovedAsync(initialTownStock);
        Assert.Equal(before, WorldRuntimeCodec.Encode(third.World.ExportState()));
        Assert.NotSame(first.World, third.World);
        Assert.NotSame(first.Policy, third.Policy);
    }
}
