using Godot;

namespace ClankerWorld.GodotClient.UI;

public enum LandscapeSeason { Summer, Spring, Autumn, Winter }

/// <summary>The approved shared seasonal treatment of grass and canopy ramps.</summary>
public static class LandscapePalette
{
    private static readonly Color Grass = new("5F8F5B");
    private static readonly Color Spring = Multiplier(Grass.Lerp(new Color("86B37A"), 0.2f));
    private static readonly Color Autumn = Multiplier(Grass.Lerp(new Color("E0893F"), 0.2f));

    public static LandscapeSeason Season(string? observed) => observed?.ToLowerInvariant() switch
    {
        "spring" => LandscapeSeason.Spring,
        "autumn" => LandscapeSeason.Autumn,
        "winter" => LandscapeSeason.Winter,
        _ => LandscapeSeason.Summer,
    };

    public static bool HasGrass(TerrainStyle style) => style is TerrainStyle.Grass or TerrainStyle.ForestGrass or
        TerrainStyle.ScrubGrass or TerrainStyle.ForestFloor or TerrainStyle.DenseForestFloor or TerrainStyle.Tundra;

    public static Color Apply(Color color, LandscapeSeason season)
    {
        if (season == LandscapeSeason.Summer) return color;
        if (season == LandscapeSeason.Winter)
        {
            var grey = color.R * 0.2126f + color.G * 0.7152f + color.B * 0.0722f;
            return PixelArt.Snap(color.Lerp(new Color(grey, grey, grey, color.A), 0.3f));
        }
        var tint = season == LandscapeSeason.Spring ? Spring : Autumn;
        return PixelArt.Snap(new Color(color.R * tint.R, color.G * tint.G, color.B * tint.B, color.A));
    }

    public static Color Ground(Color color, TerrainStyle style, LandscapeSeason season) =>
        HasGrass(style) ? Apply(color, season) : color;

    /// <summary>Transition masks contain only one ramp colour, so no new edge atlas is needed.</summary>
    public static Color GroundTint(TerrainStyle style, LandscapeSeason season)
    {
        var original = TerrainTextures.BaseColor(style);
        var next = Ground(original, style, season);
        return new Color(next.R / original.R, next.G / original.G, next.B / original.B);
    }

    private static Color Multiplier(Color target) => new(target.R / Grass.R, target.G / Grass.G, target.B / Grass.B);
}

/// <summary>The approved rose dawn and gold dusk multiply, shared with the reference renderer.</summary>
public static class GoldenHourTint
{
    public const float MaximumAmount = 0.22f;
    public static readonly Color Rose = new("E9A3A0");
    public static readonly Color Gold = new("F2B160");

    public static Color ColorAt(bool dawn) => dawn ? Rose : Gold;

    /// <summary>Zero at full day/night; strongest halfway through the host's seasonal twilight.</summary>
    public static float StrengthAt(float darkness)
    {
        var dark = Math.Clamp(darkness, 0, 1);
        return 4 * dark * (1 - dark);
    }

    public static Color Multiplier(bool dawn, float strength) =>
        Colors.White.Lerp(ColorAt(dawn), MaximumAmount * Math.Clamp(strength, 0, 1));
}
