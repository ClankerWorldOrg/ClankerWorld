using Godot;

namespace ClankerWorld.GodotClient.UI;

/// <summary>Uncommitted tile marks, following the map camera including its east/west seam.</summary>
public partial class WorldEditPreviewLayer : Control
{
    private WorldTerrainLayer? terrain;
    private List<Vector2I> tiles = [];
    private Rect2 drawnCamera;
    private int drawnSize;
    public IReadOnlyList<Vector2I> Tiles => tiles;
    public string KindName { get; private set; } = "";

    public WorldEditPreviewLayer()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 7;
    }

    public void Follow(WorldTerrainLayer layer) => terrain = layer;

    public void SetPreview(IEnumerable<Vector2I> selected, string kindName)
    {
        tiles = selected.ToList();
        KindName = kindName;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (terrain is not null && tiles.Count > 0 &&
            (terrain.VisibleTiles != drawnCamera || terrain.TileSize != drawnSize)) QueueRedraw();
    }

    public override void _Draw()
    {
        if (terrain?.World is not { } world) return;
        drawnCamera = terrain.VisibleTiles;
        drawnSize = terrain.TileSize;
        foreach (var tile in tiles)
        {
            var bounds = new Rect2(tile, Vector2.One);
            var first = world.WrapsEastWest ? (int)MathF.Ceiling((drawnCamera.Position.X - bounds.End.X) / world.Width) : 0;
            var last = world.WrapsEastWest ? (int)MathF.Floor((drawnCamera.End.X - bounds.Position.X) / world.Width) : 0;
            for (var copy = first; copy <= last; copy++)
            {
                var at = (Vector2)tile + new Vector2(copy * world.Width, 0);
                if (!new Rect2(at, Vector2.One).Intersects(drawnCamera)) continue;
                var rectangle = new Rect2(at * terrain.Stride, Vector2.One * drawnSize);
                DrawRect(rectangle, new Color(1, 0.8f, 0.3f, 0.4f));
                DrawRect(rectangle, new Color("FFD166"), filled: false, width: 2);
            }
        }
    }
}
