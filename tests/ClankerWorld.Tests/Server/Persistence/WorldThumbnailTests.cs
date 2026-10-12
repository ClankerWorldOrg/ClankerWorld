using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed class WorldThumbnailTests
{
    [Theory]
    [InlineData(WorldSizePreset.Medium)]
    public void AThumbnailKeepsTheWorldsShapeAndCommonTerrain(WorldSizePreset size)
    {
        var geography = new GeographyOptions("thumbnail-" + size, size);
        using var world = new WorldRuntime(geography.Seed, startPace: WorldStartPace.FounderSetup, geographyOptions: geography);
        var map = world.ExportState().Map;
        var thumbnail = WorldThumbnail.From(map);

        Assert.Equal((96, 48), (thumbnail.Width, thumbnail.Height));
        Assert.Equal("terrain-kind-v1", thumbnail.Encoding);
        var pixels = Convert.FromBase64String(thumbnail.Data);
        Assert.Equal(96 * 48, pixels.Length);
        var kinds = map.Tiles.Select(tile => (byte)tile.Terrain).ToHashSet();
        Assert.All(pixels, pixel => Assert.Contains(pixel, kinds));
        // Each pixel is its block's commonest terrain, so open water keeps about its share.
        static bool OpenWater(int kind) => kind is (int)TerrainKind.Ocean or (int)TerrainKind.Lake;
        var mapWater = map.Tiles.Count(tile => OpenWater((int)tile.Terrain)) / (double)map.Tiles.Count;
        var thumbnailWater = pixels.Count(pixel => OpenWater(pixel)) / (double)pixels.Length;
        Assert.InRange(thumbnailWater, mapWater - 0.08, mapWater + 0.08);
    }
}
