using ClankerWorld.GodotClient.Protocol;
using Godot;

namespace ClankerWorld.GodotClient.UI;

/// <summary>Compact, indexed map facts shared by the local camera and overview.</summary>
public sealed class WorldTerrainMap
{
    private readonly byte[] terrain;
    private readonly byte[]? climate;
    private readonly byte[]? elevation;
    private readonly byte[]? hydrology;
    private readonly byte[]? surface;
    private readonly byte[]? vegetation;
    private readonly byte[]? fertility;
    private TerrainStyle[]? styles;
    private bool[]? hills;

    private WorldTerrainMap(int width, int height, byte[] terrain, byte[]? climate = null,
        byte[]? elevation = null, byte[]? hydrology = null, byte[]? surface = null,
        byte[]? vegetation = null, bool wrapsEastWest = false, byte[]? fertility = null)
    {
        Width = width;
        Height = height;
        WrapsEastWest = wrapsEastWest;
        this.terrain = terrain;
        this.climate = climate;
        this.elevation = elevation;
        this.hydrology = hydrology;
        this.surface = surface;
        this.vegetation = vegetation;
        this.fertility = fertility;
    }

    public int Width { get; }
    public int Height { get; }
    public bool WrapsEastWest { get; }
    public bool HasMapLayers => climate is not null;

