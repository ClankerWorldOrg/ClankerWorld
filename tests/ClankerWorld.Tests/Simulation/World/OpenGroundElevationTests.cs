using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class OpenGroundElevationTests
{
    [Theory]
    [InlineData(TerrainKind.Meadow)]
    [InlineData(TerrainKind.Water)]
    [InlineData(TerrainKind.Mountain)]
    [InlineData(TerrainKind.River)]
    [InlineData(TerrainKind.Lake)]
    [InlineData(TerrainKind.Ocean)]
    [InlineData(TerrainKind.Peak)]
    [InlineData(TerrainKind.Sand)]
    [InlineData(TerrainKind.Forest)]
    [InlineData(TerrainKind.Snow)]
    public void LiveElevationKeepsGroundMountainAndPeakBandsAcrossLegacyProjections(TerrainKind terrain)
    {
        var point = new GridPoint(0, 0);
        var elevations = new byte[1];
        var map = new WorldMap(1, 1, 0, [new(point, terrain)], [], [], "ground-bands")
        {
            ElevationLevels = elevations,
            HydrologyKinds = [(byte)WaterKind.Land],
            SurfaceKinds = [(byte)SurfaceKind.Grass],
        };
        var ocean = map with { HydrologyKinds = [(byte)WaterKind.Ocean] };
        var fallbackLand = terrain is not (TerrainKind.Water or TerrainKind.River or TerrainKind.Lake or TerrainKind.Ocean);
        var partial = map with { HydrologyKinds = null };
        var rock = map with { SurfaceKinds = [(byte)SurfaceKind.Rock] };
        var waterSurface = map with { SurfaceKinds = [(byte)SurfaceKind.Water] };
        for (var elevation = 0; elevation <= byte.MaxValue; elevation++)
        {
            elevations[0] = (byte)elevation;
            Assert.Equal(elevation < 245, map.IsPassable(point));
            Assert.Equal(elevation < 215, map.IsBuildable(point));
            Assert.False(ocean.IsPassable(point));
            Assert.False(ocean.IsBuildable(point));
            Assert.Equal(fallbackLand && elevation < 245, partial.IsPassable(point));
            Assert.Equal(fallbackLand && elevation < 215, partial.IsBuildable(point));
            Assert.Equal(elevation < 245, rock.IsPassable(point));
            Assert.False(rock.IsBuildable(point));
            Assert.Equal(elevation < 245, waterSurface.IsPassable(point));
            Assert.False(waterSurface.IsBuildable(point));
        }
        var legacy = map with { ElevationLevels = null };
        var malformed = map with { ElevationLevels = [] };
        Assert.Equal(terrain != TerrainKind.Peak, legacy.IsPassable(point));
        Assert.Equal(terrain is not (TerrainKind.Mountain or TerrainKind.Peak), legacy.IsBuildable(point));
        Assert.Equal(legacy.IsPassable(point), malformed.IsPassable(point));
        Assert.Equal(legacy.IsBuildable(point), malformed.IsBuildable(point));
    }
}
