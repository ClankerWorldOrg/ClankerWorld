using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class MapDigestLifetimeTests
{
    [Fact]
    public void LayerAndTextDigestsStayIndependentAfterRejectedMaps()
    {
        var tiles = Enumerable.Range(0, 4).Select(index =>
            new TerrainTile(new(index % 2, index / 2), TerrainKind.Meadow)).ToArray();
        var map = new WorldMap(2, 2, 0, tiles, [], [], string.Empty)
        {
            ClimateZones = [0, 1, 2, 3],
            ElevationLevels = [0, 255, 1, 254],
            HydrologyKinds = [3, 2, 1, 0],
            SurfaceKinds = [0, 1, 2, 3],
            VegetationKinds = [1, 0, 1, 0],
        };
        const string layerDigest = "44b1026746e51953ac90ca092febb58325219bcf9f3761b7880ce7f1b56ba387";
        const string textDigest = "08bb6588cf05686c13dfe64be3b9d4b1044fd00d1379701612aff677ff936bf5";
        Assert.Equal(layerDigest, MapLayerManifestCodec.Digest(map));
        Assert.Throws<InvalidDataException>(() => MapLayerManifestCodec.Digest(map with { ElevationLevels = [0, 1, 2] }));
        Assert.Null(MapLayerManifestCodec.Digest(map with { ClimateZones = null }));
        Assert.Throws<ArgumentNullException>(() => MapLayerManifestCodec.Digest(null!));
        Assert.Throws<ArgumentNullException>(() => MapManifestCodec.Digest(null!));
        var unknownTerrain = tiles.ToArray();
        unknownTerrain[^1] = unknownTerrain[^1] with { Terrain = (TerrainKind)255 };
        Assert.Throws<ArgumentOutOfRangeException>(() => MapManifestCodec.Digest(map with { Tiles = unknownTerrain }));
        Assert.Equal(textDigest, MapManifestCodec.Digest(map));
        Assert.Equal(layerDigest, MapLayerManifestCodec.Digest(map));
        Assert.Equal(textDigest, MapManifestCodec.Digest(map));
    }
}
