using System.Text.Json;
using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Observation;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class WorldMapEditTests
{
    internal static WorldMapEdit Edit(WorldRuntime world, params WorldMapEditOperation[] operations)
    {
        var state = world.ExportState();
        return new(world.Society.WorldId, state.EventHistoryFloor + state.Events.Count, world.Society.RunEpoch,
            "map-edit-" + (state.EventHistoryFloor + state.Events.Count), operations);
    }

    internal static GridPoint[] FreeTiles(WorldRuntime world)
    {
        var state = world.ExportState();
        var ground = new GroundOccupancy(state.Map, world.WorldContent, world.WorldSimulation,
            state.Fields, world.RoadTiles, state.Bridges, world.Towns);
        var occupied = GroundOccupancy.Combine(ground.MapObjects, ground.Resources, ground.Fields,
            ground.RoadsAndBridgeEntrances, ground.ProtectedBuildingTiles(), ground.ExpansionSites,
            ground.MarketSites, ground.MarketStallSlots, ground.ProtectedProjectSites(), world.Agents.Select(agent => agent.Position));
        var reachable = MapAcceptance.ReachableFrom(state.Map, world.Agents[0].Position);
        return state.Map.Tiles.Where(tile => state.Map.IsBuildable(tile.Position) &&
                state.Map.SurfaceAt(tile.Position) is not SurfaceKind.Sand && !occupied.Contains(tile.Position) && reachable.Contains(tile.Position))
            .Select(tile => tile.Position).ToArray();
    }

    internal static WorldMapEditOperation Operation(string kind, string value, GridPoint point) =>
        new(kind, null, value, null, point.X, point.Y, null);

    [Fact]
    public async Task EveryOfferedEditSurvivesStrictSaveAndEventReplayWithMatchingContinuation()
    {
        using var world = NormalPathWorld.CreateGenerated("map-edit-normal", _ => new DeterministicDecisionProvider());
        world.Pause();
        var baseline = WorldRuntimeCodec.Encode(world.ExportState());
        var points = FreeTiles(world);
        var command = Edit(world, WorldMapEditRules.Choices.Select((choice, index) =>
            Operation(choice.Operation, choice.Value, points[index])).ToArray());
        var applied = world.ApplyMapEdit(command);
        Assert.True(applied.Applied, applied.Failure);
        Assert.Equal(0, world.WorldTick);
        var accepted = world.ExportState();
        Assert.Equal(MapManifestCodec.Digest(accepted.Map), accepted.Map.ManifestDigest);
        foreach (var (choice, index) in WorldMapEditRules.Choices.Select((choice, index) => (choice, index)))
        {
            var point = points[index];
            if (choice.Terrain is { } terrain)
                Assert.Equal(terrain, accepted.Map.Tiles.Single(tile => tile.Position == point).Terrain);
            else if (choice.ResourceKind is { } kind)
            {
                var resource = Assert.Single(accepted.Map.Resources, resource => resource.Position == point);
                Assert.Equal(kind, resource.Kind);
                Assert.Equal(choice.NaturalObjectKind, resource.NaturalObjectKind);
                Assert.Equal(choice.IsRenewable, resource.IsRenewable);
            }
            else
                Assert.Equal(choice.ObjectKind, Assert.Single(accepted.Map.MapObjects, item => item.Position == point).Kind);
        }
        Assert.All(accepted.Map.Resources.Where(resource => resource.Id.StartsWith("developer-map:", StringComparison.Ordinal)), resource =>
        {
            Assert.Contains(world.WorldSystems.Ecology.Resources, item => item.Id == resource.Id && item.Position == resource.Position);
            Assert.Contains(world.WorldSystems.Chunks.SelectMany(chunk => chunk.Resources), item => item.ResourceId == resource.Id);
            Assert.True(IdentityRules.Reference(resource.Id, DerivedId.MaximumLength));
        });
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(baseline), _ => new DeterministicDecisionProvider());
        foreach (var entry in accepted.Events.Select(item => item.Payload).OfType<DeveloperEditEventPayload>().Where(item => item.Operation == "edit_world"))
        {
            var result = replay.ApplyMapEdit(JsonSerializer.Deserialize<WorldMapEdit>(entry.Value)!);
            Assert.True(result.Applied, result.Failure);
        }
        var finalBytes = WorldRuntimeCodec.Encode(accepted);
        Assert.Equal(finalBytes, WorldRuntimeCodec.Encode(replay.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(finalBytes), _ => new DeterministicDecisionProvider());
        world.Resume(); replay.Resume(); restored.Resume();
        for (var tick = 0; tick < 3; tick++)
        {
            await world.AdvanceOneTickAsync(); await replay.AdvanceOneTickAsync(); await restored.AdvanceOneTickAsync();
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(restored.ExportState()));
        }
    }

    [Fact]
    public void CurrentOverridesStayBoundedAndSurviveCompactionWithoutAcceptingUnexplainedTerrain()
    {
        var directory = Directory.CreateTempSubdirectory("map-edit-archive-");
        try
        {
            using var world = NormalPathWorld.CreateGenerated("map-edit-archive", _ => new DeterministicDecisionProvider());
            world.Pause();
            var points = FreeTiles(world);
            var first = Edit(world, Operation("set_terrain", "forest", points[0]));
            Assert.True(world.ApplyMapEdit(first).Applied);
            var latest = world.ApplyMapEdit(Edit(world, Operation("set_terrain", "snow", points[0])));
            Assert.True(latest.Applied, latest.Failure);
            var state = world.ExportState();
            Assert.Single(state.MapAuthoring!.Terrain);
            var next = state.Events[^1].EventId + 1;
            using var crowded = WorldRuntime.Restore(state with
            {
                Events = state.Events.Concat(Enumerable.Range(0, WorldHistory.CompactionThreshold + 1)
                    .Select(index => new WorldEventEntry(next + index, world.WorldTick, new PausedEventPayload(null)))).ToArray(),
            });
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            Assert.True(file.Save(crowded));
            var saved = crowded.ExportState();
            Assert.DoesNotContain(saved.Events, entry => entry.Payload is DeveloperEditEventPayload { Operation: "edit_world" });
            using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(File.ReadAllBytes(file.Path)));
            Assert.Equal(WorldRuntimeCodec.Encode(saved), WorldRuntimeCodec.Encode(reload.ExportState()));
            Assert.NotNull(reload.ApplyMapEdit(first).Failure);
            var unrelated = WorldMapAuthoring.PaintTerrain(saved.Map, [new(points[1], TerrainKind.Forest)]);
            unrelated = unrelated with { ManifestDigest = MapManifestCodec.Digest(unrelated) };
            Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(saved with { Map = unrelated }));
            Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(saved with { MapAuthoring = null }));
            Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(saved with
            {
                MapAuthoring = saved.MapAuthoring! with { Terrain = [new(points[0], (TerrainKind)999)] },
            }));
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public void NewCampObjectsKeepTheWorldWeatherAtItsOriginalTown()
    {
        using var original = NormalPathWorld.CreateGenerated("map-edit-markers", _ => new DeterministicDecisionProvider());
        original.Pause();
        var state = original.ExportState();
        var anchor = original.Towns.First(town => town.OriginSite is not null).OriginSite!.Value;
        var point = FreeTiles(original).First(point => point.X / WeatherRules.RegionSize != anchor.X / WeatherRules.RegionSize ||
            point.Y / WeatherRules.RegionSize != anchor.Y / WeatherRules.RegionSize);
        var systems = state.WorldSystems!;
        var weather = systems.RegionalWeather!;
        using var world = WorldRuntime.Restore(state with
        {
            WorldSystems = systems with
            {
                RegionalWeather = weather with
                {
                    Episodes = weather.Episodes.Select(episode => episode with
                    {
                        Weather = episode.X == anchor.X / WeatherRules.RegionSize && episode.Y == anchor.Y / WeatherRules.RegionSize
                            ? WeatherKind.Rain : WeatherKind.Clear,
                        StartedAt = 0,
                        EndsAt = systems.Config.TicksPerDay,
                        SevereAllowedAt = 0,
                    }).ToArray(),
                },
            },
        });
        Assert.Equal(WeatherKind.Clear, WeatherRules.At(world.WorldSystems, point, state.Map.Height));
        Assert.Equal("rain", new OwnerWorldObservationStore(world).GetSnapshot().Authoring!.Weather);
        var result = world.ApplyMapEdit(Edit(world, Operation("place_object", "cooking", point)));
        Assert.True(result.Applied, result.Failure);
        Assert.Equal("rain", new OwnerWorldObservationStore(world).GetSnapshot().Authoring!.Weather);
    }

    [Fact]
    public void InvalidOrStaleBatchesLeaveAllLayersResourcesEventsAndSavedBytesUntouched()
    {
        using var world = NormalPathWorld.CreateGenerated("map-edit-refusal", _ => new DeterministicDecisionProvider());
        world.Pause();
        var point = FreeTiles(world)[0];
        var operation = Operation("set_terrain", "meadow", point);
        var command = Edit(world, operation);
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        foreach (var invalid in new[]
        {
            command with { WorldId = "different-world" }, command with { ExpectedEventId = command.ExpectedEventId - 1 },
            command with { RunEpoch = command.RunEpoch + 1 }, command with { Operations = [] },
            command with { Operations = Enumerable.Repeat(operation, WorldMapEditRules.MaximumOperations + 1).ToArray() },
            command with { Operations = [operation, operation] },
            command with { Operations = [operation, Operation("set_terrain", "peak", world.Agents[0].Position)] },
            command with { Operations = [operation, operation with { X = -1 }] },
            command with { Operations = [operation with { Value = "unknown" }] },
            command with { Operations = [operation with { Kind = "set_weather_season" }] },
            command with { Operations = [Operation("place_resource", "food", point) with { IsRenewable = false }] },
            command with { Operations = [Operation("place_object", "storage", point) with { Id = new string('x', DerivedId.MaximumLength + 1) }] },
            command with { Operations = [Operation("place_object", "storage", point) with { Id = "storage" }] },
            command with { Operations = [Operation("place_object", "cooking", point) with { Id = "cooking" }] },
        })
        {
            var result = world.ApplyMapEdit(invalid, _ => Assert.Fail("A refused batch must never reach persistence."));
            Assert.False(result.Applied);
            Assert.False(result.AlreadyApplied);
            Assert.False(string.IsNullOrWhiteSpace(result.Failure));
            Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        }
        world.Resume();
        var running = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.Contains("Pause", world.ApplyMapEdit(Edit(world, operation)).Failure!);
        Assert.Equal(running, WorldRuntimeCodec.Encode(world.ExportState()));
    }

    [Fact]
    public void DurableBatchRetriesOnceAndFailedWriteCannotPublishAnyPart()
    {
        var directory = Directory.CreateTempSubdirectory("map-edit-write-");
        try
        {
            using var world = NormalPathWorld.CreateGenerated("map-edit-write", _ => new DeterministicDecisionProvider());
            world.Pause();
            var points = FreeTiles(world);
            var command = Edit(world, Operation("set_terrain", "forest", points[0]),
                Operation("place_resource", "food", points[1]), Operation("place_object", "storage", points[2]));
            command = command with { BatchId = new string('b', DerivedId.MaximumLength) };
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            file.Save(world);
            var before = WorldRuntimeCodec.Encode(world.ExportState());
            var saved = File.ReadAllBytes(file.Path);
            File.Move(file.Path, file.Path + ".prior");
            Directory.CreateDirectory(file.Path);
            var failure = Record.Exception(() =>
            {
                var result = file.ApplyMapEdit(world, command);
                Assert.True(result.Applied, result.Failure);
            });
            Assert.True(failure is IOException or UnauthorizedAccessException, failure?.ToString());
            Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
            Assert.Equal(saved, File.ReadAllBytes(file.Path + ".prior"));
            Directory.Delete(file.Path);
            File.Move(file.Path + ".prior", file.Path);
            var result = file.ApplyMapEdit(world, command);
            Assert.True(result.Applied, result.Failure);
            var accepted = WorldRuntimeCodec.Encode(world.ExportState());
            Assert.Equal(accepted, File.ReadAllBytes(file.Path));
            Assert.True(file.ApplyMapEdit(world, command).AlreadyApplied);
            using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(File.ReadAllBytes(file.Path)));
            Assert.True(reload.ApplyMapEdit(command).AlreadyApplied);
            Assert.Equal(accepted, WorldRuntimeCodec.Encode(reload.ExportState()));
            Assert.Single(reload.ExportState().Events, item => item.Payload is DeveloperEditEventPayload { Operation: "edit_world" });
        }
        finally { directory.Delete(true); }
    }
}
