using Godot;

namespace ClankerWorld.GodotClient.UI;

public static partial class BuildingSprites
{
    private static Rect2I Bounds(bool[,] mask)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (var y = 0; y < mask.GetLength(1); y++)
            for (var x = 0; x < mask.GetLength(0); x++)
                if (mask[x, y])
                {
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                }
        return maxX < 0 ? new Rect2I(0, 0, 1, 1) : new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static bool In(bool[,] mask, int x, int y) => x >= 0 && y >= 0 && x < mask.GetLength(0) && y < mask.GetLength(1) && mask[x, y];

    /// <summary>Whole-pixel distance from the mask edge (1 on the rim), up to the limit.</summary>
    private static int Depth(bool[,] mask, int x, int y, int limit)
    {
        for (var d = 1; d <= limit; d++)
            for (var dy = -d; dy <= d; dy++)
                for (var dx = -d; dx <= d; dx++)
                    if ((Math.Abs(dx) == d || Math.Abs(dy) == d) && !In(mask, x + dx, y + dy)) return d;
        return limit + 1;
    }

    /// <summary>Whole-pixel distance from an empty pixel to the mask; limit + 1 when farther.</summary>
    private static int Near(bool[,] mask, int x, int y, int limit)
    {
        for (var d = 1; d <= limit; d++)
            for (var dy = -d; dy <= d; dy++)
                for (var dx = -d; dx <= d; dx++)
                    if ((Math.Abs(dx) == d || Math.Abs(dy) == d) && In(mask, x + dx, y + dy)) return d;
        return limit + 1;
    }

    private static void Blend(Image image, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= image.GetWidth() || y >= image.GetHeight()) return;
        image.SetPixel(x, y, color.A >= 0.999f ? color : PixelArt.Snap(image.GetPixel(x, y).Blend(color)));
    }

    private static uint Hash(int x, int y, int salt) => PixelArt.Hash(x, y, salt);
}
