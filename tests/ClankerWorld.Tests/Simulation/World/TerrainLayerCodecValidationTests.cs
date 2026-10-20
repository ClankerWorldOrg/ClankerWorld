using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TerrainLayerCodecValidationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new WorldTerrainChunkCodec() },
    };

    [Theory]
    [InlineData("climateZones")]
    [InlineData("elevationLevels")]
    [InlineData("hydrologyKinds")]
    [InlineData("surfaceKinds")]
    [InlineData("vegetationKinds")]
    public void NativeTerrainReadAndWriteAcceptExactlyTheDeclaredLayerBytes(string layer)
    {
        var template = JsonNode.Parse(JsonSerializer.SerializeToUtf8Bytes(Map(), Options))!;
        byte[] data = [0];
        var map = WithLayer(Map(), layer, data);
        for (var value = 0; value <= byte.MaxValue; value++)
        {
            data[0] = (byte)value; // Reuse borrowed storage after both successes and refusals.
            var known = layer switch
            {
                "climateZones" => Enum.IsDefined((ClimateZone)value),
                "hydrologyKinds" => Enum.IsDefined((WaterKind)value),
                "surfaceKinds" => Enum.IsDefined((SurfaceKind)value),
                "vegetationKinds" => Enum.IsDefined((VegetationCover)value),
                _ => true, // Every elevation byte is supported.
            };
            var document = StoredLayer(template, map, layer, data);
            if (known)
            {
                var encoded = JsonSerializer.SerializeToUtf8Bytes(map, Options);
                Assert.Equal(data, Layer(JsonSerializer.Deserialize<WorldMap>(encoded, Options)!, layer));
                Assert.Equal(data, Layer(JsonSerializer.Deserialize<WorldMap>(document.ToJsonString(), Options)!, layer));
            }
            else
            {
                Assert.Equal("The world map layers are invalid.", Assert.Throws<InvalidDataException>(() =>
                    JsonSerializer.SerializeToUtf8Bytes(map, Options)).Message);
                Assert.Equal("The world map layers are invalid.", Assert.Throws<InvalidDataException>(() =>
                    JsonSerializer.Deserialize<WorldMap>(document.ToJsonString(), Options)).Message);
            }
        }
        data[0] = 0;
        Assert.Equal(data, Layer(JsonSerializer.Deserialize<WorldMap>(JsonSerializer.SerializeToUtf8Bytes(map, Options), Options)!, layer));
    }

    [Theory]
    [InlineData("climateZones")]
    [InlineData("elevationLevels")]
    [InlineData("hydrologyKinds")]
    [InlineData("surfaceKinds")]
    [InlineData("vegetationKinds")]
    public void NativeTerrainKeepsOptionalLayersAndItsReadWriteLengthRefusals(string layer)
    {
        var missing = WithLayer(Map(), layer, null);
        var encoded = JsonSerializer.SerializeToUtf8Bytes(missing, Options);
        Assert.Null(Layer(JsonSerializer.Deserialize<WorldMap>(encoded, Options)!, layer));
        var template = JsonNode.Parse(JsonSerializer.SerializeToUtf8Bytes(Map(), Options))!;
        foreach (var length in new[] { 0, 2 })
        {
            var data = new byte[length];
            var map = WithLayer(Map(), layer, data);
            Assert.Equal("The world map layers are invalid.", Assert.Throws<InvalidDataException>(() =>
                JsonSerializer.SerializeToUtf8Bytes(map, Options)).Message);
            var document = StoredLayer(template, map, layer, data, includeDigest: false);
            Assert.Equal(layer == "climateZones" ? "The world map layers are invalid." : "The world map layer is damaged.",
                Assert.Throws<InvalidDataException>(() => JsonSerializer.Deserialize<WorldMap>(document.ToJsonString(), Options)).Message);
        }
    }

    private static JsonNode StoredLayer(JsonNode template, WorldMap map, string layer, byte[] data, bool includeDigest = true)
    {
        var document = template.DeepClone();
        if (layer == "climateZones") document[layer] = Convert.ToBase64String(data);
        else
        {
            using var stream = new MemoryStream();
            using (var compressor = new BrotliStream(stream, CompressionLevel.Fastest, leaveOpen: true)) compressor.Write(data);
            document[layer] = Convert.ToBase64String(stream.ToArray());
        }
        document["mapLayersSha256"] = includeDigest ? MapLayerManifestCodec.Digest(map) : null;
        return document;
    }

    private static byte[]? Layer(WorldMap map, string layer) => layer switch
    {
        "climateZones" => map.ClimateZones,
        "elevationLevels" => map.ElevationLevels,
        "hydrologyKinds" => map.HydrologyKinds,
        "surfaceKinds" => map.SurfaceKinds,
        _ => map.VegetationKinds,
    };

    private static WorldMap WithLayer(WorldMap map, string layer, byte[]? data) => layer switch
    {
        "climateZones" => map with { ClimateZones = data },
        "elevationLevels" => map with { ElevationLevels = data },
        "hydrologyKinds" => map with { HydrologyKinds = data },
        "surfaceKinds" => map with { SurfaceKinds = data },
        _ => map with { VegetationKinds = data },
    };

    private static WorldMap Map() => new(1, 1, 0, [new(new(0, 0), TerrainKind.Meadow)], [], [], string.Empty)
    {
        ClimateZones = [0],
        ElevationLevels = [100],
        HydrologyKinds = [0],
        SurfaceKinds = [0],
        VegetationKinds = [0],
    };
}
