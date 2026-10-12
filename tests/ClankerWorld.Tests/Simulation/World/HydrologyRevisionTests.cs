using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class HydrologyRevisionTests
{
    [Fact]
    public void UnknownHydrologyVersionIsRejectedInsteadOfSubstitutingAnotherMap()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GeographyGenerator.Generate(
            new GeographyOptions("unsupported", WorldSizePreset.Small, HydrologyVersion: 2)));
    }
}
