using Godot;
using static ClankerWorld.GodotClient.UI.SpriteDrawing;

namespace ClankerWorld.GodotClient.UI;

public static partial class BuildingSprites
{
    private static partial class ApprovedArt
    {
        /// <summary>
        /// Round 3 Market: a timber market hall with no stalls inside it; the
        /// stalls stand outside on the approved packed-earth plaza. A
        /// hipped roof of timber planks carries a louvred lantern along its ridge
        /// that flies the approved Market's gold pennant. Along the door side a
        /// lower lean-to roof covers an open arcade, with a sign of trading
        /// scales over the way in and the doorstep and path beyond its eave. It
        /// is laid out once with the door "down" and turned to the door side by
        /// <see cref="PortFrame"/>; every piece is lit from the north-west.
        /// </summary>
        private static void PaintMarket(Plate p, Recipe recipe, int w, int h, BuildingDoor door)
        {
            var side = door.Side;
            var frame = new PortFrame(Opposite(side), w, h);
            float breadth = frame.Breadth, length = frame.Length;
            // The arcade's eave stands seven units in from the footprint edge, leaving room for the doorstep and path.
            const float front = 7, arcadeDepth = 12;
            var arcade = p.Px(frame.Map(4, length - front - arcadeDepth, breadth - 8, arcadeDepth));
            var hall = p.Px(frame.Map(3, 3, breadth - 6, length - front - arcadeDepth - 2));
            var middle = p.P(Fit(door.Tile is { } t ? t * 32 + 16 : breadth / 2f, 14, breadth - 14));
            // The hall roof's eave shadow falls across the lean-to; where both shadows land outside, only one is laid.
            var hallShadow = new Rect2I(hall.Position.X + p.P(2), hall.Position.Y + p.P(3), hall.Size.X, hall.Size.Y);
            LeanTo(p, arcade, side, recipe with { Salt = 225 }, hallShadow);
            ArcadePosts(p, arcade, side, middle);
            PaintRoof(p, hall, recipe);
            Lantern(p, hall);
            var sign = Inward(p, arcade, side, middle, 6);
            ScalesSign(p, sign.X, sign.Y);
            var (from, to) = Span(p, middle, 3);
            var next = Step(p, arcade, side, from, to, 1, p.Small ? 1 : 3);
            if (door.Tile is not null) Path(p, arcade, side, middle, next);
        }

        /// <summary>The side across the footprint from <paramref name="side"/>.</summary>
        private static DoorSide Opposite(DoorSide side) => side switch
        {
            DoorSide.North => DoorSide.South,
            DoorSide.East => DoorSide.West,
            DoorSide.West => DoorSide.East,
            _ => DoorSide.North,
        };

        /// <summary>
        /// A lean-to roof over <paramref name="area"/> (pixels): one face sloping
        /// down toward <paramref name="front"/>, laid in the recipe's courses
        /// along that eave, with the eave shadow and the one-pixel edge. The main
        /// roof, painted after it, lays its own eave shadow across it, so the
        /// lean-to leaves out its shadow where <paramref name="shadowed"/> already has one.
        /// </summary>
        private static void LeanTo(Plate p, Rect2I area, DoorSide front, Recipe recipe, Rect2I shadowed)
        {
            int x0 = area.Position.X, y0 = area.Position.Y, w = area.Size.X, h = area.Size.Y;
            if (w < 4 || h < 4) return;
            var shadow = new Rect2I(x0 + p.P(2), y0 + p.P(3), w, h);
            for (var y = shadow.Position.Y; y < shadow.End.Y; y++)
                for (var x = shadow.Position.X; x < shadow.End.X; x++)
                    if (!shadowed.HasPoint(new Vector2I(x, y))) p.Put(x, y, Shadow);
            var face = front switch
            {
                DoorSide.North => Face.North,
                DoorSide.East => Face.East,
                DoorSide.West => Face.West,
                _ => Face.South,
            };
            for (var j = 0; j < h; j++)
                for (var i = 0; i < w; i++)
                {
                    if (i == 0 || j == 0 || i == w - 1 || j == h - 1)
                    {
                        p.Put(x0 + i, y0 + j, recipe.Roof.Edge);
                        continue;
                    }
                    // "along" follows the eave, "across" counts up the slope from it, as on a main roof face.
                    var (along, across) = front switch
                    {
                        DoorSide.North => (i - 1, j - 1),
                        DoorSide.East => (j - 1, w - 2 - i),
                        DoorSide.West => (j - 1, i - 1),
                        _ => (i - 1, h - 2 - j),
                    };
                    p.Put(x0 + i, y0 + j, Surface(recipe, face, along, across, p.Small));
                }
        }

