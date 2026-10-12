using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class StarterContentTests
{
    [Fact]
    public async Task StarterPackActivatesOnTheNextTickAndSurvivesRestoreWithoutDuplication()
    {
        using var runtime = new WorldRuntime("test-alpha");
        for (var tick = 0; tick < 5; tick++)
        {
            _ = await runtime.AdvanceOneTickAsync();
        }

        Assert.True(runtime.StageStarterContent());
        Assert.False(runtime.StageStarterContent());
        Assert.Empty(runtime.ExportState().WorldContent!.Buildings);
        _ = await runtime.AdvanceOneTickAsync();
        var state = runtime.ExportState();
        Assert.Equal(4, state.WorldContent!.Buildings.Count);
        Assert.Equal(3, state.WorldContent.Recipes.Count);
        Assert.Equal(ContentPackageLifecycle.Active, Assert.Single(state.Content!.Packages).Lifecycle);

        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)));
        Assert.False(restored.StageStarterContent());
        Assert.Equal(WorldRuntimeCodec.Encode(state), WorldRuntimeCodec.Encode(restored.ExportState()));
    }
}
