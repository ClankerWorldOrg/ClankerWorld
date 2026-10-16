using System.Text.Json.Serialization;

namespace ClankerWorld.Simulation.World;

public sealed record WorldTerrainEdit(GridPoint Position, TerrainKind Terrain);

/// <summary>Current overrides, bounded by map cells; history compaction cannot remove their authority.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldMapAuthoring(
    [property: JsonRequired] IReadOnlyList<WorldTerrainEdit> Terrain,
    [property: JsonRequired] IReadOnlyList<MapResource> Resources,
    [property: JsonRequired] IReadOnlyList<MapObject> Objects)
{
    internal bool IsValid(WorldMap map) => Terrain is not null && Resources is not null && Objects is not null &&
        Terrain.Count <= map.Tiles.Count && Resources.Count <= map.Tiles.Count && Objects.Count <= map.Tiles.Count &&
        Terrain.All(edit => edit is not null && map.Contains(edit.Position) &&
            WorldMapEditRules.Choices.Any(choice => choice.Terrain == edit.Terrain)) &&
        Terrain.Select(edit => edit.Position).Distinct().Count() == Terrain.Count &&
        Resources.All(resource => resource is not null && IdentityRules.Reference(resource.Id, DerivedId.MaximumLength) && map.Contains(resource.Position) &&
            resource.TreeKind is null && WorldMapEditRules.Choices.Any(choice => choice.ResourceKind == resource.Kind &&
                choice.NaturalObjectKind == resource.NaturalObjectKind && choice.IsRenewable == resource.IsRenewable)) &&
        Objects.All(item => item is not null && IdentityRules.Reference(item.Id, DerivedId.MaximumLength) && map.Contains(item.Position) &&
            WorldMapEditRules.Choices.Any(choice => choice.ObjectKind is not null && choice.ObjectKind == item.Kind)) &&
        Resources.Select(resource => resource.Id).Concat(Objects.Select(item => item.Id)).Distinct(StringComparer.Ordinal).Count() == Resources.Count + Objects.Count &&
        Resources.Select(resource => resource.Position).Concat(Objects.Select(item => item.Position)).Distinct().Count() == Resources.Count + Objects.Count &&
        Terrain.SequenceEqual(Terrain.OrderBy(edit => edit.Position.Y).ThenBy(edit => edit.Position.X)) &&
        Resources.SequenceEqual(Resources.OrderBy(resource => resource.Id, StringComparer.Ordinal)) &&
        Objects.SequenceEqual(Objects.OrderBy(item => item.Id, StringComparer.Ordinal));

    internal static WorldMap PaintTerrain(WorldMap map, IReadOnlyList<WorldTerrainEdit> edits)
    {
        if (edits.Count == 0) return map;
        var byPoint = edits.ToDictionary(edit => edit.Position, edit => edit.Terrain);
        var tiles = map.Tiles.Select(tile => byPoint.TryGetValue(tile.Position, out var terrain) ? tile with { Terrain = terrain } : tile).ToArray();
        var elevation = map.ElevationLevels?.ToArray();
        var hydrology = map.HydrologyKinds?.ToArray();
        var surface = map.SurfaceKinds?.ToArray();
        var vegetation = map.VegetationKinds?.ToArray();
        foreach (var edit in edits)
        {
            var layer = edit.Position.Y * map.Width + edit.Position.X;
            var terrain = edit.Terrain;
            var water = terrain switch { TerrainKind.River => WaterKind.River, TerrainKind.Lake => WaterKind.Lake, TerrainKind.Ocean => WaterKind.Ocean, _ => WaterKind.Land };
            if (hydrology is not null) hydrology[layer] = (byte)water;
            if (elevation is not null) elevation[layer] = terrain switch { TerrainKind.Mountain => WorldMap.MountainElevationThreshold, TerrainKind.Peak => WorldMap.PeakElevationThreshold, _ => 100 };
            if (surface is not null) surface[layer] = (byte)(water != WaterKind.Land ? SurfaceKind.Water : terrain switch
            { TerrainKind.Mountain or TerrainKind.Peak => SurfaceKind.Rock, TerrainKind.Sand => SurfaceKind.Sand, TerrainKind.Forest => SurfaceKind.ForestFloor, TerrainKind.Snow => SurfaceKind.Snow, _ => SurfaceKind.Grass });
            if (vegetation is not null) vegetation[layer] = (byte)(terrain switch
            { TerrainKind.Meadow => VegetationCover.Grass, TerrainKind.Forest => VegetationCover.Forest, TerrainKind.Snow => VegetationCover.Tundra, _ => VegetationCover.None });
        }
        return map with
        {
            Tiles = tiles,
            ElevationLevels = elevation,
            HydrologyKinds = hydrology,
            SurfaceKinds = surface,
            VegetationKinds = vegetation
        };
    }

    internal WorldMap ApplyToBaseline(WorldMap baseline)
    {
        var result = PaintTerrain(baseline, Terrain) with
        {
            Resources = baseline.Resources.Concat(Resources).OrderBy(resource => resource.Id, StringComparer.Ordinal).ToArray(),
            MapObjects = baseline.MapObjects.Concat(Objects).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        };
        return result with { ManifestDigest = MapManifestCodec.Digest(result) };
    }
}
