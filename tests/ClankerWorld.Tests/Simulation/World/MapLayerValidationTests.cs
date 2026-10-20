using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class MapLayerValidationTests
{
    [Theory]
    [InlineData("climate")]
    [InlineData("hydrology")]
    [InlineData("surface")]
    [InlineData("vegetation")]
    public void PackedLayersAcceptExactlyTheirDefinedByteValues(string layer)
    {
        for (var value = 0; value <= byte.MaxValue; value++)
        {
            var map = OpenMap();
            var bytes = layer switch
            {
                "climate" => map.ClimateZones!,
                "hydrology" => map.HydrologyKinds!,
                "surface" => map.SurfaceKinds!,
                _ => map.VegetationKinds!,
            };
            // The last cell is away from both starter resources. A cactus
            // there has valid ground, so membership is the only changed rule.
            if (layer == "vegetation")
            {
                map.SurfaceKinds![^1] = (byte)SurfaceKind.Sand;
                map.ClimateZones![^1] = (byte)ClimateZone.Dry;
            }
            bytes[^1] = (byte)value;
            map = map with { ManifestDigest = MapManifestCodec.Digest(map) };
            var expected = layer switch
            {
                "climate" => Enum.IsDefined((ClimateZone)value),
                "hydrology" => Enum.IsDefined((WaterKind)value),
                "surface" => Enum.IsDefined((SurfaceKind)value),
                _ => Enum.IsDefined((VegetationCover)value),
            };
            var result = MapAcceptance.Validate(map, allowEmptyStartingSite: true);
            Assert.True(result.IsValid == expected,
                $"{layer} byte {value}: expected valid={expected}, got {result.IsValid}: {result.Failure}");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CactusPlacementChecksLateCellsAndFreshBorrowedLayersInEitherTileOrder(bool reverse)
    {
        foreach (var mode in new[] { "surface", "climate", "water", "height", "no-surface", "no-climate", "no-water", "no-height" })
        {
            var map = OpenMap(reverse);
            foreach (var index in new[] { 0, map.Tiles.Count - 1 })
            {
                map.VegetationKinds![index] = (byte)VegetationCover.Cactus;
                map.SurfaceKinds![index] = (byte)SurfaceKind.Sand;
                map.ClimateZones![index] = (byte)ClimateZone.Dry;
            }
            map = map with { ManifestDigest = MapManifestCodec.Digest(map) };
            var valid = MapAcceptance.Validate(map, allowEmptyStartingSite: true);
            Assert.True(valid.IsValid, valid.Failure);
            // Keep the first cactus valid and change the last one only.
            // In-place edits must be checked again after the successful call.
            switch (mode)
            {
                case "surface": map.SurfaceKinds![^1] = (byte)SurfaceKind.Grass; break;
                case "climate": map.ClimateZones![^1] = (byte)ClimateZone.Cold; break;
                case "water": map.HydrologyKinds![^1] = (byte)WaterKind.Ocean; break;
                case "height": map.ElevationLevels![^1] = WorldMap.MountainElevationThreshold; break;
                case "no-surface": map = map with { SurfaceKinds = null }; break;
                case "no-climate": map = map with { ClimateZones = null }; break;
                case "no-water": map = map with { HydrologyKinds = null }; break;
                case "no-height": map = map with { ElevationLevels = null }; break;
            }
            var result = MapAcceptance.Validate(map, allowEmptyStartingSite: true);
            if (mode == "no-height")
                Assert.True(result.IsValid, result.Failure); // Existing optional-elevation behavior.
            else
                Assert.True(!result.IsValid && result.Failure == "Cacti must stand on desert sand.",
                    $"{mode}, reversed={reverse}: expected cactus refusal, got {result.IsValid}: {result.Failure}");
        }
    }

    private static WorldMap OpenMap(bool reverse = false)
    {
        const int width = 16, height = 8, area = width * height;
        var tiles = Enumerable.Range(0, area).Select(index =>
            new TerrainTile(new(index % width, index / width), TerrainKind.Meadow)).ToArray();
        if (reverse) Array.Reverse(tiles);
        return new(width, height, 0, tiles, [],
            [new("berry-patch", "food", new(1, 1), true),
             new("timber-tree", "construction", new(width - 2, height - 2), true)], string.Empty)
        {
            ClimateZones = Enumerable.Repeat((byte)ClimateZone.Temperate, area).ToArray(),
            HydrologyKinds = new byte[area],
            SurfaceKinds = new byte[area],
            VegetationKinds = new byte[area],
            ElevationLevels = new byte[area],
        };
    }
}
