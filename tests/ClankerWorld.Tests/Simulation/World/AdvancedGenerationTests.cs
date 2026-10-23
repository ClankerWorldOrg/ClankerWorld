using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class AdvancedGenerationTests
{
    [Fact]
    public void AdvancedOptionsSurviveExactSaveRestore()
    {
        var options = new GeographyOptions("advanced-save", WorldSizePreset.Medium, WaterPercent: 80, LatitudeCooling: false,
            HydrologyVersion: GeographyGenerator.CurrentHydrologyVersion, ForestCover: GenerationAmount.High, MountainRelief: GenerationAmount.High, RiverAbundance: GenerationAmount.High);
        using var original = new WorldRuntime(options.Seed, startPace: WorldStartPace.FounderSetup, geographyOptions: options);
        var bytes = WorldRuntimeCodec.Encode(original.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(options, restored.ExportState().Geography);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }
}