        /// <summary>
        /// The Market's ridge lantern: a raised, louvred vent along the middle of
        /// the ridge that lets the air out of the hall. From above it is a small
        /// hipped roof of grey slate shingles sitting astride the main ridge, its
        /// shadow falling south-east across the planks, with the approved
        /// Market's gold pennant flying from its peak.
        /// </summary>
        private static void Lantern(Plate p, Rect2I hall)
        {
            int iw = hall.Size.X - 2, ih = hall.Size.Y - 2;
            var horizontal = iw >= ih;
            var length = horizontal ? iw : ih;
            var breadth = horizontal ? ih : iw;
            var ridge = (breadth + 1) / 2 - 1;
            var along = Math.Max(p.P(12), length * 2 / 5);
            var across = p.Small ? 5 : 9;
            int centreAlong = length / 2, top = ridge - across / 2;
            var roof = horizontal
                ? new Rect2I(hall.Position.X + 1 + centreAlong - along / 2, hall.Position.Y + 1 + top, along, across)
                : new Rect2I(hall.Position.X + 1 + top, hall.Position.Y + 1 + centreAlong - along / 2, across, along);
            PaintRoof(p, roof, new Recipe(Slate, Material.Shingle, RoofShape.Hip, 226));
            var peak = RidgeEnds(roof.Position.X + 1, roof.Position.Y + 1, roof.Size.X - 2, roof.Size.Y - 2);
            var (px, py) = peak[0];
            Pennant(p, px + (peak[1].X - px) / 2 + (p.Small ? 0 : 1), py + (peak[1].Y - py) / 2);
        }

        /// <summary>
        /// The arcade's posts along the lean-to's eave, two flanking the way in
        /// and the rest spaced evenly to the corners: each a timber post head lit
        /// on its north-west pixel, with a small shadow.
        /// </summary>
        private static void ArcadePosts(Plate p, Rect2I arcade, DoorSide side, int middle)
        {
            // At 16 px a post would be a single edge-coloured pixel on the eave, so the arcade shows by its roof alone (B7).
            if (p.Small) return;
            const int size = 3;
            var horizontal = side is DoorSide.South or DoorSide.North;
            var start = (horizontal ? arcade.Position.X : arcade.Position.Y) + 1;
            var end = (horizontal ? arcade.End.X : arcade.End.Y) - 1 - size;
            var gap = p.P(6);
            var spots = new List<int>();
            // Posts from a door-side post out to a corner; a short run keeps only its corner post.
            void Run(int corner, int flank)
            {
                if (Math.Abs(flank - corner) < p.P(8))
                {
                    spots.Add(corner);
                    return;
                }
                var bays = Math.Max(1, (int)MathF.Round(Math.Abs(flank - corner) / (float)p.P(11)));
                for (var k = 0; k <= bays; k++) spots.Add(corner + (flank - corner) * k / bays);
            }
            Run(start, middle - gap - size);
            Run(end, middle + gap);
            foreach (var t in spots.Distinct())
            {
                var (x, y) = side switch
                {
                    DoorSide.North => (t, arcade.Position.Y - size / 2),
                    DoorSide.East => (arcade.End.X - 1 - size / 2, t),
                    DoorSide.West => (arcade.Position.X - size / 2, t),
                    _ => (t, arcade.End.Y - 1 - size / 2),
                };
                p.Fill(x + 1, y + 1, size, size, SmallShadow);
                p.Fill(x, y, size, size, Timber.Edge);
                p.Put(x + 1, y + 1, Timber.Light);
            }
        }

