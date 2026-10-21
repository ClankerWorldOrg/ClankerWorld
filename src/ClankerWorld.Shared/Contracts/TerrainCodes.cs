namespace ClankerWorld.Contracts;

/// <summary>The server's packed terrain and layer vocabulary, shared with the renderer.</summary>
public static class TerrainCodes
{
    public const string TerrainEncoding = "terrain-kind-v1";
    public const string LayersEncoding = "map-layers-v2";

    public static byte RenderKind(byte value) => value switch
    {
        0 => 1,
        1 => 2,
        2 => 3,
        3 => 4,
        4 => 5,
        5 => 6,
        6 => 10,
        7 => 7,
        8 => 8,
        9 => 9,
        _ => throw new InvalidDataException("The packed world terrain contains an unknown kind."),
    };

    public static byte Encode(string name) => name.ToLowerInvariant() switch
    {
        "meadow" => 0,
        "water" => 1,
        "mountain" => 2,
        "river" => 3,
        "lake" => 4,
        "ocean" => 5,
        "peak" => 6,
        "sand" => 7,
        "forest" => 8,
        "snow" => 9,
        _ => throw new InvalidDataException("The world terrain contains an unknown kind."),
    };

    public static string NameFor(byte kind) => kind switch
    {
        1 => "Meadow",
        2 => "Water",
        3 => "Mountain",
        4 => "River",
        5 => "Lake",
        6 => "Ocean",
        7 => "Sand",
        8 => "Forest",
        9 => "Snow",
        10 => "Peak",
        _ => "Unavailable",
    };

    public static string? ClimateName(byte? value) => value switch
    {
        0 => "Tropical",
        1 => "Dry",
        2 => "Temperate",
        3 => "Cold",
        4 => "Polar",
        _ => null,
    };

    public static string? SurfaceName(byte? value) => value switch
    {
        0 => "Grass",
        1 => "Sand",
        2 => "Rock",
        3 => "Snow",
        4 => "Water",
        5 => "Forest floor",
        6 => "Dry scrub",
        7 => "Fertile soil",
        _ => null,
    };

    public static string? HydrologyName(byte? value) => value switch
    {
        0 => "Land",
        1 => "Ocean",
        2 => "Lake",
        3 => "River",
        _ => null,
    };

    public static string? VegetationName(byte? value) => value switch
    {
        0 => "None",
        1 => "Grass",
        2 => "Forest",
        3 => "Scrub",
        4 => "Tundra",
        5 => "Cactus",
        _ => null,
    };

}
