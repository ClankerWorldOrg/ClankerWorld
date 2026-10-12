using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class AdvancedGenerationTests
{
    [Theory]
    [InlineData(WorldSizePreset.Medium, GenerationAmount.High, 80)]
    public void AdvancedOptionsSurviveExactSaveRestore(WorldSizePreset size, GenerationAmount amount, int water)
    {
        var options = new GeographyOptions("advanced-save", size, WaterPercent: water, LatitudeCooling: false,
            HydrologyVersion: GeographyGenerator.CurrentHydrologyVersion, ForestCover: amount, MountainRelief: amount, RiverAbundance: amount);
        using var original = new WorldRuntime(options.Seed, startPace: WorldStartPace.FounderSetup, geographyOptions: options);
        var bytes = WorldRuntimeCodec.Encode(original.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(options, restored.ExportState().Geography);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }
}
