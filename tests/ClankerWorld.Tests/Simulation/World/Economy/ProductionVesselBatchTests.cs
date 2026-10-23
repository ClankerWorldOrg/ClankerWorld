using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed partial class ToolMakingRequestTests
{
    [Theory]
    [InlineData("water_jug", 3)]
    [InlineData("storage_pot", 2)]
    [InlineData("handcart", 2)]
    [InlineData("wood", 3)]
    public async Task AcceptedVesselBatchesCompleteAsPhysicalItemsThroughTheHostAndReplay(string kind, int amount)
    {
        var (state, _, worker, shop) = Prepared();
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == worker
                ? person with { Position = shop.Position } : person).ToArray(),
        };
        if (kind == InventoryContainerRules.Handcart)
        {
            var inventory = InventoryAuthority.Transfer(state.Society.Society.Inventory, "cart-starting-wood",
                shop.HouseholdId!, worker, "smith-input", 3, "controlled-starting-cargo");
            state = state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
        }
        using var setup = WorldRuntime.Restore(state, _ => new RequestChoices(DecisionProviderKind.Deterministic));
        var packageId = "test-vessel-batch-" + kind + "-" + amount;
        var version = ContentVersion.Parse("1.0.0");
        var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(packageId)));
        var package = RecipePackage(packageId, new RecipeDefinition(digest, "make-batch", version, "Make a batch",
            [new("wood", 3)], [new(kind, amount)], 2, shop.DefinitionId, ["crafting"]));
        setup.ProposeContent(package);
        var resolution = setup.ResolveContent(package.PackageId);
        Assert.True(resolution.IsSuccess, resolution.Diagnostic);
        setup.ValidateContent(package.PackageId, resolution);
        setup.ApproveContent(package.PackageId);
        setup.StageContent(package.PackageId);
        Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        var recipe = Assert.Single(setup.WorldContent.Recipes, item => item.PackageDigest == package.PackageDigest);
        state = Roundtrip(setup);
        using var admitting = WorldRuntime.Restore(state, _ => new RequestChoices(DecisionProviderKind.Deterministic));
        var start = admitting.StartProduction(recipe.CanonicalId, shop.InstanceId, worker);
        Assert.True(start.Applied, start.Failure);
        var running = Assert.Single(admitting.WorldSimulation.ProductionJobs, job => job.JobId == start.JobId);
        Assert.Equal(WorldProductionJobState.Running, running.State);
        Assert.Equal(3, running.InputReservationIds.Sum(id => admitting.Society.Inventory.GetReservation(id).Quantity));
        state = Roundtrip(admitting);
        using var world = WorldRuntime.Restore(state, _ => new RequestChoices(DecisionProviderKind.Deterministic));
        using var replay = WorldRuntime.Restore(state, _ => new RequestChoices(DecisionProviderKind.Deterministic));
        using var host = new VesselBatchCheckpointHost(world);
        using var replayHost = new VesselBatchCheckpointHost(replay);
        world.Resume();
        replay.Resume();
        while (world.WorldTick + 1 < running.CompletionTick)
        {
            await host.Advance();
            await replayHost.Advance();
            Assert.Equal(host.Saved(), replayHost.Saved());
        }
        var beforeCompletion = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(beforeCompletion, WorldRuntimeCodec.Encode(world.ExportState()));
        await host.Advance();
        await replayHost.Advance();
        Assert.Equal(host.Saved(), replayHost.Saved());
        Assert.Equal(WorldProductionJobState.Completed,
            Assert.Single(world.WorldSimulation.ProductionJobs, job => job.JobId == start.JobId).State);
        var outputs = world.Society.Inventory.Lots.Where(lot => lot.Id.StartsWith(start.JobId + ":output:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(amount, outputs.Sum(lot => lot.Quantity));
        Assert.Equal(InventoryContainerRules.IsContainer(kind) ? amount : 1, outputs.Length);
        Assert.Equal(outputs.Length, outputs.Select(lot => lot.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(outputs, lot => lot.Id == start.JobId + ":output:00");
        Assert.All(outputs, lot =>
        {
            Assert.Equal(kind, lot.ItemKind);
            Assert.Equal(InventoryContainerRules.IsContainer(kind) ? 1 : amount, lot.Quantity);
            Assert.Equal(kind == InventoryContainerRules.Handcart ? worker : shop.HouseholdId, lot.OwnerId);
            Assert.Null(lot.ContainerLotId);
            if (kind == InventoryContainerRules.Handcart)
            {
                Assert.Null(lot.StorageBuildingId);
                Assert.Equal(new InventoryGroundPosition(shop.Position.X, shop.Position.Y), lot.GroundPosition);
            }
            else Assert.Equal(shop.InstanceId, lot.StorageBuildingId);
        });
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == "smith-input");
        Assert.All(running.InputReservationIds, id => Assert.Equal(InventoryReservationState.Completed,
            world.Society.Inventory.GetReservation(id).State));
        state = WorldRuntimeCodec.Decode(host.Saved());
        using var reload = WorldRuntime.Restore(state, _ => new RequestChoices(DecisionProviderKind.Deterministic));
        using var reloadHost = new VesselBatchCheckpointHost(reload);
        world.Pause();
        reload.Pause();
        world.Resume();
        reload.Resume();
        await host.Advance();
        await reloadHost.Advance();
        Assert.Equal(host.Saved(), reloadHost.Saved());
        Assert.Equal(amount, world.Society.Inventory.Lots.Where(lot => lot.Id.StartsWith(start.JobId + ":output:", StringComparison.Ordinal))
            .Sum(lot => lot.Quantity));
        _ = Roundtrip(world);
    }

    private sealed class VesselBatchCheckpointHost : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("clanker-vessel-batch-");
        private readonly WorldRuntime world;
        private readonly WorldStateFile file;
        private readonly WorldRuntimeService service;
        private readonly RecordingLogger<WorldRuntimeService> logger = new();

        public VesselBatchCheckpointHost(WorldRuntime world)
        {
            this.world = world;
            file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            file.Save(world);
            var presence = new OwnerClientPresenceLease(TimeSpan.FromMinutes(5));
            presence.RecordAuthenticatedReconnect("test-owner");
            service = new WorldRuntimeService(world, file, presence, logger);
        }

        public byte[] Saved() => File.ReadAllBytes(file.Path);

        public async Task Advance()
        {
            Assert.True(await service.TryAdvanceOnceAsync(), string.Join("\n", logger.Messages));
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), Saved());
        }

        public void Dispose()
        {
            service.Dispose();
            directory.Delete(recursive: true);
        }
    }
}