        /// <summary>B6 Market: a cream sign board with gold trading scales: a beam on a post, a pan hanging from each end.</summary>
        private static void ScalesSign(Plate p, int cx, int cy)
        {
            if (p.Small)
            {
                p.Fill(cx - 1, cy - 1, 4, 4, SmallShadow);
                p.Fill(cx - 2, cy - 2, 4, 4, Timber.Edge);
                p.Fill(cx - 1, cy - 1, 2, 2, Cloth.Light);
                p.Fill(cx - 1, cy - 1, 2, 1, Gold.Base);
                p.Put(cx, cy, Gold.Shade);
                return;
            }
            SignBoard(p, cx, cy);
            string[] scales =
            [
                "...h...",
                "lllllll",
                "s..b..s",
                "bb.b.bb",
                "...b...",
                "..sbs..",
            ];
            for (var j = 0; j < scales.Length; j++)
                for (var i = 0; i < scales[j].Length; i++)
                {
                    Color? c = scales[j][i] switch
                    {
                        'h' => Gold.Highlight,
                        'l' => Gold.Light,
                        'b' => Gold.Base,
                        's' => Gold.Shade,
                        _ => null,
                    };
                    if (c is { } colour) p.Put(cx - 3 + i, cy - 3 + j, colour);
                }
        }

        /// <summary>A market stall on its own tile, facing the door side, with a gap for the path.</summary>
        private static void PaintStallLot(Plate p, int w, int h, BuildingDoor door)
        {
            var area = new Rect2(4, 4, w - 8, h - 9);
            Stall(p, area, door.Side, Berry, Fruit, 5);
            if (door.Tile is null) return;
            // The path runs from the crates to the footprint edge along the door axis.
            var middle = p.P(door.Tile.Value * 32 + 16);
            var lot = p.Px(area);
            var front = door.Side switch
            {
                DoorSide.North => new Rect2I(lot.Position.X, lot.Position.Y, lot.Size.X, 1),
                DoorSide.East => new Rect2I(lot.End.X - 1, lot.Position.Y, 1, lot.Size.Y),
                DoorSide.West => new Rect2I(lot.Position.X, lot.Position.Y, 1, lot.Size.Y),
                _ => new Rect2I(lot.Position.X, lot.End.Y - 1, lot.Size.X, 1),
            };
            Path(p, front, door.Side, middle, 1);
        }

        /// <summary>
        /// One stall in <paramref name="area"/> (units): an awning at the back
        /// sloping toward <paramref name="front"/>, and crates of produce on the
        /// counter in front of it, in two pairs with the middle clear for the path.
        /// </summary>
        private static void Stall(Plate p, Rect2 area, DoorSide front, Ramp stripe, Ramp produce, int salt)
        {
            var across = front is DoorSide.South or DoorSide.North;
            var alongSize = across ? area.Size.X : area.Size.Y;
            var depthSize = across ? area.Size.Y : area.Size.X;
            var awningDepth = MathF.Round(depthSize * 0.6f);
            Awning(p, p.Px(Orient(area, front, 0, 0, alongSize, awningDepth)), front, stripe);
            var crateDepth = Math.Min(5f, depthSize - awningDepth);
            var crates = new[] { 1f, 6f, alongSize - 11, alongSize - 6 };
            var ramps = new[] { produce, OrchardCanopy, Berry, Gold };
            for (var i = 0; i < crates.Length; i++)
            {
                if (crates[i] + 5 > alongSize) continue;
                Crate(p, p.Px(Orient(area, front, crates[i], awningDepth, 5, crateDepth)), ramps[(i + salt) % ramps.Length]);
            }
        }

        /// <summary>
        /// Maps a rectangle written in a frame where <paramref name="front"/>
        /// is "down" (depth runs from the back toward the front) into units.
        /// </summary>
        private static Rect2 Orient(Rect2 area, DoorSide front, float along, float depth, float alongSize, float depthSize) => front switch
        {
            DoorSide.North => new Rect2(area.Position.X + along, area.End.Y - depth - depthSize, alongSize, depthSize),
            DoorSide.East => new Rect2(area.Position.X + depth, area.Position.Y + along, depthSize, alongSize),
            DoorSide.West => new Rect2(area.End.X - depth - depthSize, area.Position.Y + along, depthSize, alongSize),
            _ => new Rect2(area.Position.X + along, area.Position.Y + depth, alongSize, depthSize),
        };

