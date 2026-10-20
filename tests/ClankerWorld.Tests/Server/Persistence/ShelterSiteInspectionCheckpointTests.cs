using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;
using static ClankerWorld.Tests.ShelterOrderTestFixture;

namespace ClankerWorld.Tests;

public sealed class ShelterSiteInspectionCheckpointTests
{
    [Theory]
    [InlineData("seek shelter", "ocean")]
    [InlineData("light a fire", "ocean")]
    [InlineData("seek shelter", "other_impassable")]
    [InlineData("light a fire", "other_impassable")]
    [InlineData("seek shelter", "land")]
    [InlineData("light a fire", "land")]
    public async Task InspectingAnUnusableSiteKeepsTheHostSaveableAndLearnsOnlyPassableTiles(string command, string site)
    {
        var state = WithClearWeather(Prepared());
        var actor = Actor(state);
        var (origin, target) = InspectionSite(state, actor, site);
        state = At(state, actor, origin);
        Assert.Equal(site == "land", state.Map.IsPassable(target));
        Assert.DoesNotContain(state.Knowledge!.Facts, fact => fact.OwnerId == actor && fact.Position == target);
        var initial = WorldRuntimeCodec.Encode(state);
        IDecisionProvider Providers(string id) => id == actor
            ? new DeterministicDecisionProvider() : new ActionCoverageRecorder(chooseIdle: true);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(initial), Providers);
        var normalized = WorldRuntimeCodec.Encode(world.ExportState());
        using (var validated = WorldRuntime.Restore(WorldRuntimeCodec.Decode(normalized), Providers))
            Assert.Equal(normalized, WorldRuntimeCodec.Encode(validated.ExportState()));
        var receipt = world.SubmitInstruction(new OwnerInstructionRequest("inspect-site", "owner:test", actor,
            OwnerInstructionKind.Order, $"{command} at ({target.X},{target.Y})"));
        using var host = new CheckpointHost(world);
        await host.AdvanceAndCheckSaved();

        var reached = world.Agents.Single(person => person.AgentId == actor).Position;
        Assert.NotEqual(origin, reached);
        Assert.True(state.Map.IsPassable(reached));
        Assert.InRange(state.Map.FootDistance(reached, target), 0, 1);
        var checkpoint = world.ExportState();
        Assert.Contains(checkpoint.Knowledge!.Facts, fact => fact.OwnerId == actor && fact.Position == reached &&
            fact.Acquisition == "firsthand");
        if (site == "land")
            Assert.Contains(checkpoint.Knowledge.Facts, fact => fact.OwnerId == actor && fact.Position == target &&
                fact.Acquisition == "firsthand");
        else
            Assert.DoesNotContain(checkpoint.Knowledge.Facts, fact => fact.OwnerId == actor && fact.Position == target);
        Assert.All(checkpoint.Knowledge.Facts, fact => Assert.True(state.Map.IsPassable(fact.Position)));

        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(host.Saved()), Providers);
        Assert.Equal(host.Saved(), WorldRuntimeCodec.Encode(replay.ExportState()));
        using var replayHost = new CheckpointHost(replay);
        world.Pause();
        replay.Pause();
        world.Resume();
        replay.Resume();
        for (var step = 0; step < 3; step++)
        {
            await host.AdvanceAndCheckSaved();
            await replayHost.AdvanceAndCheckSaved();
            Assert.Equal(host.Saved(), replayHost.Saved());
        }
        var order = Assert.Single(world.ExportState().Instructions!, item => item.InstructionId == receipt.InstructionId).Task!;
        Assert.Equal(("blocked", 0), (order.Status, order.CompletedUnits));
        Assert.NotEmpty(order.BlockedReason!);
        Assert.Null(order.ShelterBinding);
        Assert.Null(order.ShelterCompletion);
        Assert.Null(order.LastEffectId);
    }

    private static (GridPoint Origin, GridPoint Target) InspectionSite(WorldRuntimeState state, string actor, string site)
    {
        // Use actual generated terrain and a free two-step approach rather
        // than depending on a particular coast or a forced edge strip.
        var occupied = state.Agents.Where(person => person.AgentId != actor)
            .Select(person => person.Position).ToHashSet();
        var buildings = state.WorldSimulation!.Buildings.SelectMany(building => WorldContentSimulationRules.Footprint(
            state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building)).ToHashSet();
        foreach (var target in state.Map.Tiles.Select(tile => tile.Position)
            .Where(point => !occupied.Contains(point) && !buildings.Contains(point) && (site switch
            {
                "ocean" => state.Map.TerrainKindAt(point) == TerrainKind.Ocean,
                "land" => state.Map.IsPassable(point),
                _ => !state.Map.IsPassable(point) && state.Map.TerrainKindAt(point) != TerrainKind.Ocean,
            })))
        {
            foreach (var shore in state.Map.Tiles.Select(tile => tile.Position).Where(point =>
                state.Map.FootDistance(point, target) == 1 && state.Map.IsPassable(point) &&
                !occupied.Contains(point) && !buildings.Contains(point)))
            {
                foreach (var origin in state.Map.FootNeighbors(shore).Where(point =>
                    state.Map.FootDistance(point, target) == 2 && !occupied.Contains(point) && !buildings.Contains(point) &&
                    (!state.Map.IsDiagonalFootStep(point, shore) ||
                        !occupied.Contains(new(point.X, shore.Y)) && !occupied.Contains(new(shore.X, point.Y)))))
                    return (origin, target);
            }
        }
        throw new InvalidOperationException("The generated fixture needs a site with a free approach.");
    }

    private sealed class CheckpointHost : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("clanker-shelter-inspection-checkpoint-");
        private readonly WorldRuntime world;
        private readonly WorldRuntimeService service;
        private readonly RecordingLogger<WorldRuntimeService> logger = new();
        private readonly WorldStateFile file;

        public CheckpointHost(WorldRuntime world)
        {
            this.world = world;
            file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            file.Save(world);
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(5));
            presence.RecordAuthenticatedReconnect("test-owner");
            service = new WorldRuntimeService(world, file, presence, logger);
        }

        public byte[] Saved() => File.ReadAllBytes(file.Path);

        public async Task AdvanceAndCheckSaved()
        {
            Assert.True(await service.TryAdvanceOnceAsync(), string.Join("\n", logger.Messages));
            Assert.False(world.Society.IsPaused);
            Assert.Equal(world.WorldTick, WorldRuntimeCodec.Decode(Saved()).Society.Society.WorldTick);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), Saved());
        }

        public void Dispose()
        {
            service.Dispose();
            directory.Delete(recursive: true);
        }
    }
}
