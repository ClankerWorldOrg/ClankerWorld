using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class WorldRuntimeTests
{
    [Theory]
    [InlineData("completed")]
    public async Task RollbackPreservesRecordedProjectReferences(string stage)
    {
        using var seed = new WorldRuntime("rollback-project",
            _ => new CountingSelectingProvider(DecisionProviderKind.Deterministic, chooseIdle: true));
        var (package, building, _) = MaterialPackage(2);
        Activate(seed, package);
        await seed.AdvanceOneTickAsync();
        var state = seed.ExportState();
        var actor = state.Inhabitants[0].InhabitantId;
        state = state with
        {
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == actor ? person with
            {
                Project = new SettlementProject("build:building:" + building.CanonicalId, "Workshop", seed.WorldTick,
                    stage, stage == "completed" ? 10 : 0, LastTransitionTick: seed.WorldTick),
            } : person).ToArray(),
        };
        using var world = WorldRuntime.Restore(state);
        world.Pause();
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.Throws<InvalidOperationException>(() => world.RollbackContent(package.PackageId, "withdraw"));
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
    }

    [Theory]
    [InlineData(false)]
    public async Task RollbackWithCommittedProductionReferencesRejectsWithoutDeletingWorldState(bool completed)
    {
        var (world, usedPackage, _, jobId) = await WorldWithRollbackJob();
        using (world)
        {
            if (completed)
            {
                await world.AdvanceOneTickAsync();
                await world.AdvanceOneTickAsync();
            }
            world.Pause();
            var before = WorldRuntimeCodec.Encode(world.ExportState());
            var error = Assert.Throws<InvalidOperationException>(() => world.RollbackContent(usedPackage, "withdraw content"));
            Assert.Contains("migration", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(before),
                _ => new CountingSelectingProvider(DecisionProviderKind.Deterministic, chooseIdle: true));
            Assert.Equal(before, WorldRuntimeCodec.Encode(restored.ExportState()));
            restored.Resume();
            await restored.AdvanceOneTickAsync();
            await restored.AdvanceOneTickAsync();
            Assert.Equal(WorldProductionJobState.Completed, restored.WorldSimulation.ProductionJobs
                .Concat(restored.WorldSimulation.CropBuilds ?? []).Single(job => job.JobId == jobId).State);
        }
    }

    [Fact]
    public async Task WithdrawingUnusedPackageDoesNotCancelOtherPackagesWork()
    {
        var (world, _, unusedPackage, jobId) = await WorldWithRollbackJob();
        using (world)
        {
            var beforeInventory = world.Society.Inventory;
            var receipt = world.RollbackContent(unusedPackage, "unused package");
            Assert.Equal(ContentPackageLifecycle.Quarantined, receipt.Lifecycle);
            Assert.Equal(beforeInventory, world.Society.Inventory);
            Assert.Equal(WorldProductionJobState.Running, world.WorldSimulation.ProductionJobs
                .Concat(world.WorldSimulation.CropBuilds ?? []).Single(job => job.JobId == jobId).State);
            await world.AdvanceOneTickAsync();
            await world.AdvanceOneTickAsync();
            Assert.Equal(WorldProductionJobState.Completed, world.WorldSimulation.ProductionJobs
                .Concat(world.WorldSimulation.CropBuilds ?? []).Single(job => job.JobId == jobId).State);
            using var restored = WorldRuntime.Restore(world.ExportState());
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(restored.ExportState()));
        }
    }

    private static async Task<(WorldRuntime World, string Used, string Unused, string JobId)> WorldWithRollbackJob()
    {
        using var seed = new WorldRuntime("rollback-safety",
            _ => new CountingSelectingProvider(DecisionProviderKind.Deterministic, chooseIdle: true));
        var (workPackage, building, workRecipe) = MaterialPackage(2);
        var (cropPackage, _, _) = CropPackage();
        Activate(seed, workPackage);
        Activate(seed, cropPackage);
        await seed.AdvanceOneTickAsync();
        var state = seed.ExportState();
        var worker = state.Inhabitants[0];
        var site = state.Map.Tiles.First(tile => state.Map.IsPassable(tile.Position) &&
                !state.Map.MapObjects.Any(item => item.Position == tile.Position) &&
                !state.Map.Resources.Any(item => item.Position == tile.Position) &&
                !state.Inhabitants.Any(person => person.InhabitantId != worker.InhabitantId && person.Position == tile.Position)).Position;
        state = state with
        {
            Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == worker.InhabitantId ? person with { Position = site }
                : person.Position == site ? person with { Position = worker.Position } : person).ToArray(),
        };
        var world = WorldRuntime.Restore(state,
            _ => new CountingSelectingProvider(DecisionProviderKind.Deterministic, chooseIdle: true));
        var placed = world.PlaceBuilding("rollback-workshop", building.CanonicalId, site);
        Assert.True(placed.Applied, placed.Failure);
        var started = world.StartProduction(workRecipe.CanonicalId, placed.InstanceId, worker.InhabitantId);
        Assert.True(started.Applied, started.Failure);
        Assert.NotNull(started.JobId);
        return (world, workPackage.PackageId, cropPackage.PackageId, started.JobId);
    }
}
