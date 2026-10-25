using Godot;

namespace ClankerWorld.GodotClient.UI;

/// <summary>Shared palettes, shadow masks and snapped image plates. Transparent plate input leaves pixels unchanged.</summary>
internal static class SpriteDrawing
{
    internal readonly record struct Ramp(Color Edge, Color Shade, Color Base, Color Light, Color Highlight)
    {
        public static Ramp Of(string edge, string shade, string @base, string light, string highlight) =>
            new(new Color(edge), new Color(shade), new Color(@base), new Color(light), new Color(highlight));

        /// <summary>Step 0 is the edge, 4 the highlight; out-of-range steps clamp.</summary>
        public Color this[int step] => step switch
        {
            <= 0 => Edge,
            1 => Shade,
            2 => Base,
            3 => Light,
            _ => Highlight,
        };
    }

    internal sealed class ShadowMask(int width, int height)
    {
        private readonly bool[] covered = new bool[width * height];

        public void Add(int x, int y)
        {
            if (x >= 0 && y >= 0 && x < width && y < height) covered[y * width + x] = true;
        }

        public void Add(Rect2I area, int dx, int dy)
        {
            for (var y = area.Position.Y; y < area.End.Y; y++)
                for (var x = area.Position.X; x < area.End.X; x++)
                    Add(x + dx, y + dy);
        }

        public void Paint(Plate p, Color color)
        {
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    if (covered[y * width + x]) p.Put(x, y, color);
        }
    }

    internal sealed class Plate
    {
        public Plate(int width, int height, float scale)
        {
            Width = width;
            Height = height;
            Scale = scale;
            Image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
            Image.Fill(Colors.Transparent);
        }

        public Image Image { get; }
        public int Width { get; }
        public int Height { get; }
        public float Scale { get; }
        public bool Small => Scale < 1;
        public PixelCanvas Canvas => new(Image, new Rect2I(0, 0, Width, Height), Scale, snap: true);

        /// <summary>Units to pixels, rounded.</summary>
        public int P(float units) => (int)MathF.Round(units * Scale);

        public Rect2I Px(Rect2 units)
        {
            var left = P(units.Position.X);
            var top = P(units.Position.Y);
            return new Rect2I(left, top, P(units.End.X) - left, P(units.End.Y) - top);
        }

        public void Put(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height || color.A <= 0) return;
            Image.SetPixel(x, y, PixelArt.Snap(Image.GetPixel(x, y).Blend(color)));
        }

        public void Fill(Rect2I area, Color color)
        {
            for (var y = area.Position.Y; y < area.End.Y; y++)
                for (var x = area.Position.X; x < area.End.X; x++)
                    Put(x, y, color);
        }

        public void Fill(int x, int y, int width, int height, Color color) => Fill(new Rect2I(x, y, width, height), color);

        /// <summary>A disc centred on a pixel position, radius in pixels.</summary>
        public void Disc(float x, float y, float radius, Color color) =>
            Ellipse(x / Scale, y / Scale, radius / Scale, radius / Scale, color);

        /// <summary>An ellipse in tile units, covering the same pixels as <see cref="PixelCanvas.Ellipse"/>.</summary>
        public void Ellipse(float centerX, float centerY, float radiusX, float radiusY, Color color)
        {
            var cx = centerX * Scale;
            var cy = centerY * Scale;
            var rx = Math.Max(0.6f, radiusX * Scale);
            var ry = Math.Max(0.6f, radiusY * Scale);
            for (var y = (int)(cy - ry - 1); y <= (int)(cy + ry + 1); y++)
                for (var x = (int)(cx - rx - 1); x <= (int)(cx + rx + 1); x++)
                {
                    var dx = (x + 0.5f - cx) / rx;
                    var dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Put(x, y, color);
                }
        }

        /// <summary>A one-pixel line in tile units, in an opaque colour.</summary>
        public void Line(float fromX, float fromY, float toX, float toY, Color color) =>
            new PixelCanvas(Image, new Rect2I(0, 0, Width, Height), Scale).Line(fromX, fromY, toX, toY, color);
    }
}
