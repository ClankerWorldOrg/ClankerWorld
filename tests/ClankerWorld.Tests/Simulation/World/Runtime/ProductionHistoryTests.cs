using System.Text.Json;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Saves;
using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ProductionHistoryTests
{
    private static readonly Lazy<Task<byte[]>> NativeHistory = new(() => BuildNativeHistory("cloth"));
    private static readonly Lazy<Task<byte[]>> NativeFoodHistory = new(() => BuildNativeHistory("fruit"));

    [Fact]
    public async Task CompactedPaidJobsKeepTheirProofAndCurrentWorkAcrossReloadAndReplay()
    {
        var state = WorldRuntimeCodec.Decode(await NativeHistory.Value);
        using var world = WorldRuntime.Restore(state, _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.Equal(36, world.ExportState().WorldSimulation!.ProductionJobs.Count);
        var directory = Directory.CreateTempSubdirectory("clanker-production-history-");
        try
        {
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"), _ => new ActionCoverageRecorder(chooseIdle: true));
            var inventory = JsonSerializer.Serialize(state.Society.Society.Inventory);
            var inspection = new OwnerWorldObservationStore(world).GetSnapshot().ProductionJobs;
            Assert.Equal(32, inspection.Count);
            var visible = JsonSerializer.Serialize(inspection);
            Assert.True(file.Save(world));
            var compacted = world.ExportState();
            Assert.Equal(33, compacted.WorldSimulation!.ProductionJobs.Count);
            Assert.Equal(3, compacted.WorldSimulation.ArchivedProductionJobCount);
            Assert.Equal(inventory, JsonSerializer.Serialize(compacted.Society.Society.Inventory));
            Assert.Equal(visible, JsonSerializer.Serialize(new OwnerWorldObservationStore(world).GetSnapshot().ProductionJobs));
            Assert.Contains(compacted.WorldSimulation.ProductionJobs, job =>
                job.JobId == compacted.Knowledge!.Recipes.Single().SourceProductionJobId);
            var bytes = WorldRuntimeCodec.Encode(compacted);
            Assert.Equal(bytes, File.ReadAllBytes(file.Path));
            var archive = Path.Combine(file.Path + ".history", compacted.HistoryArchiveHead + ".json");
            var segment = JsonSerializer.Deserialize<WorldHistorySegment>(File.ReadAllBytes(archive))!;
            Assert.Equal(JsonSerializer.Serialize(state.WorldSimulation!.ProductionJobs.Skip(1).Take(3)),
                segment.Streams.Single(batch => batch.Name == "production-jobs").Events.GetRawText());
            var worker = state.WorldSimulation.ProductionJobs[0];
            var started = world.StartProduction(worker.RecipeId, worker.BuildingInstanceId, worker.WorkerId);
            Assert.True(started.Applied, started.Failure);
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(3, world.WorldSimulation.ArchivedProductionJobCount);
            Assert.True(file.Save(world));
            Assert.Equal(4, world.WorldSimulation.ArchivedProductionJobCount);
            started = world.StartProduction(worker.RecipeId, worker.BuildingInstanceId, worker.WorkerId);
            Assert.True(started.Applied, started.Failure);
            var running = world.WorldSimulation.ProductionJobs.Single(job => job.State == WorldProductionJobState.Running);
            Assert.Equal(33, new OwnerWorldObservationStore(world).GetSnapshot().ProductionJobs.Count);
            // Use the native input-hold operation for a controlled pause.
            world.PersistCheckpoint(saved => saved with
            {
                WorldSimulation = saved.WorldSimulation! with
                {
                    ProductionJobs = saved.WorldSimulation.ProductionJobs.Select(job => job.JobId == running.JobId
                        ? job with { State = WorldProductionJobState.Paused, PausedAtTick = saved.Society.Society.WorldTick } : job).ToArray(),
                },
                Society = saved.Society with
                {
                    Society = saved.Society.Society with
                    { Inventory = InventoryAuthority.HoldReservations(saved.Society.Society.Inventory, running.InputReservationIds) }
                },
            });
            file.Save(world);
            Assert.Equal(33, new OwnerWorldObservationStore(world).GetSnapshot().ProductionJobs.Count);
            bytes = WorldRuntimeCodec.Encode(world.ExportState());
            using var loaded = file.LoadOrCreate(state.WorldSeed);
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(loaded.ExportState()));
            Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await loaded.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(loaded.ExportState()));
            File.WriteAllText(archive, "corrupt");
            Assert.Throws<InvalidDataException>(() => file.LoadOrCreate(state.WorldSeed));
            Assert.Equal(bytes, File.ReadAllBytes(file.Path));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedArchiveOrCheckpointWritesKeepAllPaidJobs(bool failCheckpoint)
    {
        var original = await NativeHistory.Value;
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(original), _ => new ActionCoverageRecorder(chooseIdle: true));
        var directory = Directory.CreateTempSubdirectory("clanker-production-failure-");
        try
        {
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            File.WriteAllBytes(file.Path, original);
            if (failCheckpoint)
            {
                File.Move(file.Path, file.Path + ".previous");
                Directory.CreateDirectory(file.Path);
            }
            else File.WriteAllText(file.Path + ".history", "block archive creation");
            Assert.Throws<IOException>(() => file.Save(world));
            Assert.Equal(original, WorldRuntimeCodec.Encode(world.ExportState()));
            Assert.Equal(original, File.ReadAllBytes(failCheckpoint ? file.Path + ".previous" : file.Path));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task AReferenceToAnOlderProjectsJobDoesNotBecomeMissingDuringCompaction()
    {
        var state = WorldRuntimeCodec.Decode(await NativeHistory.Value);
        var job = state.WorldSimulation!.ProductionJobs[1];
        var recipe = state.WorldContent!.Recipes.Single(recipe => recipe.CanonicalId == job.RecipeId);
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == job.WorkerId
            ? person with { Project = new AgentProject("build:recipe:" + job.RecipeId, recipe.DisplayName, 0, "waiting", 10, JobId: job.JobId) }
            : person).ToArray()
        };
        using var world = WorldRuntime.Restore(state);
        var plan = WorldHistory.Prepare(world.ExportState());
        Assert.Contains(job, plan.State.WorldSimulation!.ProductionJobs);
        using var reloaded = WorldRuntime.Restore(plan.State with { HistoryArchiveHead = new string('a', 64) });
        Assert.Equal(job.JobId, reloaded.ExportState().Agents.Single(person => person.AgentId == job.WorkerId).Project!.JobId);
    }

    [Fact]
    public async Task ATransferredOlderFoodOutputKeepsItsCookedOriginAfterSaving()
    {
        var state = WorldRuntimeCodec.Decode(await NativeFoodHistory.Value);
        var older = state.WorldSimulation!.ProductionJobs[1];
        var original = state.Society.Society.Inventory.Lots.Single(lot =>
            lot.Id == DerivedId.Create(older.JobId + ":output:00"));
        var inventory = InventoryAuthority.Transfer(state.Society.Society.Inventory, "history-food-transfer",
            original.OwnerId, older.WorkerId, original.Id, 1, "test_food_transfer");
        state = FarmFieldTests.WithInventory(state, inventory) with
        { Agents = state.Agents.Select(person => person.AgentId == older.WorkerId ? person with { FullnessBasisPoints = 3_000 } : person).ToArray() };
        using var world = WorldRuntime.Restore(state, _ => new EatingProvider());
        var directory = Directory.CreateTempSubdirectory("clanker-food-job-proof-");
        try
        {
            var file = new WorldStateFile(Path.Combine(directory.FullName, "world.json"));
            file.Save(world);
            Assert.Contains(older, world.WorldSimulation.ProductionJobs);
            Assert.Equal(32, new OwnerWorldObservationStore(world).GetSnapshot().ProductionJobs.Count);
            var order = world.SubmitInstruction(new("eat-history-fruit", "owner:test", older.WorkerId,
                OwnerInstructionKind.Order, "eat one fruit"));
            for (var tick = 0; tick < 4 && world.ExportState().Instructions!.Single(item => item.InstructionId == order.InstructionId).Task!.Status != "finished"; tick++)
                Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            var outcome = world.ExportState().Instructions!.Single(item => item.InstructionId == order.InstructionId).Task!;
            Assert.True(outcome.Status == "finished", $"{outcome.Status}: {outcome.BlockedReason}; fullness {world.ExportState().Agents.Single(person => person.AgentId == older.WorkerId).FullnessBasisPoints}");
            Assert.Equal("cooked", world.ExportState().Agents.Single(person => person.AgentId == older.WorkerId).Survival!.LastMealKind);
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed class EatingProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var selected = request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id is "consume_food" or "collect_shared_food")?.Id ?? "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }

    internal static async Task<byte[]> BuildNativeHistory(string kind, WorldRuntimeState? initial = null)
    {
        using var generated = NormalPathWorld.CreateGenerated("paid-production-history", _ => new ActionCoverageRecorder(chooseIdle: true));
        var state = initial ?? generated.ExportState();
        var previousCount = state.WorldSimulation!.ProductionJobs.Count;
        var building = state.WorldSimulation!.Buildings.First(item => item.HouseholdId is not null &&
            state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == item.DefinitionId).Tags.Contains("house"));
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == building.HouseholdId &&
            person.AgeBand == SocietyAgeBand.Adult).Id;
        // Control only supplies and one-tick recipe duration. Import, reservation,
        // material consumption, ownership, outputs and knowledge use native paths.
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "production-history-stock", "wood", building.HouseholdId!, 64, storageBuildingId: building.InstanceId);
        state = FarmFieldTests.WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? building.Position : person.Position,
                FullnessBasisPoints = 10_000,
            }).ToArray(),
        };
        using var world = WorldRuntime.Restore(state, _ => new ActionCoverageRecorder(chooseIdle: true));
        var package = ContentIdFixture.Package(building.DefinitionId, kind);
        world.ProposeContent(package);
        world.ValidateContent(package.PackageId, world.ResolveContent(package.PackageId));
        world.ApproveContent(package.PackageId);
        world.StageContent(package.PackageId);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var recipe = world.WorldContent.Recipes.Single(item => item.PackageDigest == package.PackageDigest);
        for (var index = 0; index < 36; index++)
        {
            var started = world.StartProduction(recipe.CanonicalId, building.InstanceId, actor);
            Assert.True(started.Applied, started.Failure);
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        }
        Assert.Equal(previousCount + 36, world.WorldSimulation.ProductionJobs.Count);
        Assert.All(world.WorldSimulation.ProductionJobs, job =>
        {
            Assert.Equal(WorldProductionJobState.Completed, job.State);
            Assert.Equal(building.HouseholdId, job.OwnerId);
            Assert.All(job.InputReservationIds, id => Assert.Equal(InventoryReservationState.Completed,
                world.Society.Inventory.GetReservation(id).State));
        });
        return WorldRuntimeCodec.Encode(world.ExportState());
    }
}
