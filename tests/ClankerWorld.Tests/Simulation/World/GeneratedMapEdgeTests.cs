using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class GeneratedMapEdgeTests
{
    [Theory]
    [InlineData("polar-edge-balanced", WorldSizePreset.Small, true, ClimateMode.Balanced, true, ClimateZone.Temperate, "fe066f074943da8f135eee1164b4ffce7107ef5dc989fd7f8715cda09b5a0120")]
    [InlineData("polar-edge-uniform-dry", WorldSizePreset.Small, false, ClimateMode.Uniform, false, ClimateZone.Dry, "7b02ad7b90a950223e4505f0fc57b95c4f44703c66bed2193c3c09ead3387c74")]
    [InlineData("polar-edge-medium", WorldSizePreset.Medium, true, ClimateMode.Balanced, true, ClimateZone.Temperate, "dd30addd7c4b8c034b8a1b03059a00eb0c5abbd59ccb3f5799218f76910b5428")]
    public void EdgeTerrainRetainsGeneratedGeographyAndCanonicalDigest(string seed, WorldSizePreset size,
        bool wrap, ClimateMode mode, bool latitudeCooling, ClimateZone climate, string expectedDigest)
    {
        var options = new GeographyOptions(seed, size, wrap, ClimateMode: mode,
            SelectedClimate: climate, LatitudeCooling: latitudeCooling);
        var geography = GeographyGenerator.Generate(options);
        var map = GeneratedWorldMapGenerator.Generate(options);
        foreach (var y in new[] { 0, 1, map.Height - 2, map.Height - 1 })
        {
            for (var x = 0; x < map.Width; x++)
            {
                var tile = geography.At(x, y);
                var point = new GridPoint(x, y);
                Assert.Equal(tile.Water, map.HydrologyAt(point));
                Assert.Equal(tile.Climate, map.ClimateAt(point));
                Assert.Equal(tile.Elevation, map.ElevationLevels![y * map.Width + x]);
            }
        }
        foreach (var y in new[] { 0, map.Height - 1 })
            Assert.Contains(map.Tiles, tile => tile.Position.Y == y && map.IsPassable(tile.Position));
        Assert.Contains(map.Resources, resource => resource.Position.Y < 2 || resource.Position.Y >= map.Height - 2);
        Assert.True(MapAcceptance.Validate(map, allowEmptyStartingSite: true).IsValid);
        Assert.Equal(MapManifestCodec.Digest(map), map.ManifestDigest);
        Assert.Equal(expectedDigest, map.ManifestDigest);
    }
}
