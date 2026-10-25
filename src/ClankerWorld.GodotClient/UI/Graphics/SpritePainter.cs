using Godot;

namespace ClankerWorld.GodotClient.UI;

/// <summary>Shared shapes in pixel units; agents keep raw blending, animals snap it to 8-bit steps.</summary>
internal class SpritePainter(Image image, bool snap = false)
{
    protected Image Image => image;

    public delegate bool Keep(Vector2 offset, float along, float across);

    /// <summary>Whether an offset from an oval's centre lies inside it.</summary>
    public static bool InOval(Vector2 offset, Vector2 direction, float along, float across)
    {
        var t = offset.Dot(direction);
        var u = offset.Dot(new Vector2(-direction.Y, direction.X));
        var rt = MathF.Max(along, 0.55f);
        var ru = MathF.Max(across, 0.55f);
        return (t * t) / (rt * rt) + (u * u) / (ru * ru) <= 1f;
    }

    public void Pixel(int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= image.GetWidth() || y >= image.GetHeight()) return;
        if (color.A <= 0)
        {
            image.SetPixel(x, y, Colors.Transparent);
            return;
        }
        var blended = image.GetPixel(x, y).Blend(color);
        image.SetPixel(x, y, snap ? PixelArt.Snap(blended) : blended);
    }

    public void Dot(Vector2 at, Color color) => Pixel((int)MathF.Floor(at.X), (int)MathF.Floor(at.Y), color);

    public void Disc(Vector2 center, float radius, Color color) => Oval(center, new Vector2(0, 1), radius, radius, color);

    /// <summary>An oval with half depth <paramref name="along"/> the direction and half width across it.</summary>
    public void Oval(Vector2 center, Vector2 direction, float along, float across, Color color, Keep? keep = null)
    {
        var side = new Vector2(-direction.Y, direction.X);
        var rt = MathF.Max(along, 0.55f);
        var ru = MathF.Max(across, 0.55f);
        var reach = MathF.Max(rt, ru) + 1;
        for (var y = (int)MathF.Floor(center.Y - reach); y <= (int)MathF.Ceiling(center.Y + reach); y++)
            for (var x = (int)MathF.Floor(center.X - reach); x <= (int)MathF.Ceiling(center.X + reach); x++)
            {
                var q = new Vector2(x + 0.5f - center.X, y + 0.5f - center.Y);
                var t = q.Dot(direction);
                var u = q.Dot(side);
                if ((t * t) / (rt * rt) + (u * u) / (ru * ru) > 1f) continue;
                if (keep is null || keep(q, t, u)) Pixel(x, y, color);
            }
    }

    /// <summary>A rectangle turned to the direction: half depth along it, half width across.</summary>
    public void Box(Vector2 center, Vector2 direction, float halfAlong, float halfAcross, Color color, Keep? keep = null)
    {
        var side = new Vector2(-direction.Y, direction.X);
        var reach = MathF.Max(halfAlong, halfAcross) * 1.5f + 1;
        for (var y = (int)MathF.Floor(center.Y - reach); y <= (int)MathF.Ceiling(center.Y + reach); y++)
            for (var x = (int)MathF.Floor(center.X - reach); x <= (int)MathF.Ceiling(center.X + reach); x++)
            {
                var q = new Vector2(x + 0.5f - center.X, y + 0.5f - center.Y);
                var t = q.Dot(direction);
                var u = q.Dot(side);
                if (MathF.Abs(t) > halfAlong || MathF.Abs(u) > halfAcross) continue;
                if (keep is null || keep(q, t, u)) Pixel(x, y, color);
            }
    }

    /// <summary>An axis-aligned rectangle from its top-left corner.</summary>
    public void Rect(float x, float y, float width, float height, Color color)
    {
        var left = (int)MathF.Floor(x);
        var top = (int)MathF.Floor(y);
        for (var py = top; py < top + (int)MathF.Round(height); py++)
            for (var px = left; px < left + (int)MathF.Round(width); px++)
                Pixel(px, py, color);
    }

    /// <summary>A one-pixel line.</summary>
    public void Line(Vector2 from, Vector2 to, Color color)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling(from.DistanceTo(to) * 2));
        int lastX = int.MinValue, lastY = int.MinValue;
        for (var step = 0; step <= steps; step++)
        {
            var point = from.Lerp(to, step / (float)steps);
            var x = (int)MathF.Floor(point.X);
            var y = (int)MathF.Floor(point.Y);
            if (x == lastX && y == lastY) continue;
            lastX = x;
            lastY = y;
            Pixel(x, y, color);
        }
    }
}
