using Godot;

namespace ClankerWorld.GodotClient.UI;

/// <summary>Shared palettes, shadow masks and snapped image plates. Transparent plate input leaves pixels unchanged.</summary>
internal static class SpriteDrawing
{
    // Shared steps from the approved art palette; art-specific variants stay with their drawings.
    internal static readonly Ramp Timber = Ramp.Of("3F2A1A", "6E4E31", "8A6440", "A77C52", "D2AC77");
    internal static readonly Ramp Rock = Ramp.Of("4A4542", "625B56", "756D68", "8B837D", "A49C95");
    internal static readonly Ramp Dirt = Ramp.Of("6E5538", "977852", "B99A6B", "C9AC7C", "D9C08F");
    internal static readonly Ramp OrchardCanopy = Ramp.Of("3C5F2E", "4C7A3A", "5E8C45", "79A657", "9BC66F");
    internal static readonly Ramp Fruit = Ramp.Of("9A4E1E", "C8702E", "E0893F", "F6C27A", "FFE0A8");
    internal static readonly Ramp Thatch = Ramp.Of("6B5528", "A98A45", "D2AE5E", "E6C77B", "F0DA9A");
    internal static readonly Ramp ClayTile = Ramp.Of("5E2E22", "9E4E34", "C66A45", "E08E64", "EFA982");
    internal static readonly Ramp Slate = Ramp.Of("2B2E33", "4A4E55", "62666E", "80858E", "9A9FA7");
    internal static readonly Ramp GreyTimber = Ramp.Of("343C43", "59656F", "758390", "97A5B0", "AEBBC4");
    internal static readonly Ramp Doorstep = Ramp.Of("5F5848", "8C7F66", "B9AB8E", "C9BDA2", "DED3BC");
    internal static readonly Ramp Iron = Ramp.Of("3E3A37", "524C48", "6C6560", "8A827C", "A69E98");
    internal static readonly Ramp Cloth = Ramp.Of("75674D", "A09170", "CABC99", "E8DCC0", "FFF5DF");
    internal static readonly Ramp Gold = Ramp.Of("8A6A1E", "B8902E", "D9AE3C", "F2CC5E", "FFE28A");
    internal static readonly Ramp Berry = Ramp.Of("7A2A2E", "A33A3F", "C4474B", "F08A8A", "FFC2C2");
    internal static readonly Ramp River = Ramp.Of("2F5A75", "3B7294", "4786AB", "5695B8", "7FB4CF");
    internal static readonly Ramp Conifer = Ramp.Of("1F3E31", "2F5B45", "3F7358", "5E9278", "86B89A");
    internal static readonly Ramp Soil = Ramp.Of("4A3A2A", "5C4B35", "735F45", "86704F", "9A8460");
    internal static readonly Ramp Canopy = Ramp.Of("2E4A2A", "476B36", "557D3E", "6C9A4B", "8DB660");
    internal static readonly Ramp ForestGrass = Ramp.Of("2E4D35", "395F41", "426D4B", "4E7D56", "5E8E64");

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

    /// <summary>
    /// A cloth sack lying on its side along the x or y axis: a long rounded
    /// body, lit north-west, with its neck tied off in twine at the west or
    /// north end and the tuft flaring past it. Drawn at 32 px only.
    /// </summary>
    internal static void LyingSack(Plate p, float cx, float cy, bool alongX, Ramp cloth, Ramp timber, Color shadow)
    {
        var (rx, ry) = alongX ? (3.5f, 2.5f) : (2.5f, 3.5f);
        p.Ellipse(cx + 1, cy + 1.5f, rx, ry, shadow);
        p.Ellipse(cx, cy, rx, ry, cloth.Edge);
        p.Ellipse(cx + 0.4f, cy + 0.4f, rx - 0.9f, ry - 0.9f, cloth.Shade);
        p.Ellipse(cx - 0.3f, cy - 0.3f, rx - 1.2f, ry - 1.2f, cloth.Base);
        p.Ellipse(cx - 1, cy - 1, 0.9f, 0.9f, cloth.Light);
        // A fold across the body where the cloth sags, and the neck tied off.
        var (x, y) = ((int)cx, (int)cy);
        if (alongX) p.Put(x + 1, y, cloth.Shade);
        else p.Put(x, y + 1, cloth.Shade);
        if (alongX)
        {
            var end = (int)(cx - rx);
            p.Put(end, y, timber.Shade);
            p.Put(end - 1, y, cloth.Light);
            p.Put(end - 2, y - 1, cloth.Edge);
            p.Put(end - 2, y, cloth.Base);
            p.Put(end - 2, y + 1, cloth.Edge);
        }
        else
        {
            var end = (int)(cy - ry);
            p.Put(x, end, timber.Shade);
            p.Put(x, end - 1, cloth.Light);
            p.Put(x - 1, end - 2, cloth.Edge);
            p.Put(x, end - 2, cloth.Base);
            p.Put(x + 1, end - 2, cloth.Edge);
        }
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
            if (color.A <= 0) return;
            Canvas.Ellipse(centerX, centerY, radiusX, radiusY, color);
        }

        /// <summary>A one-pixel line in tile units, in an opaque colour.</summary>
        public void Line(float fromX, float fromY, float toX, float toY, Color color) =>
            new PixelCanvas(Image, new Rect2I(0, 0, Width, Height), Scale).Line(fromX, fromY, toX, toY, color);
    }
}
