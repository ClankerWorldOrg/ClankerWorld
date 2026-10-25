using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TreeSeedCarryCapacityTests
{
    [Theory]
    [InlineData(8, false, "tree_seed", false)]
    [InlineData(7, false, "tree_seed", true)]
    [InlineData(7, true, "tree_seed", true)]
    [InlineData(8, false, "orchard_seed", false)]
    public async Task SharedSeedsNeedCollectionRoomButCarriedSeedsCanPlantWithFullCargo(
        int stones, bool carriedSeed, string kind, bool canPlant)
    {
        using var generated = NormalPathWorld.CreateGenerated("audit-town-invariants", _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var state = generated.ExportState();
        var farmhouse = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-farmhouse");
        var household = farmhouse.HouseholdId!;
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == household).Id;
        var inventory = state.Society.Society.Inventory with { Lots = [], Reservations = [] };
        inventory = InventoryAuthority.AddLot(inventory, "cargo-stone", "stone", actor, stones);
        inventory = InventoryAuthority.AddLot(inventory, "planting-seed", kind, carriedSeed ? actor : household, 1,
            storageBuildingId: carriedSeed ? null : farmhouse.InstanceId);
        var prepared = RawInventoryTestState.WithInventory(state, inventory) with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Agents = state.Agents.Select(person => person with
            {
                Equipment = null,
                Position = person.AgentId == actor ? farmhouse.Position : person.Position,
                FullnessBasisPoints = 9_000,
            }).ToArray(),
        };
        var choice = kind == TreeGrowthRules.TreeSeedItem ? "plant_tree" : "plant_orchard";
        var policy = new PlantingProvider(choice);
        IDecisionProvider Provider(string id) => id == actor ? policy : new ActionCoverageRecorder(chooseIdle: true);
        var original = WorldRuntimeCodec.Encode(prepared);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(original), Provider);
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(original, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 40 && !world.ExportState().Events.Any(item => item.Kind == "tree_planted"); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        Assert.NotEmpty(policy.Offered);
        Assert.Equal(canPlant, policy.Offered.Any(candidates => candidates.Contains(choice)));
        var final = world.ExportState();
        var planted = final.Events.Where(item => item.Kind == "tree_planted").ToArray();
        Assert.Equal(canPlant ? 1 : 0, planted.Length);
        Assert.Equal(stones, world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "stone").Sum(lot => lot.Quantity));
        Assert.Equal(canPlant ? 0 : 1, world.Society.Inventory.Lots.Where(lot => lot.ItemKind == kind).Sum(lot => lot.Quantity));
        if (canPlant)
        {
            Assert.StartsWith(actor + ":", planted[0].Detail, StringComparison.Ordinal);
            var sapling = Assert.Single(final.Map.Resources, resource => resource.Id.StartsWith(TreeGrowthRules.PlantedTreeIdPrefix, StringComparison.Ordinal));
            Assert.True(world.WorldSystems.Ecology.GetResource(sapling.Id).IsPlanted);
        }
        else
        {
            var seed = world.Society.Inventory.GetLot("planting-seed");
            Assert.Equal(household, seed.OwnerId);
            Assert.Equal(farmhouse.InstanceId, seed.StorageBuildingId);
        }
        world.Validate();
        var saved = WorldRuntimeCodec.Encode(final);
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), Provider);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(reload.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await reload.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reload.ExportState()));
    }

    private sealed class PlantingProvider(string choice) : IDecisionProvider
    {
        public ConcurrentQueue<string[]> Offered { get; } = new();
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            Offered.Enqueue(observation.Candidates.Select(candidate => candidate.Id).ToArray());
            var selected = observation.Candidates.FirstOrDefault(candidate => candidate.Id == choice) ??
                observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, selected.Id, 1,
                observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}
