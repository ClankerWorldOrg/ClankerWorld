using Godot;
using static ClankerWorld.GodotClient.UI.SpriteDrawing;

namespace ClankerWorld.GodotClient.UI;

/// <summary>How a building in an abandoned Town has weathered.</summary>
public enum BuildingNeglect : byte
{
    /// <summary>Lived in, or in a Town that is not abandoned.</summary>
    None,
    /// <summary>Look A: faded, mossy and weedy, with the door boarded up.</summary>
    Neglected,
    /// <summary>Look B, after a full season abandoned: also a hole in the roof, fallen planks and a sapling.</summary>
    FallingApart,
}

public static partial class BuildingSprites
{
    private static readonly Dictionary<(BuildingKind Kind, int Width, int Height, int Tile, BuildingDoor Door, int Stage), ImageTexture> SiteCache = [];

    /// <summary>
    /// A building under construction at stage 1, 2 or 3 (October 7 art
    /// review): a cleared, staked-out site with materials, then the footing,
    /// sill, posts and half a floor, then the walls with the roof half on from
    /// the back. Drawn at 32 px over the building's own approved picture and
    /// halved for 16 px tiles, as the Port is.
    /// </summary>
    public static ImageTexture ConstructionTexture(BuildingKind kind, int width, int height, int tilePixels, BuildingDoor door, int stage)
    {
        width = Math.Clamp(width, 1, 8);
        height = Math.Clamp(height, 1, 8);
        stage = Math.Clamp(stage, 1, 3);
        var key = (kind, width, height, tilePixels, door, stage);
        if (SiteCache.TryGetValue(key, out var cached)) return cached;
        var texture = ImageTexture.CreateFromImage(RenderConstruction(kind, width, height, tilePixels, door, stage));
        SiteCache[key] = texture;
        return texture;
    }

    public static Image RenderConstruction(BuildingKind kind, int width, int height, int tilePixels, BuildingDoor door, int stage)
    {
        width = Math.Clamp(width, 1, 8);
        height = Math.Clamp(height, 1, 8);
        var site = Construction.Draw(kind, width, height, door, Math.Clamp(stage, 1, 3));
        return tilePixels == 32 ? site : ResizeArt(site, width * tilePixels, height * tilePixels);
    }

    /// <summary>The flat colour of a construction site at overview zoom: cleared earth, or timber for a pier over water.</summary>
    public static Color SiteColor(BuildingKind kind) => kind == BuildingKind.Port ? new("8A6440") : new("B99A6B");

    private static readonly Dictionary<(int Tile, int Stage), ImageTexture> LanternSiteCache = [];

    /// <summary>
    /// The materials on a street lantern's own tile while it is built: a stone
    /// heap and a log at stage 1, the log alone at stage 2, nothing once the
    /// fitting stands. The fitting itself goes up on the Road edge with the
    /// finished lamp (see <see cref="StreetLanternSite"/>).
    /// </summary>
    public static ImageTexture? LanternSiteTexture(int tilePixels, int stage)
    {
        if (stage >= 3) return null;
        stage = Math.Max(1, stage);
        if (LanternSiteCache.TryGetValue((tilePixels, stage), out var cached)) return cached;
        var image = Construction.LanternMaterials(stage);
        if (tilePixels != 32) image = ResizeArt(image, tilePixels, tilePixels);
        var texture = ImageTexture.CreateFromImage(image);
        LanternSiteCache[(tilePixels, stage)] = texture;
        return texture;
    }

    /// <summary>What part of a structure is built or weathers.</summary>
    private enum Shape { Building, Fence, Pier, Bridge, Lantern }

    /// <summary>The finished 32 px art and its structure mask, excluding yards and doorsteps on roofed buildings.</summary>
    private static (Image Finished, bool[,] Mask, int Width, int Height, float DoorMiddle, Shape Kind, bool Roofed)
        PrepareStructure(BuildingKind kind, int tilesWide, int tilesHigh, BuildingDoor door)
    {
        var finished = Render(kind, tilesWide, tilesHigh, 32, door);
        var w = tilesWide * 32;
        var h = tilesHigh * 32;
        var mask = new bool[w, h];
        var plan = Plan(kind, tilesWide, tilesHigh, door);
        var roofed = kind != BuildingKind.Port && ApprovedArt.PlanFor(kind, tilesWide, tilesHigh, door, 32) is not null;
        var shape = kind switch
        {
            BuildingKind.Port => Shape.Pier,
            BuildingKind.AnimalYard => Shape.Fence,
            _ => Shape.Building,
        };
        // A yard weathers only its fence, five units in from the footprint's edge.
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                mask[x, y] = roofed
                    ? Inside(plan.Roof, x, y) || plan.Wing is { } wing && Inside(wing, x, y)
                    : finished.GetPixel(x, y).A > 0.9f && (shape != Shape.Fence || x < 6 || y < 6 || x >= w - 6 || y >= h - 6);
        return (finished, mask, w, h, plan.DoorMiddle, shape, roofed);
    }

    private static bool Inside(Rect2 area, int x, int y) =>
        x >= (int)MathF.Round(area.Position.X) && x < (int)MathF.Round(area.End.X) &&
        y >= (int)MathF.Round(area.Position.Y) && y < (int)MathF.Round(area.End.Y);

    /// <summary>The approved construction stages, copied from <c>tools/ArtPreview/Proposed/BuildingStates.cs</c>.</summary>
    private static class Construction
    {
        private static readonly Color SiteString = new("E8DCC0");
        private static readonly Color SiteSoot = new("2A2622");
        private static readonly Color SiteShadow = new(0.05f, 0.08f, 0.05f, 0.28f);