        /// <summary>A small open crate of produce: timber rim, produce heaped inside with a lit top-left.</summary>
        private static void Crate(Plate p, Rect2I box, Ramp produce)
        {
            if (box.Size.X < 2 || box.Size.Y < 2) return;
            p.Fill(new Rect2I(box.Position.X + 1, box.Position.Y + 1, box.Size.X, box.Size.Y), SmallShadow);
            p.Fill(box, Timber.Edge);
            var inner = new Rect2I(box.Position.X + 1, box.Position.Y + 1, box.Size.X - 2, box.Size.Y - 2);
            if (inner.Size.X <= 0 || inner.Size.Y <= 0)
            {
                p.Fill(box.Position.X, box.Position.Y, 1, 1, produce.Base);
                return;
            }
            p.Fill(inner, produce.Base);
            p.Put(inner.Position.X, inner.Position.Y, produce.Light);
            if (inner.Size.X > 2) p.Put(inner.Position.X + 2, inner.Position.Y, produce.Light);
            if (inner.Size.Y > 1) p.Put(inner.End.X - 1, inner.End.Y - 1, produce.Shade);
        }

        /// <summary>The Market's finial, on the hall's lantern: a pole top with a little gold pennant flying east.</summary>
        private static void Pennant(Plate p, int x, int y)
        {
            if (p.Small)
            {
                p.Put(x, y, Timber.Edge);
                p.Put(x + 1, y, Gold.Light);
                return;
            }
            p.Fill(x - 1, y - 1, 2, 2, Timber.Edge);
            p.Fill(x + 1, y - 2, 4, 1, Gold.Light);
            p.Fill(x + 1, y - 1, 3, 1, Gold.Base);
            p.Put(x + 1, y, Gold.Shade);
        }

        private static DoorSide PortLandSide(int tilesWide, int tilesHigh, DoorSide door) => tilesHigh >= tilesWide
            ? door == DoorSide.South ? DoorSide.South : DoorSide.North
            : door == DoorSide.West ? DoorSide.West : DoorSide.East;

        /// <summary>
        /// The Port's layout frame. The Port is laid out once with the land at
        /// the top ("across" runs along the shore, "out" from the land over the
        /// water) and mapped into the footprint for each of its four rotations.
        /// Only positions are mapped; every piece is drawn in place with the
        /// north-west light, so a rotated Port is lit like any other building.
        /// </summary>
        private readonly record struct PortFrame(DoorSide Land, float Width, float Height)
        {
            /// <summary>The length of the Port from the land end to the sea end, in units.</summary>
            public float Length => Land is DoorSide.North or DoorSide.South ? Height : Width;

            /// <summary>The width of the Port along the shore, in units.</summary>
            public float Breadth => Land is DoorSide.North or DoorSide.South ? Width : Height;

            /// <summary>A rectangle in the frame (across, out, size across, size out) to footprint units.</summary>
            public Rect2 Map(float across, float outward, float sizeAcross, float sizeOut) => Land switch
            {
                DoorSide.North => new Rect2(across, outward, sizeAcross, sizeOut),
                DoorSide.South => new Rect2(across, Height - outward - sizeOut, sizeAcross, sizeOut),
                DoorSide.West => new Rect2(outward, across, sizeOut, sizeAcross),
                _ => new Rect2(Width - outward - sizeOut, across, sizeOut, sizeAcross),
            };

            public Rect2 Map(Rect2 r) => Map(r.Position.X, r.Position.Y, r.Size.X, r.Size.Y);

            /// <summary>A point in the frame to footprint units.</summary>
            public Vector2 Point(float across, float outward) => Land switch
            {
                DoorSide.North => new Vector2(across, outward),
                DoorSide.South => new Vector2(across, Height - outward),
                DoorSide.West => new Vector2(outward, across),
                _ => new Vector2(Width - outward, across),
            };

            /// <summary>The top-left corner of a small square of <paramref name="size"/> units placed at (across, out).</summary>
            public Vector2 Corner(float across, float outward, float size) => Map(across, outward, size, size).Position;
        }

