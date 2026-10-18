using System.Text.Json;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TerrainGridWriterTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new WorldTerrainChunkCodec() },
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeTerrainWriterKeepsEveryKindAndCanonicalTileOrder(bool reverse)
    {
        var map = Map();
        var expected = JsonSerializer.SerializeToUtf8Bytes(map, Options);
        if (reverse) map = map with { Tiles = map.Tiles.Reverse().ToArray() };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(map, Options);
        Assert.Equal(expected, bytes);
        using var document = JsonDocument.Parse(bytes);
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
            document.RootElement.GetProperty("terrainChunks")[0].GetProperty("data").GetBytesFromBase64());
        var decoded = JsonSerializer.Deserialize<WorldMap>(bytes, Options)!;
        Assert.Equal(Map().Tiles, decoded.Tiles);
        Assert.Equal(bytes, JsonSerializer.SerializeToUtf8Bytes(decoded, Options));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("outside-x")]
    [InlineData("outside-y")]
    [InlineData("negative-x")]
    [InlineData("negative-y")]
    [InlineData("unknown-kind")]
    [InlineData("negative-kind")]
    [InlineData("duplicate-unknown-kind")]
    [InlineData("zero-width")]
    [InlineData("oversized-width")]
    [InlineData("overflowing-area")]
    public void NativeTerrainWriterRefusesMalformedGridsBeforeWritingChunks(string damage)
    {
        var map = Map();
        var tiles = map.Tiles.ToList();
        switch (damage)
        {
            case "duplicate": tiles[^1] = tiles[0]; break;
            case "missing": tiles.RemoveAt(tiles.Count - 1); break;
            case "extra": tiles.Add(tiles[0]); break;
            case "outside-x": tiles[0] = tiles[0] with { Position = new(map.Width, 0) }; break;
            case "outside-y": tiles[0] = tiles[0] with { Position = new(0, map.Height) }; break;
            case "negative-x": tiles[0] = tiles[0] with { Position = new(-1, 0) }; break;
            case "negative-y": tiles[0] = tiles[0] with { Position = new(0, -1) }; break;
            case "unknown-kind": tiles[0] = tiles[0] with { Terrain = (TerrainKind)255 }; break;
            case "negative-kind": tiles[0] = tiles[0] with { Terrain = (TerrainKind)int.MinValue }; break;
            case "duplicate-unknown-kind": tiles[^1] = tiles[0] with { Terrain = (TerrainKind)255 }; break;
            case "zero-width": map = map with { Width = 0 }; break;
            case "oversized-width": map = map with { Width = 4097 }; break;
            case "overflowing-area": map = map with { Width = int.MaxValue, Height = int.MaxValue }; break;
        }
        var error = Assert.Throws<InvalidDataException>(() => JsonSerializer.SerializeToUtf8Bytes(map with { Tiles = tiles }, Options));
        Assert.Equal(damage switch
        {
            "duplicate" => "The world terrain has duplicate tiles.",
            "missing" or "extra" or "zero-width" or "oversized-width" or "overflowing-area" =>
                "The world terrain dimensions are invalid.",
            _ => "The world terrain tile is invalid.",
        }, error.Message);
    }

    [Fact]
    public void NativeTerrainWriterRechecksBorrowedTilesAfterSuccessfulEncoding()
    {
        var map = Map();
        var tiles = map.Tiles.ToArray();
        map = map with { Tiles = tiles };
        _ = JsonSerializer.SerializeToUtf8Bytes(map, Options);
        tiles[^1] = tiles[0];
        Assert.Throws<InvalidDataException>(() => JsonSerializer.SerializeToUtf8Bytes(map, Options));
    }

    private static WorldMap Map() => new(10, 1, 0,
        Enum.GetValues<TerrainKind>().Select((kind, index) => new TerrainTile(new(index, 0), kind)).ToArray(),
        [], [], string.Empty);
}