        /// <summary>One site: the finished picture, the structure inside it, which way it faces and which side is built first.</summary>
        private sealed record Look(Image Finished, bool[,] Mask, int Width, int Height, DoorSide Door, float DoorMiddle, Vector2 Back, Shape Kind, bool Doorway);

        public static Image Draw(BuildingKind kind, int tilesWide, int tilesHigh, BuildingDoor door, int stage)
        {
            var (finished, mask, w, h, doorMiddle, shape, roofed) = PrepareStructure(kind, tilesWide, tilesHigh, door);
            // Work starts at the back, so the open frame faces the Road; a pier is built out from the land.
            var side = shape == Shape.Pier
                ? tilesHigh >= tilesWide
                    ? door.Side == DoorSide.South ? DoorSide.South : DoorSide.North
                    : door.Side == DoorSide.West ? DoorSide.West : DoorSide.East
                : door.Side;
            var back = side switch
            {
                DoorSide.North => new Vector2(0, 1),
                DoorSide.East => new Vector2(-1, 0),
                DoorSide.West => new Vector2(1, 0),
                _ => new Vector2(0, -1),
            };
            if (shape == Shape.Pier) back = -back;
            var middle = kind == BuildingKind.Silo ? tilesWide * 16f : doorMiddle;
            return Apply(new Look(finished, mask, w, h, side, middle, back, shape, roofed || kind == BuildingKind.Silo), stage);
        }

        /// <summary>One construction stage on its own, transparent outside the site.</summary>
        private static Image Apply(Look s, int stage)
        {
            var image = Image.CreateEmpty(s.Width, s.Height, false, Image.Format.Rgba8);
            image.Fill(Colors.Transparent);
            var mask = s.Mask;
            var b = Bounds(mask);
            var w = s.Width;
            var h = s.Height;
            var onWater = s.Kind is Shape.Pier;

            // Cleared earth over the site and a little round it (never over water).
            if (!onWater)
                for (var y = 0; y < h; y++)
                    for (var x = 0; x < w; x++)
                    {
                        var near = In(mask, x, y) ? 0 : Near(mask, x, y, 2);
                        if (near > 2 || (near == 2 && Hash(x, y, 3) % 3 == 0)) continue;
                        if (s.Kind == Shape.Fence && !In(mask, x, y) && near > 1) continue;
                        var n = Hash(x, y, 5) % 19;
                        Blend(image, x, y, n == 0 ? Dirt.Shade : n == 1 ? Dirt.Light : Dirt.Base);
                    }

            switch (s.Kind)
            {
                case Shape.Fence:
                    FenceStage(image, s, b, stage);
                    break;
                default:
                    if (stage == 1) StakeOut(image, s, b, onWater);
                    else Frame(image, s, b, stage);
                    break;
            }
            Materials(image, s, b, stage);
            return image;
        }

        /// <summary>Stage 1: corner stakes with a string line between them (piles already driven over water).</summary>
        private static void StakeOut(Image image, Look s, Rect2I b, bool onWater)
        {
            if (onWater)
            {
                Piles(image, s, b);
                return;
            }
            int left = b.Position.X, top = b.Position.Y, right = b.End.X - 1, bottom = b.End.Y - 1;
            for (var x = left; x <= right; x++) { Blend(image, x, top, SiteString); Blend(image, x, bottom, SiteString); }
            for (var y = top; y <= bottom; y++) { Blend(image, left, y, SiteString); Blend(image, right, y, SiteString); }
            foreach (var (x, y) in new[] { (left, top), (right, top), (left, bottom), (right, bottom) }) Stake(image, x - 1, y - 1);
        }

        private static void Stake(Image image, int x, int y)
        {
            Rect(image, x + 1, y + 1, 2, 2, SiteShadow);
            Rect(image, x, y, 2, 2, Timber.Edge);
            Blend(image, x, y, Timber.Highlight);
        }

        /// <summary>Piles driven along both edges of a pier every six pixels, following its outline, each with a ripple; none on land.</summary>
        private static void Piles(Image image, Look s, Rect2I b)
        {
            var lengthwise = b.Size.Y >= b.Size.X;
            var ripple = new Color(1, 1, 1, 0.35f);
            void Pile(int x, int y)
            {
                if (OnLand(s, x, y)) return;
                Blend(image, x - 1, y, ripple); Blend(image, x + 2, y + 1, ripple);
                Rect(image, x, y, 2, 2, Timber.Edge);
                Blend(image, x, y, Timber.Light);
            }
            for (var a = (lengthwise ? b.Position.Y : b.Position.X) + 2; a < (lengthwise ? b.End.Y : b.End.X) - 1; a += 6)
            {
                int first = -1, last = -1;
                for (var c = lengthwise ? b.Position.X : b.Position.Y; c < (lengthwise ? b.End.X : b.End.Y); c++)
                {
                    if (!(lengthwise ? In(s.Mask, c, a) : In(s.Mask, a, c))) continue;
                    if (first < 0) first = c;
                    last = c;
                }
                if (first < 0) continue;
                if (lengthwise) { Pile(first + 1, a); Pile(last - 2, a); }
                else { Pile(a, first + 1); Pile(a, last - 2); }
            }
        }