    public static WorldTerrainMap FromTiles(IReadOnlyList<OwnerWorldTile> tiles, int width, int height,
        OwnerWorldPackedMapLayers? layers = null, bool wrapsEastWest = false)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var terrain = new byte[checked(width * height)];
        foreach (var tile in tiles)
        {
            if (tile.X < 0 || tile.X >= width || tile.Y < 0 || tile.Y >= height) continue;
            terrain[tile.Y * width + tile.X] = tile.Terrain is "meadow" or "water" or "mountain" or "river" or "lake" or "ocean" or "peak" or "sand" or "forest" or "snow"
                ? TerrainCodes.RenderKind(TerrainCodes.Encode(tile.Terrain)) : (byte)0;
        }
        return WithLayers(width, height, terrain, layers, wrapsEastWest);
    }

    public static WorldTerrainMap FromPacked(OwnerWorldPackedTerrain packed,
        OwnerWorldPackedMapLayers? layers = null, bool wrapsEastWest = false)
    {
        ArgumentNullException.ThrowIfNull(packed);
        if (packed.Encoding != TerrainCodes.TerrainEncoding || packed.Width <= 0 || packed.Height <= 0)
            throw new InvalidDataException("The world terrain encoding or dimensions are unsupported.");
        var source = Convert.FromBase64String(packed.Data);
        if (source.Length != checked(packed.Width * packed.Height))
            throw new InvalidDataException("The packed world terrain length is invalid.");
        var terrain = new byte[source.Length];
        for (var index = 0; index < source.Length; index++)
            terrain[index] = TerrainCodes.RenderKind(source[index]);
        return WithLayers(packed.Width, packed.Height, terrain, layers, wrapsEastWest);
    }

    public byte At(int x, int y) => terrain[y * Width + x];
    public byte? ClimateAt(int x, int y) => LayerAt(climate, x, y);
    public byte? ElevationAt(int x, int y) => LayerAt(elevation, x, y);
    public byte? HydrologyAt(int x, int y) => LayerAt(hydrology, x, y);
    public byte? SurfaceAt(int x, int y) => LayerAt(surface, x, y);
    public byte? VegetationAt(int x, int y) => LayerAt(vegetation, x, y);
    public byte? FertilityAt(int x, int y) => LayerAt(fertility, x, y);
    public static string FertilityName(int value) => value <= 0 ? "Not farmable" : value < 35 ? "Poor" : value < 55 ? "Fair" : value < 75 ? "Good" : "Rich";

    /// <summary>Render-only legacy projection; inspection keeps missing layer facts unavailable.</summary>
    public byte RenderSurfaceAt(int x, int y) => SurfaceAt(x, y) ?? At(x, y) switch
    {
        1 => 0, // meadow
        2 or 4 or 5 or 6 => 4, // water, river, lake, ocean
        3 or 10 => 2, // mountain, peak
        7 => 1, // sand
        8 => 5, // forest floor
        9 => 3, // snow
        _ => 0,
    };

    /// <summary>Bit mask of cardinal edges where land meets generated water; N/E/S/W are bits 1/2/4/8.</summary>
    public byte WaterEdgeMaskAt(int x, int y, bool wrapsEastWest)
    {
        if (!HasMapLayers || HydrologyAt(x, y) is not 0) return 0;
        return EdgeMask(x, y, wrapsEastWest,
            (nx, ny) => HydrologyAt(nx, ny) is { } value && value != 0);
    }

    /// <summary>Bit mask where a neighboring ground surface differs, for broken natural transition edges.</summary>
    public byte SurfaceBoundaryMaskAt(int x, int y, bool wrapsEastWest)
    {
        if (!HasMapLayers || SurfaceAt(x, y) is not { } current || current == 4) return 0;
        return EdgeMask(x, y, wrapsEastWest,
            (nx, ny) => SurfaceAt(nx, ny) is { } adjacent && adjacent != current);
    }

    private byte EdgeMask(int x, int y, bool wrapsEastWest, Func<int, int, bool> isEdge)
    {
        byte mask = 0;
        if (y > 0 && isEdge(x, y - 1)) mask |= 1;
        var east = x + 1;
        if (east == Width && wrapsEastWest) east = 0;
        if (east < Width && isEdge(east, y)) mask |= 2;
        if (y + 1 < Height && isEdge(x, y + 1)) mask |= 4;
        var west = x - 1;
        if (west < 0 && wrapsEastWest) west = Width - 1;
        if (west >= 0 && isEdge(west, y)) mask |= 8;
        return mask;
    }

    /// <summary>Flat overview color: the base of the tile's pixel-art ground style, warmed on hills.</summary>
    public Color DisplayColorAt(int x, int y) => IsHillAt(x, y)
        ? TerrainTextures.HillColor(TerrainTextures.BaseColor(StyleAt(x, y)))
        : TerrainTextures.BaseColor(StyleAt(x, y));

    /// <summary>
    /// Whether the world marks the tile as cactus cover: dry desert sand or
    /// brush where cacti grow. The ground draws as desert brush, with a cactus
    /// on some of these tiles (<see cref="CactusSprites.ForTile"/>).
    /// </summary>
    public bool IsCactusCoverAt(int x, int y) =>
        x >= 0 && y >= 0 && x < Width && y < Height && VegetationAt(x, y) == 5 && StyleAt(x, y) == TerrainStyle.DesertBrush;

    /// <summary>
    /// Whether the tile is in the hill band at a mountain's base: dry land
    /// below mountain height but at least 190 high, within a reach that widens
    /// with the size of the massif (<see cref="HillBand"/>). Hills are drawn
    /// over the ground; they walk like grass.
    /// </summary>
    public bool IsHillAt(int x, int y)
    {
        if (!HasMapLayers || x < 0 || y < 0 || x >= Width || y >= Height) return false;
        hills ??= HillBand.Classify(elevation!, hydrology!, Width, Height, WrapsEastWest);
        return hills[y * Width + x];
    }

    /// <summary>
    /// Ground style from independent surface/cover facts, falling back to the
    /// v1 terrain projection for old maps. Cover overrides elevation, matching
    /// the original flat palette.
    /// </summary>
    public TerrainStyle StyleAt(int x, int y)
    {
        // Ground transitions read eight neighbors per drawn tile, so styles
        // are derived once per map; the layers never change after loading.
        if (styles is null)
        {
            var computed = new TerrainStyle[checked(Width * Height)];
            for (var index = 0; index < computed.Length; index++) computed[index] = ComputeStyle(index);
            styles = computed;
        }
        return styles[y * Width + x];
    }

    private TerrainStyle ComputeStyle(int index)
    {
        if (!HasMapLayers)
            return terrain[index] switch
            {
                1 => TerrainStyle.Grass,
                2 => TerrainStyle.ShallowWater,
                3 => TerrainStyle.Mountain,
                4 => TerrainStyle.River,
                5 => TerrainStyle.Lake,
                6 => TerrainStyle.Ocean,
                7 => TerrainStyle.Sand,
                8 => TerrainStyle.ForestGrass,
                9 => TerrainStyle.Snow,
                10 => TerrainStyle.Peak,
                _ => TerrainStyle.Unknown,
            };
        var water = hydrology![index];
        if (water != 0)
            return water switch
            {
                1 => TerrainStyle.Ocean,
                2 => TerrainStyle.Lake,
                3 => TerrainStyle.River,
                _ => TerrainStyle.Unknown,
            };
        var ground = surface![index];
        var covered = (vegetation![index], ground) switch
        {
            (2, 0) => TerrainStyle.ForestGrass,
            (2, 5) => TerrainStyle.DenseForestFloor,
            (3, 0) => TerrainStyle.ScrubGrass,
            (3, 6) => TerrainStyle.DryBrush,
            (4, 0) => TerrainStyle.Tundra,
            (3, 1) => TerrainStyle.ScrubSand,
            (4, 3) => TerrainStyle.TundraSnow,
            // Cacti are not part of the current art direction; dry cactus
            // cover reads as brush.
            (5, 1) or (5, 6) => TerrainStyle.DesertBrush,
            _ => (TerrainStyle?)null,
        };
        if (covered is { } style) return style;
        if (elevation![index] >= 245) return TerrainStyle.Peak;
        if (elevation[index] >= 215) return TerrainStyle.Mountain;
        return ground switch
        {
            0 => TerrainStyle.Grass,
            1 => TerrainStyle.Sand,
            2 => TerrainStyle.Rock,
            3 => TerrainStyle.Snow,
            4 => TerrainStyle.ShallowWater,
            5 => TerrainStyle.ForestFloor,
            6 => TerrainStyle.DryScrub,
            7 => TerrainStyle.FertileSoil,
            _ => TerrainStyle.Unknown,
        };
    }

    private byte? LayerAt(byte[]? layer, int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height && layer is not null
            ? layer[y * Width + x] : null;

    private static WorldTerrainMap WithLayers(int width, int height, byte[] terrain,
        OwnerWorldPackedMapLayers? layers, bool wrapsEastWest)
    {
        if (layers is null) return new WorldTerrainMap(width, height, terrain, wrapsEastWest: wrapsEastWest);
        if (layers.Width != width || layers.Height != height ||
            layers.Encoding is not ("map-layers-v1" or "map-layers-v2"))
            throw new InvalidDataException("The packed world map layers have an unsupported encoding or dimensions.");
        var length = checked(width * height);
        var climate = DecodeLayer(layers.Climate, length, 5, "climate");
        var elevation = DecodeLayer(layers.Elevation, length, null, "elevation");
        var hydrology = DecodeLayer(layers.Hydrology, length, 3, "hydrology");
        var surface = DecodeLayer(layers.Surface, length, 7, "surface");
        var vegetation = DecodeLayer(layers.Vegetation, length, 5, "vegetation");
        var fertility = layers.Fertility is null ? null : DecodeLayer(layers.Fertility, length, 100, "fertility");
        return new WorldTerrainMap(width, height, terrain, climate, elevation, hydrology, surface, vegetation,
            wrapsEastWest, fertility);
    }

    private static byte[] DecodeLayer(string encoded, int expectedLength, int? maximumValue, string name)
    {
        var bytes = Convert.FromBase64String(encoded);
        if (bytes.Length != expectedLength || maximumValue is { } max && bytes.Any(value => value > max))
            throw new InvalidDataException($"The packed world {name} layer is invalid.");
        return bytes;
    }

    public static string NameFor(byte value) => TerrainCodes.NameFor(value);
    public static string? ClimateName(byte? value) => TerrainCodes.ClimateName(value);
    public static string? SurfaceName(byte? value) => TerrainCodes.SurfaceName(value);
    public static string? HydrologyName(byte? value) => TerrainCodes.HydrologyName(value);
    public static string? VegetationName(byte? value) => TerrainCodes.VegetationName(value);

    public static string? NaturalObjectName(string? value) => value switch
    {
        "berry_bush" => "Berry bush",
        "wild_greens" => "Wild greens",
        "fiber_plant" => "Fiber plant",
        "reeds" => "Reeds",
        "stone_outcrop" => "Stone outcrop",
        "fallen_wood" => "Fallen wood",
        "iron_outcrop" => "Iron outcrop",
        "gold_outcrop" => "Gold outcrop",
        "diamond_outcrop" => "Diamond outcrop",
        "clay_bank" => "Clay bank",
        "medicinal_herb_patch" => "Medicinal herb patch",
        "wild_seed_patch" => "Wild seed patch",
        "fertile_soil" => "Fertile soil",
        _ => null,
    };
}