        /// <summary>
        /// B3/B6 Port: one row on land, a plank shed whose door faces the Road;
        /// the other three rows are a plank pier on piles over the water,
        /// widening to a T-head with iron bollards and goods waiting. The pier
        /// keeps open water on both long sides, where boats tie up. Drawn in any
        /// of the four rotations (<see cref="PortLandSide"/>).
        /// <paramref name="paintedBoat"/> adds the moored boat of the approved
        /// round-1 drawing; round 2 leaves the water free for the real boat.
        /// </summary>
        private static void PaintPort(Plate p, int w, int h, BuildingDoor door, bool paintedBoat)
        {
            var frame = new PortFrame(PortLandSide(w / 32, h / 32, door.Side), w, h);
            float breadth = frame.Breadth, length = frame.Length;
            const float shedBottom = 27;
            var pier = new Rect2(breadth / 2f - 12, shedBottom - 3, 24, length - 30 - (shedBottom - 3));
            var head = new Rect2(6, length - 30, breadth - 12, 24);
            foreach (var deck in new[] { pier, head })
            {
                var px = p.Px(frame.Map(deck));
                p.Fill(new Rect2I(px.Position.X + p.P(2), px.Position.Y + p.P(3), px.Size.X, px.Size.Y), Shadow);
            }
            if (paintedBoat) Boat(p, frame.Map(new Rect2(pier.End.X + 4, 46, 9, 28)));
            Deck(p, p.Px(frame.Map(pier)), 201);
            Deck(p, p.Px(frame.Map(head)), 202);
            // Piles standing proud of the deck edges.
            void PostAt(float across, float outward)
            {
                var corner = frame.Corner(across, outward, 3);
                Post(p, corner.X, corner.Y);
            }
            for (var y = 38f; y < head.Position.Y - 4; y += 16)
            {
                PostAt(pier.Position.X - 1, y);
                PostAt(pier.End.X - 2, y);
            }
            foreach (var (x, y) in new[] { (head.Position.X - 1, head.Position.Y - 1), (head.End.X - 2, head.Position.Y - 1), (head.Position.X - 1, head.End.Y - 2), (head.End.X - 2, head.End.Y - 2) })
                PostAt(x, y);
            // Bollards along the outer edge of the T-head, and one on the pier where a boat ties up.
            foreach (var (x, y) in new[] { (head.Position.X + 6, head.End.Y - 4), (head.End.X - 6, head.End.Y - 4), (breadth / 2f, head.End.Y - 4), (pier.End.X - 3, 48f) })
            {
                var at = frame.Point(x, y);
                Bollard(p, at.X, at.Y);
            }
            // Mooring line from the pier bollard to the painted boat's bow.
            if (paintedBoat)
            {
                var (from, to) = (frame.Point(pier.End.X - 2, 48), frame.Point(pier.End.X + 7.5f, 47.5f));
                p.Canvas.Line(from.X, from.Y, to.X, to.Y, Cloth.Shade);
            }
            // Goods waiting on the T-head.
            foreach (var (x, y) in new[] { (head.Position.X + 9, head.Position.Y + 6), (head.Position.X + 16, head.Position.Y + 8) })
            {
                var corner = frame.Corner(x, y, 6);
                DeckCrate(p, corner.X, corner.Y);
            }
            var coil = frame.Point(head.End.X - 12, head.Position.Y + 9);
            p.Canvas.Ring(coil.X, coil.Y, 2.5f, Cloth.Shade);
            p.Canvas.Disc(coil.X, coil.Y, 1.2f, Cloth.Base);
            // The shed on the shore.
            var recipe = new Recipe(Timber, Material.Shingle, RoofShape.Hip, 203);
            var shed = new Rect2(9, 5, breadth - 18, shedBottom - 5);
            var roof = p.Px(frame.Map(shed));
            PaintRoof(p, roof, recipe);
            var middle = p.P(Fit(door.Tile is { } t ? t * 32 + 16 : breadth / 2f, shed.Position.X + 6, shed.End.X - 6));
            var next = Door(p, roof, frame.Land, middle, 3, 2);
            if (door.Tile is not null) Path(p, roof, frame.Land, middle, next);
        }