        /// <summary>
        /// Stages 2 and 3 for buildings and piers: a footing and sill
        /// round the outline with posts, the door gap open, floorboards; at stage 3
        /// the walls stand and the finished roof covers the back of the structure,
        /// with open rafters over the rest and a ladder at the front.
        /// </summary>
        private static void Frame(Image image, Look s, Rect2I b, int stage)
        {
            var mask = s.Mask;
            var w = mask.GetLength(0);
            var h = mask.GetLength(1);
            var timberOnly = s.Kind is Shape.Pier;
            var horizontalDoor = s.Door is DoorSide.South or DoorSide.North;
            bool InDoorGap(int x, int y)
            {
                if (!s.Doorway) return false;
                var along = horizontalDoor ? x + 0.5f : y + 0.5f;
                var onSide = s.Door switch
                {
                    DoorSide.North => y < b.Position.Y + 3,
                    DoorSide.East => x > b.End.X - 4,
                    DoorSide.West => x < b.Position.X + 3,
                    _ => y > b.End.Y - 4,
                };
                return onSide && MathF.Abs(along - s.DoorMiddle) < 3.5f;
            }
            if (timberOnly && stage == 2) Piles(image, s, b);
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    if (!mask[x, y]) continue;
                    var depth = Depth(mask, x, y, 3);
                    var t = FromBack(b, s.Back, x, y);
                    if (depth <= 2 && !timberOnly)
                    {
                        if (InDoorGap(x, y)) continue;
                        // Stage 2: stone footing, lit on its north-west rim; stage 3: the walls' timber tops.
                        var litRim = depth == 1 && (!In(mask, x - 1, y) || !In(mask, x, y - 1));
                        var shadeRim = depth == 1 && (!In(mask, x + 1, y) || !In(mask, x, y + 1));
                        var ramp = stage == 2 ? Rock : Timber;
                        Blend(image, x, y, litRim ? ramp.Light : shadeRim ? ramp.Shade : ramp.Base);
                        continue;
                    }
                    if (depth == 3 && !timberOnly)
                    {
                        if (!InDoorGap(x, y)) Blend(image, x, y, stage == 2 ? Timber.Base : Timber.Shade);
                        continue;
                    }
                    // Inside: floorboards across the structure, laid from the back.
                    var planked = stage == 3 || t < 0.6f;
                    var row = horizontalDoor || timberOnly && b.Size.Y >= b.Size.X ? y : x;
                    if (planked)
                    {
                        var board = row % 3;
                        Blend(image, x, y, board == 2 ? Timber.Shade : (row / 3 + (horizontalDoor ? x : y) / 9) % 2 == 0 ? Timber.Light : Timber.Base);
                    }
                    else if (timberOnly) { if (depth <= 2) Blend(image, x, y, Timber.Shade); }
                    else if (row % 6 == 0) Blend(image, x, y, Timber.Shade);
                }
            if (!timberOnly) Posts(image, s, b, InDoorGap);
            if (stage < 3) return;

