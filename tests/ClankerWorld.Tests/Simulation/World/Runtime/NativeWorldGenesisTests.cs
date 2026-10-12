using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using System.Text.Json;

namespace ClankerWorld.Tests;

public sealed class NativeWorldGenesisTests
{
    [Theory]
    [InlineData(WorldStartPace.Legacy)]
    [InlineData(WorldStartPace.PlacedFounders)]
    [InlineData(WorldStartPace.FounderSetup)]
    public async Task NewWorldsReplayTheirSavedEnvironmentWithoutRetiredPrototypeRecords(WorldStartPace pace)
    {
        Func<string, IDecisionProvider> providers = _ => new ActionCoverageRecorder(chooseIdle: true);
        using var world = pace == WorldStartPace.FounderSetup
            ? NormalPathWorld.CreateGenerated("no-starting-money", providers)
            : new WorldRuntime("no-starting-money", providers, startPace: pace);
        world.Validate();
        AssertNoRetiredPrototypeRecords(world);
        if (world.Society.IsPaused) world.Resume();

        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), providers);
        restored.Validate();
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        AssertNoRetiredPrototypeRecords(restored);

        for (var tick = 0; tick < 3; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()),
                WorldRuntimeCodec.Encode(restored.ExportState()));
            AssertNoRetiredPrototypeRecords(world);
        }
    }

    private static void AssertNoRetiredPrototypeRecords(WorldRuntime world)
    {
        using var environment = JsonDocument.Parse(WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.False(environment.RootElement.GetProperty("state").GetProperty("worldSystems").TryGetProperty("factions", out _));
        Assert.False(environment.RootElement.GetProperty("state").GetProperty("worldSystems").TryGetProperty("currency", out _));
        Assert.False(environment.RootElement.GetProperty("state").GetProperty("worldSystems").TryGetProperty("culture", out _));
    }
}