        /// <summary>A plank deck: planks laid across it, seams one step darker, ragged plank ends and edge stringers.</summary>
        private static void Deck(Plate p, Rect2I deck, int salt)
        {
            var depth = p.Small ? 2 : 4;
            var eastWest = deck.Size.Y >= deck.Size.X;
            int length = eastWest ? deck.Size.X : deck.Size.Y, breadth = eastWest ? deck.Size.Y : deck.Size.X;
            for (var j = 0; j < breadth; j++)
            {
                var course = j / depth;
                var row = j % depth;
                var shortStart = PixelArt.Hash(course, 0, salt) % 2 == 0;
                var shortEnd = PixelArt.Hash(course, 1, salt) % 2 == 0;
                var tone = PixelArt.Hash(course, 2, salt) % 4 == 0 ? 3 : 2;
                for (var i = 0; i < length; i++)
                {
                    if ((i == 0 && shortStart) || (i == length - 1 && shortEnd)) continue;
                    Color c;
                    if (row == depth - 1) c = Timber.Shade;
                    else if (i <= 1 || i >= length - 2) c = Timber.Shade;
                    else if (row == 0 && !p.Small) c = Timber[tone + 1];
                    else c = Timber[tone];
                    if (!p.Small && row != depth - 1 && PixelArt.Hash(i, j, salt + 4) % 37 == 0) c = Timber.Shade;
                    if (eastWest) p.Put(deck.Position.X + i, deck.Position.Y + j, c);
                    else p.Put(deck.Position.X + j, deck.Position.Y + i, c);
                }
            }
        }

        /// <summary>A pile head standing at a deck edge: timber, lit north-west, with a small shadow.</summary>
        private static void Post(Plate p, float x, float y)
        {
            var px = p.P(x);
            var py = p.P(y);
            var size = p.Small ? 2 : 3;
            p.Fill(px + 1, py + 1, size, size, SmallShadow);
            p.Fill(px, py, size, size, Timber.Edge);
            if (!p.Small) p.Put(px + 1, py + 1, Timber.Light);
        }

        /// <summary>B6 Port: an iron mooring bollard, a dark ring with a lit cap.</summary>
        private static void Bollard(Plate p, float x, float y)
        {
            var c = p.Canvas;
            c.Disc(x + 1, y + 1.5f, 2.4f, SmallShadow);
            c.Disc(x, y, 2.4f, Iron.Edge);
            c.Disc(x - 0.3f, y - 0.3f, 1.5f, Iron.Base);
            c.Dot(x - 1, y - 1, Iron.Highlight);
        }

        /// <summary>A closed crate on the deck: lid boards with a seam, lit top edge.</summary>
        private static void DeckCrate(Plate p, float x, float y)
        {
            var c = p.Canvas;
            c.Rect(x + 1, y + 2, 6, 6, SmallShadow);
            c.Rect(x, y, 6, 6, Timber.Edge);
            c.Rect(x + 1, y + 1, 4, 4, Timber.Light);
            c.Rect(x + 1, y + 1, 4, 1, Timber.Highlight);
            c.Rect(x + 1, y + 3, 4, 1, Timber.Base);
        }

        /// <summary>A rowing boat moored beside the pier: hull, darker inside, two thwarts, shadow on the water.</summary>
        private static void Boat(Plate p, Rect2 area)
        {
            var c = p.Canvas;
            var cx = area.Position.X + area.Size.X / 2;
            var cy = area.Position.Y + area.Size.Y / 2;
            var rx = area.Size.X / 2;
            var ry = area.Size.Y / 2;
            c.Ellipse(cx + 2, cy + 3, rx, ry, Shadow);
            c.Ellipse(cx, cy, rx, ry, Timber.Edge);
            c.Ellipse(cx - 0.3f, cy, rx - 1, ry - 1, Timber.Light);
            c.Ellipse(cx + 0.2f, cy + 0.5f, rx - 2.2f, ry - 3, Timber.Shade);
            c.Rect(cx - rx + 2, cy - 5, rx * 2 - 4, 1, Timber.Base);
            c.Rect(cx - rx + 2, cy + 4, rx * 2 - 4, 1, Timber.Base);
        }
    }
}