            // The finished roof over the back of the structure, its leading edge stepped course by course.
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    if (!mask[x, y]) continue;
                    var t = FromBack(b, s.Back, x, y);
                    var step = ((horizontalDoor ? x : y) / 4 % 2) * 0.05f;
                    if (t < 0.52f + step)
                    {
                        var c = s.Finished.GetPixel(x, y);
                        if (c.A > 0) Blend(image, x, y, c);
                    }
                    else if (!timberOnly && Depth(mask, x, y, 3) > 3)
                    {
                        // Open rafters over the dark interior.
                        var across = horizontalDoor ? x : y;
                        Blend(image, x, y, across % 4 == 0 ? Timber.Base : across % 4 == 1 ? Timber.Light : new Color(SiteSoot, 0.55f));
                    }
                }
            if (!timberOnly) Ladder(image, s, b);
        }

        /// <summary>Posts at the outline's corners and every ten pixels along it, kept out of the door gap.</summary>
        private static void Posts(Image image, Look s, Rect2I b, Func<int, int, bool> inDoorGap)
        {
            var mask = s.Mask;
            void Post(int x, int y)
            {
                if (!In(mask, x, y) || inDoorGap(x, y) || inDoorGap(x + 2, y + 2)) return;
                Rect(image, x + 1, y + 1, 3, 3, SiteShadow);
                Rect(image, x, y, 3, 3, Timber.Edge);
                Rect(image, x, y, 2, 2, Timber.Base);
                Blend(image, x, y, Timber.Highlight);
            }
            for (var x = b.Position.X; x <= b.End.X - 3; x += 10) { Post(x, b.Position.Y); Post(x, b.End.Y - 3); }
            for (var y = b.Position.Y + 10; y <= b.End.Y - 3; y += 10) { Post(b.Position.X, y); Post(b.End.X - 3, y); }
            Post(b.End.X - 3, b.Position.Y);
            Post(b.End.X - 3, b.End.Y - 3);
        }

        /// <summary>A short ladder leaning on the front wall beside the door.</summary>
        private static void Ladder(Image image, Look s, Rect2I b)
        {
            var horizontal = s.Door is DoorSide.South or DoorSide.North;
            var x = (int)MathF.Round(horizontal ? Math.Clamp(s.DoorMiddle + 7, b.Position.X + 2, b.End.X - 6)
                : s.Door == DoorSide.West ? b.Position.X - 3 : b.End.X - 6);
            var y = horizontal ? s.Door == DoorSide.North ? b.Position.Y - 3 : b.End.Y - 6
                : (int)MathF.Round(Math.Clamp(s.DoorMiddle + 7, b.Position.Y + 2, b.End.Y - 6));
            for (var k = 0; k < 8; k++)
            {
                Blend(image, x, y + k, Timber.Light);
                Blend(image, x + 3, y + k, Timber.Base);
                Blend(image, x + 4, y + k, SiteShadow);
                if (k % 2 == 1) { Blend(image, x + 1, y + k, Timber.Highlight); Blend(image, x + 2, y + k, Timber.Light); }
            }
        }

        /// <summary>Fences: stakes and string, then posts and rails up the back half, then the whole fence before the yard is worn in.</summary>
        private static void FenceStage(Image image, Look s, Rect2I b, int stage)
        {
            if (stage == 1)
            {
                StakeOut(image, s, b, false);
                return;
            }
            var mask = s.Mask;
            for (var y = 0; y < mask.GetLength(1); y++)
                for (var x = 0; x < mask.GetLength(0); x++)
                {
                    if (!mask[x, y]) continue;
                    if (stage == 2 && FromBack(b, s.Back, x, y) > 0.5f) continue;
                    var c = s.Finished.GetPixel(x, y);
                    if (c.A > 0) Blend(image, x, y, c);
                }
            if (stage == 2) StakeOut(image, s, b, false);
        }

        /// <summary>A log stack, a stone heap and a plank stack beside the site, shrinking as the work goes on.</summary>
        private static void Materials(Image image, Look s, Rect2I b, int stage)
        {
            var w = s.Width;
            var h = s.Height;
            if (s.Kind == Shape.Pier)
            {
                // Over water the timber waits on the bank.
                var (x, y) = s.Door switch
                {
                    DoorSide.North => (2, 20),
                    DoorSide.East => (w - 30, h - 12),
                    _ => (2, h - 12),
                };
                if (stage < 3) LogStack(image, x, y, stage == 1 ? 3 : 2);
                if (stage == 1) StoneHeap(image, x + 18, y + 4);
                return;
            }
            // The first corner of the footprint with room clear of the site, else the back corner over it.
            var candidates = new (int X, int Y)[] { (w - 16, h - 10), (2, h - 10), (w - 16, 2), (2, 2), (w / 2 - 7, h - 10) };
            (int X, int Y) spot = candidates.FirstOrDefault(c => Clear(s.Mask, c.X, c.Y, 14, 8), (X: -1, Y: -1));
            if (spot.X < 0)
            {
                if (stage > 1) return;
                spot = (b.End.X - 16, b.Position.Y + 3);
            }
            var logs = stage switch { 1 => 3, 2 => 2, _ => 1 };
            LogStack(image, spot.X, spot.Y, logs);
            if (stage == 1) StoneHeap(image, spot.X + 10, spot.Y + 4);
        }

        /// <summary>A street lantern's materials on its own 32 px tile, placed as in the approved lantern stages.</summary>
        public static Image LanternMaterials(int stage)
        {
            var image = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
            image.Fill(Colors.Transparent);
            if (stage == 1) StoneHeap(image, 6, 8);
            if (stage < 3) LogStack(image, 18, 6, 1);
            return image;
        }

        private static bool Clear(bool[,] mask, int x0, int y0, int w, int h)
        {
            if (x0 < 1 || y0 < 1 || x0 + w > mask.GetLength(0) - 1 || y0 + h > mask.GetLength(1) - 1) return false;
            for (var y = y0 - 2; y < y0 + h + 2; y++)
                for (var x = x0 - 2; x < x0 + w + 2; x++)
                    if (In(mask, x, y)) return false;
            return true;
        }

        private static void LogStack(Image image, int x, int y, int logs)
        {
            for (var k = logs - 1; k >= 0; k--)
            {
                var ly = y + k * 2;
                Rect(image, x + 1, ly + 3, 11, 1, SiteShadow);
                Rect(image, x, ly, 11, 3, Timber.Edge);
                Rect(image, x, ly, 10, 2, Timber.Base);
                Rect(image, x, ly, 10, 1, Timber.Light);
                Rect(image, x + 9, ly, 2, 2, Timber.Highlight);
                Blend(image, x + 10, ly + 1, Timber.Shade);
            }
        }

        private static void StoneHeap(Image image, int x, int y)
        {
            void Stone(int cx, int cy, int r)
            {
                for (var dy = -r; dy <= r; dy++)
                    for (var dx = -r; dx <= r; dx++)
                    {
                        var d = dx * dx + dy * dy;
                        if (d > r * r + 1) continue;
                        Blend(image, cx + dx + 1, cy + dy + 1, SiteShadow);
                    }
                for (var dy = -r; dy <= r; dy++)
                    for (var dx = -r; dx <= r; dx++)
                    {
                        var d = dx * dx + dy * dy;
                        if (d > r * r + 1) continue;
                        var rim = d > (r - 1) * (r - 1);
                        Blend(image, cx + dx, cy + dy, rim ? (dx + dy < 0 ? Rock.Light : Rock.Edge) : dx + dy < 0 ? Rock.Highlight : Rock.Base);
                    }
            }
            Stone(x + 2, y + 2, 2);
            Stone(x + 6, y + 3, 2);
            Stone(x + 4, y, 2);
        }

        /// <summary>A pier's landward tile row, on its door side, where no piles are driven.</summary>
        private static bool OnLand(Look s, int x, int y) => s.Kind == Shape.Pier && s.Door switch
        {
            DoorSide.North => y < 32,
            DoorSide.East => x >= s.Width - 32,
            DoorSide.West => x < 32,
            _ => y >= s.Height - 32,
        };

        /// <summary>A pixel's place from the back of the structure (0) to its front (1).</summary>
        private static float FromBack(Rect2I b, Vector2 back, int x, int y)
        {
            if (back.Y < 0) return (y - b.Position.Y) / (float)b.Size.Y;
            if (back.Y > 0) return (b.End.Y - 1 - y) / (float)b.Size.Y;
            if (back.X < 0) return (x - b.Position.X) / (float)b.Size.X;
            return (b.End.X - 1 - x) / (float)b.Size.X;
        }

        private static void Rect(Image image, int x, int y, int w, int h, Color color)
        {
            for (var py = y; py < y + h; py++)
                for (var px = x; px < x + w; px++)
                    Blend(image, px, py, color);
        }
    }

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
    /// <summary>
    /// One pixel of a building in an abandoned Town, faded greyer and a little
    /// darker as in the approved abandoned looks: the structure itself more
    /// than its yard and doorstep, and more once it is falling apart. Lantern
    /// fittings on a weathered building fade the same way.
    /// </summary>
    public static Color Weathered(Color color, BuildingNeglect neglect, bool structure = true)
    {
        if (neglect == BuildingNeglect.None) return color;
        var ruin = neglect == BuildingNeglect.FallingApart;
        var amount = structure ? (ruin ? 0.42f : 0.26f) : 0.16f;
        var grey = new Color(color.Luminance, color.Luminance, color.Luminance, color.A);
        return PixelArt.Snap(color.Lerp(grey, amount).Darkened(structure ? (ruin ? 0.14f : 0.08f) : 0.05f) with { A = color.A });
    }

    /// <summary>Keep the subject-name seed used by the approved abandoned proposal.</summary>
    public static int NeglectSalt(BuildingKind kind, int tilesWide, int tilesHigh)
    {
        var name = kind switch
        {
            BuildingKind.TailorShop => "tailor",
            BuildingKind.MarketStall => "stall",
            BuildingKind.AnimalYard => "yard",
            _ => kind.ToString().ToLowerInvariant(),
        };
        return $"{name}.{tilesWide}x{tilesHigh}".Length * 131;
    }

    private static readonly Dictionary<(bool EastWest, int Deck, int Tile, bool Ruin), ImageTexture> NeglectedBridgeCache = [];

    /// <summary>
    /// A whole bridge in an abandoned Town, from the bank tile before its deck
    /// to the bank tile after it, weathered as the approved abandoned looks:
    /// faded, mossy and missing a few boards, and losing whole planks once
    /// the Town has stood empty for a full season.
    /// </summary>
    public static ImageTexture NeglectedBridgeTexture(bool eastWest, int deckTiles, int tilePixels, BuildingNeglect neglect)
    {
        deckTiles = Math.Clamp(deckTiles, 1, 32);
        var ruin = neglect == BuildingNeglect.FallingApart;
        var key = (eastWest, deckTiles, tilePixels, ruin);
        if (NeglectedBridgeCache.TryGetValue(key, out var cached)) return cached;
        var texture = ImageTexture.CreateFromImage(RenderNeglectedBridge(eastWest, deckTiles, tilePixels, neglect));
        NeglectedBridgeCache[key] = texture;
        return texture;
    }

    public static Image RenderNeglectedBridge(bool eastWest, int deckTiles, int tilePixels, BuildingNeglect neglect)
    {
        deckTiles = Math.Clamp(deckTiles, 1, 32);
        var image = Neglect.Bridge(eastWest, deckTiles, neglect == BuildingNeglect.FallingApart);
        return tilePixels == 32 ? image : ResizeArt(image, image.GetWidth() / 32 * tilePixels, image.GetHeight() / 32 * tilePixels);
    }

    /// <summary>
    /// A street lantern's fitting in an abandoned Town, faded and mossy with
    /// weeds round its foot, in a 32 px frame whose post is at
    /// <see cref="NeglectedLanternPost"/>.
    /// </summary>
    public static Image NeglectedLantern(LanternStyle style, Vector2 inward, BuildingNeglect neglect, int tilePixels = 32)
    {
        var image = Neglect.Lantern(style, inward, neglect == BuildingNeglect.FallingApart);
        return tilePixels == 32 ? image : ResizeArt(image, tilePixels, tilePixels);
    }

    /// <summary>Where the post stands in <see cref="NeglectedLantern"/>'s frame.</summary>
    public static Vector2 NeglectedLanternPost(LanternStyle style, Vector2 inward) => Neglect.LanternFramePost(style, inward);

    // The approved preview samples top-left pixels; Godot's nearest resize
    // samples their centres and changes the mid-zoom picture.
    private static Image ResizeArt(Image image, int width, int height)
    {
        var sourceWidth = image.GetWidth();
        var sourceHeight = image.GetHeight();
        var source = image.GetData();
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var sourceX = Math.Min(sourceWidth - 1, x * sourceWidth / width);
                var sourceY = Math.Min(sourceHeight - 1, y * sourceHeight / height);
                Array.Copy(source, (sourceY * sourceWidth + sourceX) * 4, pixels, (y * width + x) * 4, 4);
            }
        return Image.CreateFromData(width, height, false, Image.Format.Rgba8, pixels);
    }

    /// <summary>
    /// The abandoned looks approved in the October 7 art review, copied from
    /// <c>tools/ArtPreview/Proposed/BuildingStates.cs</c>. They are drawn
    /// over a building's own 32 px picture, so they always match its art.
    /// </summary>
    private static class Neglect
    {
        private static readonly Color NeglectSoot = new("2A2622");
        private static readonly Color NeglectFlower = new("F2CC5E");
        private static readonly Color NeglectShadow = new(0.05f, 0.08f, 0.05f, 0.28f);

        /// <summary>One building's picture and the structure inside it that weathers.</summary>
        private sealed record Look(Image Finished, bool[,] Mask, int Width, int Height, DoorSide Door, float DoorMiddle, Shape Kind, bool Doorway, int Salt);

        /// <summary>The weathered 32 px picture of one building.</summary>
        public static Image Draw(BuildingKind kind, int tilesWide, int tilesHigh, BuildingDoor door, bool ruin)
        {
            var (finished, mask, w, h, doorMiddle, shape, roofed) = PrepareStructure(kind, tilesWide, tilesHigh, door);
            var doorway = roofed || kind == BuildingKind.Silo;
            return Apply(new Look(finished, mask, w, h, door.Side, doorMiddle, shape, doorway, NeglectSalt(kind, tilesWide, tilesHigh)), ruin);
        }

        /// <summary>
        /// A bridge of <paramref name="deckTiles"/> plank tiles with a bank tile
        /// at each end, where its weeds grow, weathered as one picture.
        /// </summary>
        public static Image Bridge(bool eastWest, int deckTiles, bool ruin)
        {
            var length = deckTiles + 2;
            var (w, h) = eastWest ? (length * 32, 32) : (32, length * 32);
            var finished = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
            finished.Fill(Colors.Transparent);
            var deck = RoadSprites.BridgeDeck(eastWest, 32);
            for (var tile = 1; tile <= deckTiles; tile++)
                finished.BlitRect(deck, new Rect2I(0, 0, 32, 32), eastWest ? new Vector2I(tile * 32, 0) : new Vector2I(0, tile * 32));
            var mask = new bool[w, h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    mask[x, y] = finished.GetPixel(x, y).A > 0.9f;
            return Apply(new Look(finished, mask, w, h, eastWest ? DoorSide.West : DoorSide.North, 16, Shape.Bridge, false, $"bridge.{deckTiles}".Length * 131), ruin);
        }

        /// <summary>
        /// A street lantern's daytime fitting weathered, in a 32 px frame with
        /// its post at <see cref="LanternFramePost"/>, as the lanterns were
        /// drawn for the approved abandoned looks.
        /// </summary>
        public static Image Lantern(LanternStyle style, Vector2 inward, bool ruin)
        {
            var finished = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
            finished.Fill(Colors.Transparent);
            foreach (var cell in NightLightShapes.StreetLantern(style, LanternFramePost(style, inward), inward, 0f, 0f, 0))
            {
                if (cell.Kind != LightCellKind.Paint) continue;
                for (var y = (int)MathF.Floor(cell.Area.Position.Y); y < (int)MathF.Ceiling(cell.Area.End.Y); y++)
                    for (var x = (int)MathF.Floor(cell.Area.Position.X); x < (int)MathF.Ceiling(cell.Area.End.X); x++)
                        Blend(finished, x, y, cell.Color);
            }
            var mask = new bool[32, 32];
            for (var y = 0; y < 32; y++)
                for (var x = 0; x < 32; x++)
                    mask[x, y] = finished.GetPixel(x, y).A > 0.9f;
            var salt = (style == LanternStyle.Stone ? "lantern.stone" : "lantern.hanging").Length * 131;
            return Apply(new Look(finished, mask, 32, 32, DoorSide.South, 16, Shape.Lantern, false, salt), ruin);
        }

        /// <summary>
        /// Where the post stands in a lantern's 32 px frame: a stone lamp in
        /// the middle, a hanging lantern's post on the side away from its arm
        /// so the lantern hangs near the middle.
        /// </summary>
        public static Vector2 LanternFramePost(LanternStyle style, Vector2 inward) =>
            style == LanternStyle.Stone ? new(16, 17)
            : inward.X > 0 ? new(9, 18)
            : inward.X < 0 ? new(23, 18)
            : inward.Y > 0 ? new(16, 9)
            : new(16, 25);

        /// <summary>
        /// The neglected (A) or falling-apart (B) look over the building's own
        /// picture, transparent outside the structure and its weeds. Blended
        /// colours are snapped to the 8-bit steps Godot stores.
        /// </summary>
        private static Image Apply(Look s, bool ruin)
        {
            var mask = s.Mask;
            var b = Bounds(mask);
            var w = s.Width;
            var h = s.Height;
            var art = (Image)s.Finished.Duplicate();
            var salt = s.Salt + (ruin ? 7 : 0);
            var roofed = s.Kind == Shape.Building;

            // Faded, greyer and a little darker; the structure more than its yard and doorstep.
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var c = art.GetPixel(x, y);
                    if (c.A <= 0) continue;
                    art.SetPixel(x, y, Weathered(c, ruin ? BuildingNeglect.FallingApart : BuildingNeglect.Neglected, mask[x, y]));
                }

            // Moss in clumps on the structure, more on the shaded south-east half.
            var clumps = Math.Max(2, b.Size.X * b.Size.Y / (ruin ? 70 : 140));
            for (var k = 0; k < clumps; k++)
            {
                var cx = b.Position.X + 2 + (int)(Hash(k, 1, salt) % (uint)Math.Max(1, b.Size.X - 4));
                var cy = b.Position.Y + 2 + (int)(Hash(k, 2, salt) % (uint)Math.Max(1, b.Size.Y - 4));
                if (Hash(k, 3, salt) % 3 == 0 && cx + cy < b.Position.X + b.Position.Y + (b.Size.X + b.Size.Y) / 2) continue;
                for (var dy = -1; dy <= 1; dy++)
                    for (var dx = -2; dx <= 1; dx++)
                    {
                        if (!In(mask, cx + dx, cy + dy) || Hash(cx + dx, cy + dy, salt) % 3 == 0) continue;
                        var c = art.GetPixel(cx + dx, cy + dy);
                        art.SetPixel(cx + dx, cy + dy, PixelArt.Snap(c.Lerp((dx + dy) < 0 ? OrchardCanopy.Base : OrchardCanopy.Shade, 0.75f) with { A = c.A }));
                    }
            }

            // A few missing shingles or boards: dark gaps on the structure, away from its rim.
            var gaps = Math.Max(2, b.Size.X * b.Size.Y / (ruin ? 120 : 260));
            for (var k = 0; k < gaps; k++)
            {
                var gx = b.Position.X + 3 + (int)(Hash(k, 5, salt) % (uint)Math.Max(1, b.Size.X - 6));
                var gy = b.Position.Y + 3 + (int)(Hash(k, 6, salt) % (uint)Math.Max(1, b.Size.Y - 6));
                if (!In(mask, gx, gy) || !In(mask, gx + 1, gy)) continue;
                var color = s.Kind is Shape.Pier or Shape.Bridge ? Colors.Transparent : NeglectSoot;
                art.SetPixel(gx, gy, color);
                if (Hash(k, 7, salt) % 2 == 0) art.SetPixel(gx + 1, gy, color);
            }

            if (ruin)
            {
                if (roofed) RoofHole(art, s, b, salt);
                else Breaks(art, s, b, salt);
            }

            if (ruin && roofed) FallenPlanks(art, s, b, salt);
            Weeds(art, s, b, ruin, salt);
            if (s.Doorway && roofed) BoardedDoor(art, s, b);
            if (ruin && s.Kind is Shape.Building or Shape.Fence) Sapling(art, s, b, salt);
            return art;
        }

        /// <summary>B: a ragged hole in the front half of the roof, broken rafters over the dark inside.</summary>
        private static void RoofHole(Image art, Look s, Rect2I b, int salt)
        {
            var cx = b.Position.X + b.Size.X * 0.64f;
            var cy = b.Position.Y + b.Size.Y * 0.6f;
            var rx = MathF.Max(4.5f, b.Size.X * 0.22f);
            var ry = MathF.Max(4f, b.Size.Y * 0.2f);
            var horizontal = s.Door is DoorSide.South or DoorSide.North;
            for (var y = b.Position.Y; y < b.End.Y; y++)
                for (var x = b.Position.X; x < b.End.X; x++)
                {
                    if (!In(s.Mask, x, y) || Depth(s.Mask, x, y, 3) <= 3) continue;
                    var dx = (x + 0.5f - cx) / rx;
                    var dy = (y + 0.5f - cy) / ry;
                    var d = dx * dx + dy * dy;
                    var ragged = 1f - (Hash(x, y, salt + 9) % 4) * 0.09f;
                    if (d > ragged) continue;
                    var across = horizontal ? x : y;
                    var broken = Hash(across, (horizontal ? y : x) / 3, salt) % 4 == 0;
                    var color = d > ragged - 0.18f ? Timber.Edge
                        : across % 4 == 0 && !broken ? Timber.Shade
                        : NeglectSoot;
                    art.SetPixel(x, y, color);
                }
        }

        /// <summary>B for fences, piers, bridges and lanterns: a few short pieces missing.</summary>
        private static void Breaks(Image art, Look s, Rect2I b, int salt)
        {
            var lengthwise = b.Size.Y >= b.Size.X;
            for (var k = 0; k < 3; k++)
            {
                var at = (int)(Hash(k, 11, salt) % (uint)Math.Max(1, (lengthwise ? b.Size.Y : b.Size.X) - 10)) + 5;
                var span = 3 + (int)(Hash(k, 12, salt) % 3);
                for (var a = at; a < at + span; a++)
                    for (var c = 0; c < (lengthwise ? b.Size.X : b.Size.Y); c++)
                    {
                        var x = lengthwise ? b.Position.X + c : b.Position.X + a;
                        var y = lengthwise ? b.Position.Y + a : b.Position.Y + c;
                        if (!In(s.Mask, x, y)) continue;
                        // Fences lose rail between posts; decks lose whole planks; a lantern only weathers.
                        if (s.Kind == Shape.Lantern) continue;
                        if (s.Kind == Shape.Fence && Hash(x, y, salt) % 3 == 0) continue;
                        if (s.Kind is Shape.Pier && c > (lengthwise ? b.Size.X : b.Size.Y) * 0.7f) continue;
                        art.SetPixel(x, y, Colors.Transparent);
                    }
            }
        }

        /// <summary>B: planks fallen from the roof hole, lying on the ground outside the front wall.</summary>
        private static void FallenPlanks(Image image, Look s, Rect2I b, int salt)
        {
            var x = (int)(b.Position.X + b.Size.X * 0.62f);
            var y = s.Door == DoorSide.North ? b.Position.Y - 4 : b.End.Y + 1;
            if (y + 3 >= image.GetHeight()) y = b.End.Y - 2;
            for (var k = 0; k < 7; k++)
            {
                Blend(image, x + k, y + k / 3, Timber.Base);
                Blend(image, x + k, y + k / 3 - 1, Timber.Light);
                Blend(image, x + k + 1, y + k / 3 + 1, NeglectShadow);
            }
            for (var k = 0; k < 5; k++)
            {
                Blend(image, x - 3 + k, y + 2, Timber.Shade);
                Blend(image, x - 3 + k, y + 3, NeglectShadow);
            }
        }

        /// <summary>Weeds and long grass round the structure and over its doorstep, thicker on a ruin.</summary>
        private static void Weeds(Image image, Look s, Rect2I b, bool ruin, int salt)
        {
            var w = image.GetWidth();
            var h = image.GetHeight();
            using var bridgeGround = s.Kind == Shape.Bridge ? BridgeGround(w, h) : null;
            // Match the short-end fallback in ApprovedArt.PortLandSide.
            var landSide = h >= w
                ? s.Door == DoorSide.South ? DoorSide.South : DoorSide.North
                : s.Door == DoorSide.West ? DoorSide.West : DoorSide.East;
            var odds = ruin ? 4u : 7u;
            var pathOdds = ruin ? 7u : 11u;
            for (var y = 1; y < h - 1; y++)
                for (var x = 1; x < w - 1; x++)
                {
                    if (In(s.Mask, x, y)) continue;
                    var near = Near(s.Mask, x, y, 4);
                    var onPath = s.Finished.GetPixel(x, y).A > 0.9f;
                    if (near > 4 && !onPath) continue;
                    // The approved pier has weeds on its land row or column.
                    if (s.Kind == Shape.Pier && !(landSide switch
                    {
                        DoorSide.North => y < 32,
                        DoorSide.South => y >= h - 32,
                        DoorSide.West => x < 32,
                        _ => x >= w - 32,
                    })) continue;
                    // Match the approved bridge's ground and current overlay test.
                    if (bridgeGround is not null)
                    {
                        var color = bridgeGround.GetPixel(x, y).Blend(image.GetPixel(x, y));
                        if (color.G <= color.B) continue;
                    }
                    if (Hash(x, y, salt + 21) % (onPath && near > 4 ? pathOdds : odds) != 0) continue;
                    var tall = Hash(y, x, salt) % 2 == 0;
                    Blend(image, x, y, OrchardCanopy.Base);
                    Blend(image, x + 1, y, OrchardCanopy.Shade);
                    Blend(image, x, y - 1, OrchardCanopy.Light);
                    if (tall) Blend(image, x, y - 2, ruin && Hash(x, y, 4) % 9 == 0 ? NeglectFlower : OrchardCanopy.Highlight);
                }
        }

        private static Image BridgeGround(int width, int height)
        {
            var ground = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
            using var water = WaterTextures.Atlas(32).GetImage();
            for (var y = 0; y < height / 32; y++)
                for (var x = 0; x < width / 32; x++)
                {
                    var bank = width > height ? x == 0 || x == width / 32 - 1 : y == 0 || y == height / 32 - 1;
                    if (bank)
                        ground.BlitRect(TerrainTextures.Tile(TerrainStyle.Grass, TerrainTextures.VariantAt(x + 3, y + 1), 32),
                            new Rect2I(0, 0, 32, 32), new Vector2I(x * 32, y * 32));
                    else
                        ground.BlitRect(water, (Rect2I)WaterTextures.Region(TerrainStyle.River, x, y, 32), new Vector2I(x * 32, y * 32));
                }
            return ground;
        }

        /// <summary>Two crossed planks nailed over the door, at its place on the front wall.</summary>
        private static void BoardedDoor(Image image, Look s, Rect2I b)
        {
            var (x, y) = s.Door switch
            {
                DoorSide.North => ((int)s.DoorMiddle - 4, b.Position.Y - 1),
                DoorSide.East => (b.End.X - 5, (int)s.DoorMiddle - 3),
                DoorSide.West => (b.Position.X - 2, (int)s.DoorMiddle - 3),
                _ => ((int)s.DoorMiddle - 4, b.End.Y - 4),
            };
            for (var k = 0; k < 8; k++)
            {
                var down = y + k * 5 / 8;
                var up = y + 4 - k * 5 / 8;
                Blend(image, x + k + 1, down + 1, NeglectShadow);
                Blend(image, x + k, down, Timber.Highlight);
                Blend(image, x + k, up, Timber.Light);
            }
            Blend(image, x, y, NeglectSoot);
            Blend(image, x + 7, y + 4, NeglectSoot);
            Blend(image, x, y + 4, NeglectSoot);
            Blend(image, x + 7, y, NeglectSoot);
        }

        /// <summary>B: a young tree come up by a back corner where nobody clears it any more.</summary>
        private static void Sapling(Image image, Look s, Rect2I b, int salt)
        {
            var w = image.GetWidth();
            var h = image.GetHeight();
            var candidates = new (int X, int Y)[] { (b.End.X + 2, b.Position.Y + 4), (b.Position.X - 6, b.Position.Y + 4), (b.End.X - 6, b.Position.Y + 4), (b.Position.X + 5, b.Position.Y + 5) };
            // With no clear ground, it grows hard against the back corner, its crown leaning over the eave.
            var fallback = (X: Math.Min(b.End.X + 1, w - 4), Y: Math.Max(b.Position.Y, 4));
            var (x, y) = candidates.FirstOrDefault(c => c.X >= 4 && c.X <= w - 5 && c.Y >= 4 && c.Y <= h - 5 && !In(s.Mask, c.X, c.Y), fallback);
            for (var dy = -4; dy <= 4; dy++)
                for (var dx = -4; dx <= 4; dx++)
                {
                    var d = dx * dx + dy * dy;
                    if (d <= 12) Blend(image, x + dx + 2, y + dy + 2, NeglectShadow);
                }
            for (var dy = -4; dy <= 4; dy++)
                for (var dx = -4; dx <= 4; dx++)
                {
                    var d = dx * dx + dy * dy;
                    var lobe = 10 + (int)(Hash(x + dx, y + dy, salt) % 4);
                    if (d > lobe) continue;
                    var rim = d > lobe - 5;
                    Blend(image, x + dx, y + dy, rim ? (dx + dy < 0 ? OrchardCanopy.Base : OrchardCanopy.Edge) : dx + dy < -1 ? OrchardCanopy.Light : OrchardCanopy.Base);
                }
            Blend(image, x - 1, y - 2, OrchardCanopy.Highlight);
        }
    }
}
