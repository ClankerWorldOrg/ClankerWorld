using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed class StarterClayBankTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StarterClayUsesTheAcceptedTownLandmassAndReplaysAcrossStrictReload(bool island)
    {
        var options = new GeographyOptions("world-audit-island-0", WorldSizePreset.Small,
            WaterPercent: 50, HydrologyVersion: 1, CandidateAttempt: 2);
        using var world = new WorldRuntime(options.Seed, _ => new ActionCoverageRecorder(chooseIdle: true),
            startPace: WorldStartPace.FounderSetup, geographyOptions: options);
        var map = world.ExportState().Map;
        var site = island ? new GridPoint(0, 2) : NormalPathWorld.FindStartingTownSite(map);
        world.InitializeFirstTownContent();
        var layout = world.AcceptFirstTownLayout(site);
        var warehouse = layout.Buildings.Single(building => building.Role == "warehouse").Position;
        var occupied = layout.RoadTiles.Concat(layout.Buildings.SelectMany(building =>
            Enumerable.Range(0, building.Height).SelectMany(y => Enumerable.Range(0, building.Width)
                .Select(x => new GridPoint(building.Position.X + x, building.Position.Y + y)))))
            .Concat(map.Resources.Select(resource => resource.Position)).ToHashSet();
        GridPoint[] placements = island ? [new(2, 3), new(251, 3), new(252, 3), new(253, 3)] :
            map.Tiles.Select(tile => tile.Position).Where(point => map.FootDistance(site, point) <= 5 &&
                map.IsBuildable(point) && !occupied.Contains(point)).Take(WorldRuntime.RequiredFounders).ToArray();
        Assert.Equal(WorldRuntime.RequiredFounders, placements.Length);
        for (var index = 0; index < placements.Length; index++)
            world.PlaceFounder($"founder:{index + 1:D32}", placements[index]);
        world.StartWorld();
        var initial = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(initial, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var state = world.ExportState();
        var clay = state.Map.Resources.SingleOrDefault(resource => resource.Id == "settlement-clay");
        if (clay is null)
        {
            Assert.True(island);
            Assert.Contains(state.Events, entry => entry.Kind == "starting_resource_blocked" && entry.Detail == "clay");
        }
        else
        {
            Assert.True(state.Map.IsReachableOnFoot(warehouse, clay.Position),
                $"Starter clay at {clay.Position} cannot be reached from the accepted Warehouse at {warehouse}.");
            Assert.True(state.Map.IsBuildable(clay.Position));
            Assert.DoesNotContain(clay.Position, occupied);
            Assert.Equal(WaterKind.Land, state.Map.HydrologyAt(clay.Position));
            GridPoint[] neighbors = [new(clay.Position.X, clay.Position.Y - 1), new(clay.Position.X + 1, clay.Position.Y),
                new(clay.Position.X, clay.Position.Y + 1), new(clay.Position.X - 1, clay.Position.Y)];
            Assert.Contains(neighbors.Select(state.Map.WrapColumn).Where(state.Map.Contains), point =>
                state.Map.HydrologyAt(point) is WaterKind.River or WaterKind.Lake);
        }
        var checkpoint = WorldRuntimeCodec.Encode(state);
        using var loaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(checkpoint), _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.Equal(checkpoint, WorldRuntimeCodec.Encode(loaded.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await loaded.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(loaded.ExportState()));
    }

    [Fact]
    public async Task GeneratedRockyFreshwaterWorldKeepsAdvancingAndCheckpointingAcrossStarterClayInsertion()
    {
        using var world = NormalPathWorld.CreateGenerated("default-estate-council", _ => new ActionCoverageRecorder(chooseIdle: true));
        var initial = WorldRuntimeCodec.Encode(world.ExportState());
        var mountain = new GridPoint(93, 95);
        Assert.True(world.ExportState().Map.IsPassable(mountain));
        Assert.False(world.ExportState().Map.IsBuildable(mountain));
        // This normal seed previously chose that passable mountain as its clay
        // bank, making the first host checkpoint invalid and pausing the world.
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(initial, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(initial), _ => new ActionCoverageRecorder(chooseIdle: true));
        var directory = Path.Combine(Path.GetTempPath(), "clankerworld-starter-clay-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(directory, "world.json");
            var file = new WorldStateFile(path);
            file.Save(world);
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(1));
            presence.RecordAuthenticatedReconnect("owner:test");
            using var service = new WorldRuntimeService(world, file, presence);
            for (var tick = 0; tick < 4; tick++)
            {
                Assert.True(await service.TryAdvanceOnceAsync());
                Assert.False(world.Society.IsPaused);
                Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
                var checkpoint = WorldRuntimeCodec.Encode(world.ExportState());
                Assert.Equal(checkpoint, File.ReadAllBytes(path));
                Assert.Equal(checkpoint, WorldRuntimeCodec.Encode(replay.ExportState()));
                using var loaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(File.ReadAllBytes(path)));
                Assert.Equal(checkpoint, WorldRuntimeCodec.Encode(loaded.ExportState()));
            }
            var map = world.ExportState().Map;
            var clay = Assert.Single(map.Resources, resource => resource.Id == "settlement-clay");
            Assert.NotEqual(mountain, clay.Position);
            Assert.True(map.IsBuildable(clay.Position));
            Assert.True(map.IsReachableOnFoot(world.WorldSimulation.Buildings.Single(building =>
                building.InstanceId == "first-town-warehouse").Position, clay.Position));
            Assert.Equal(WaterKind.Land, map.HydrologyAt(clay.Position));
            GridPoint[] neighbors = [new(clay.Position.X, clay.Position.Y - 1), new(clay.Position.X + 1, clay.Position.Y),
                new(clay.Position.X, clay.Position.Y + 1), new(clay.Position.X - 1, clay.Position.Y)];
            Assert.Contains(neighbors.Select(map.WrapColumn).Where(map.Contains), point => map.HydrologyAt(point) is WaterKind.River or WaterKind.Lake);
            Assert.True(MapAcceptance.Validate(map, allowEmptyStartingSite: true).IsValid);
            Assert.Equal(16, world.ExportState().WorldSystems!.Ecology.GetResource(clay.Id).Quantity);
            world.Validate();
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
