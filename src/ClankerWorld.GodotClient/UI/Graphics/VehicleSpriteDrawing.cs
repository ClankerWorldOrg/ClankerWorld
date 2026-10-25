using Godot;
using static ClankerWorld.GodotClient.UI.SpriteDrawing;

namespace ClankerWorld.GodotClient.UI;

/// <summary>Pixel coordinates, masks and painting shared by the approved vehicle sprites.</summary>
internal static class VehicleSpriteDrawing
{
    internal enum Facing { South, East, North, West, SouthEast, NorthEast, NorthWest, SouthWest }

    internal static bool IsDiagonal(Facing facing) => facing >= Facing.SouthEast;

    internal static Color Toward(Ramp r, int step, int toward, float amount) => r[step].Lerp(r[toward], amount);

    internal readonly record struct Heading(Facing Facing)
    {
        private const float Centre = 16;
        private const float Half = 0.70710678f;

        /// <summary>The unit vector the vehicle points along, in tile pixels (y down).</summary>
        public Vector2 Forward => Facing switch
        {
            Facing.East => new Vector2(1, 0),
            Facing.West => new Vector2(-1, 0),
            Facing.South => new Vector2(0, 1),
            Facing.North => new Vector2(0, -1),
            Facing.SouthEast => new Vector2(Half, Half),
            Facing.NorthEast => new Vector2(Half, -Half),
            Facing.NorthWest => new Vector2(-Half, -Half),
            _ => new Vector2(-Half, Half),
        };

        /// <summary>The unit vector to the vehicle's right: a quarter turn clockwise from <see cref="Forward"/>.</summary>
        public Vector2 Right => new(-Forward.Y, Forward.X);

        /// <summary>A point of the frame in tile pixels.</summary>
        public Vector2 Map(float u, float v) => Facing switch
        {
            Facing.East => new Vector2(Centre + u, Centre + v),
            Facing.West => new Vector2(Centre - u, Centre - v),
            Facing.South => new Vector2(Centre - v, Centre + u),
            Facing.North => new Vector2(Centre + v, Centre - u),
            _ => new Vector2(Centre, Centre) + Forward * u + Right * v,
        };

        /// <summary>A tile pixel position in the frame (the inverse of <see cref="Map"/>).</summary>
        public Vector2 Local(float x, float y) => Facing switch
        {
            Facing.East => new Vector2(x - Centre, y - Centre),
            Facing.West => new Vector2(Centre - x, Centre - y),
            Facing.South => new Vector2(y - Centre, Centre - x),
            Facing.North => new Vector2(Centre - y, x - Centre),
            _ => new Vector2(new Vector2(x - Centre, y - Centre).Dot(Forward), new Vector2(x - Centre, y - Centre).Dot(Right)),
        };

        /// <summary>
        /// The diagonal lattice of a 45° facing: steps along and across the
        /// vehicle in units of half a pixel's diagonal, so a pixel centre always
        /// lands on whole numbers and seams and bands fall on clean 45° stairs.
        /// </summary>
        public (int Along, int Across) Lattice(int x, int y)
        {
            var local = Local(x + 0.5f, y + 0.5f);
            return ((int)MathF.Round(local.X * 1.41421356f), (int)MathF.Round(local.Y * 1.41421356f));
        }

        /// <summary>A direction of the frame in tile pixels.</summary>
        public Vector2 Direction(float u, float v) => Map(u, v) - Map(0, 0);

        /// <summary>The pixel rectangle covering [u, u + du) × [v, v + dv) of the frame.</summary>
        public Rect2I Box(float u, float v, float du, float dv)
        {
            var a = Map(u, v);
            var b = Map(u + du, v + dv);
            var left = (int)MathF.Round(MathF.Min(a.X, b.X));
            var top = (int)MathF.Round(MathF.Min(a.Y, b.Y));
            return new Rect2I(left, top, (int)MathF.Round(MathF.Max(a.X, b.X)) - left, (int)MathF.Round(MathF.Max(a.Y, b.Y)) - top);
        }
    }

    internal sealed class Mask
    {
        private const int Size = 32;
        private readonly bool[] on = new bool[Size * Size];

        public Mask(Func<int, int, bool> covers)
        {
            for (var y = 0; y < Size; y++)
                for (var x = 0; x < Size; x++)
                    on[y * Size + x] = covers(x, y);
        }

        /// <summary>The pixels of a part written in the vehicle's frame (u forward, v to its right).</summary>
        public static Mask Of(Heading f, Func<float, float, bool> inside) => new((x, y) =>
        {
            var local = f.Local(x + 0.5f, y + 0.5f);
            return inside(local.X, local.Y);
        });

        public bool this[int x, int y] => x >= 0 && y >= 0 && x < Size && y < Size && on[y * Size + x];

        /// <summary>Every covered pixel, row by row.</summary>
        public IEnumerable<(int X, int Y)> Pixels()
        {
            for (var y = 0; y < Size; y++)
                for (var x = 0; x < Size; x++)
                    if (on[y * Size + x]) yield return (x, y);
        }

        /// <summary>Whether a covered pixel has an open pixel above, below or beside it.</summary>
        public bool Rim(int x, int y) => this[x, y] && (!this[x - 1, y] || !this[x + 1, y] || !this[x, y - 1] || !this[x, y + 1]);

        /// <summary>The part less its outer ring.</summary>
        public Mask Inner() => new((x, y) => this[x, y] && !Rim(x, y));

        /// <summary>The part and another together.</summary>
        public Mask Or(Mask other) => new((x, y) => this[x, y] || other[x, y]);

        /// <summary>
        /// How squarely a ring pixel faces the north-west light, from its open
        /// sides: 1 facing it, −1 facing away, 0 side-on (or not on the ring).
        /// </summary>
        public float Light(int x, int y)
        {
            float nx = 0, ny = 0;
            if (!this[x - 1, y]) nx -= 1;
            if (!this[x + 1, y]) nx += 1;
            if (!this[x, y - 1]) ny -= 1;
            if (!this[x, y + 1]) ny += 1;
            var length = MathF.Sqrt(nx * nx + ny * ny);
            return length == 0 ? 0 : -(nx + ny) / (length * 1.41421356f);
        }

        /// <summary>
        /// Whether a ring pixel takes the lit tone of a two-tone part: it faces
        /// the light, or it is side-on and open to the north (the way the
        /// cardinal sprites light the north row of an east-west pole).
        /// </summary>
        public bool Lit(int x, int y)
        {
            var light = Light(x, y);
            return light > 0.3f || (light > -0.3f && !this[x, y - 1]);
        }
    }

}
