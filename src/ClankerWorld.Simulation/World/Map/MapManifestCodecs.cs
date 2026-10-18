using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation.World;

/// <summary>Canonical digest for the independent generated-map layer bytes.</summary>
public static class MapLayerManifestCodec
{
    private const string Header = "clankerworld.map-layers/v1";

    public static string? Digest(WorldMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.ClimateZones is not { } climate || map.ElevationLevels is not { } elevation ||
            map.HydrologyKinds is not { } hydrology || map.SurfaceKinds is not { } surface ||
            map.VegetationKinds is not { } vegetation)
            return null;

        var expectedLength = checked(map.Width * map.Height);
        if (climate.Length != expectedLength || elevation.Length != expectedLength ||
            hydrology.Length != expectedLength || surface.Length != expectedLength ||
            vegetation.Length != expectedLength)
            throw new InvalidDataException("The world map layers do not match their dimensions.");

        using var hash = SHA256.Create();
        using var digestStream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);
        using (var writer = new BinaryWriter(digestStream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Header);
            writer.Write(map.Width);
            writer.Write(map.Height);
            foreach (var layer in new[] { climate, elevation, hydrology, surface, vegetation })
            {
                writer.Write(layer.Length);
                writer.Write(layer);
            }
        }
        digestStream.FlushFinalBlock();
        return Convert.ToHexStringLower(hash.Hash!);
    }
}

/// <summary>
/// Explicit canonical bytes for the genesis map manifest. The digest is not
/// included in its own input, avoiding self-referential serialization.
/// </summary>
public static class MapManifestCodec
{
    private const string Header = "clankerworld.seeded-map/v1";

    public static byte[] Encode(WorldMap map)
    {
        using var stream = new MemoryStream();
        WriteManifest(map, stream);
        return stream.ToArray();
    }

    public static string Digest(WorldMap map)
    {
        using var hash = SHA256.Create();
        using var digestStream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);
        WriteManifest(map, digestStream);
        digestStream.FlushFinalBlock();
        return Convert.ToHexStringLower(hash.Hash!);
    }

    private static void WriteManifest(WorldMap map, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(map);
        using var writer = new StreamWriter(stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        var builder = new StringBuilder();
        builder.Append(Header).Append('\n');
        builder.Append("dimensions=").Append(map.Width).Append('x').Append(map.Height).Append('\n');
        builder.Append("generation_attempt=").Append(map.GenerationAttempt).Append('\n');
        WriteAndClear(builder, writer, force: true);
        if (map.ClimateZones is not null)
        {
            writer.Write("climate-zones=");
            writer.Write(Convert.ToBase64String(map.ClimateZones));
            writer.Write('\n');
        }
        foreach (var tile in OrderedTiles(map.Tiles))
        {
            builder.Append("tile=")
                .Append(tile.Position.X).Append(',').Append(tile.Position.Y).Append('|')
                .Append(ToWireValue(tile.Terrain)).Append('\n');
            WriteAndClear(builder, writer);
        }

        foreach (var mapObject in map.MapObjects.OrderBy(mapObject => mapObject.Id, StringComparer.Ordinal))
        {
            builder.Append("object=")
                .Append(mapObject.Id).Append('|').Append(mapObject.Kind).Append('|')
                .Append(mapObject.Position.X).Append(',').Append(mapObject.Position.Y).Append('\n');
            WriteAndClear(builder, writer);
        }

        foreach (var resource in map.Resources.OrderBy(resource => resource.Id, StringComparer.Ordinal))
        {
            builder.Append("resource=")
                .Append(resource.Id).Append('|').Append(resource.Kind).Append('|')
                .Append(resource.Position.X).Append(',').Append(resource.Position.Y).Append('|')
                .Append(resource.IsRenewable ? "renewable" : "finite");
            if (resource.TreeKind is { } treeKind)
                builder.Append("|tree:").Append(treeKind);
            if (resource.NaturalObjectKind is { } naturalObjectKind)
                builder.Append("|natural:").Append(naturalObjectKind);
            builder.Append('\n');
            WriteAndClear(builder, writer);
        }
        WriteAndClear(builder, writer, force: true);
    }

    private static void WriteAndClear(StringBuilder builder, TextWriter writer, bool force = false)
    {
        // Batch ordinary rows without retaining the complete manifest text.
        if (!force && builder.Length < 4096) return;
        foreach (var chunk in builder.GetChunks()) writer.Write(chunk.Span);
        builder.Clear();
    }

    private static IEnumerable<TerrainTile> OrderedTiles(IReadOnlyList<TerrainTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        for (var index = 1; index < tiles.Count; index++)
        {
            var previous = tiles[index - 1].Position;
            var current = tiles[index].Position;
            if (previous.Y > current.Y || previous.Y == current.Y && previous.X > current.X)
                return tiles.OrderBy(tile => tile.Position.Y).ThenBy(tile => tile.Position.X);
        }
        return tiles;
    }

    private static string ToWireValue(TerrainKind terrain) => terrain switch
    {
        TerrainKind.Meadow => "meadow",
        TerrainKind.Water => "water",
        TerrainKind.Mountain => "mountain",
        TerrainKind.River => "river",
        TerrainKind.Lake => "lake",
        TerrainKind.Ocean => "ocean",
        TerrainKind.Peak => "peak",
        TerrainKind.Sand => "sand",
        TerrainKind.Forest => "forest",
        TerrainKind.Snow => "snow",
        _ => throw new ArgumentOutOfRangeException(nameof(terrain)),
    };
}
