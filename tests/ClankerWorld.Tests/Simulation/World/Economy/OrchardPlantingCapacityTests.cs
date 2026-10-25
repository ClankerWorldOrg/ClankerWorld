using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class OrchardPlantingCapacityTests
{
    [Theory]
    [InlineData(8, false, false)]
    [InlineData(7, false, true)]
    [InlineData(7, true, true)]
    public async Task OrchardPlantingNeedsRoomToCollectASharedSeedButUsesAnAlreadyCarriedSeedAtFullCapacity(
        int stone, bool carriedSeed, bool canPlant)
    {
        using var generated = NormalPathWorld.CreateGenerated("audit-town-invariants", _ => new Choices(false));
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var original = generated.ExportState();
        var farm = Assert.Single(original.WorldSimulation!.Buildings, building => generated.WorldContent.Buildings
            .Single(definition => definition.CanonicalId == building.DefinitionId).Tags.Contains("farmhouse"));
        var actor = original.Society.Society.Agents.First(person => person.HouseholdId == farm.HouseholdId).Id;
        var inventory = original.Society.Society.Inventory;
        Assert.DoesNotContain(inventory.Lots, lot => PersonalEquipmentRules.IsCarried(lot, actor));
        inventory = InventoryAuthority.AddLot(inventory, "planting-stone", "stone", actor, stone);
        inventory = InventoryAuthority.AddLot(inventory, "planting-seed", TreeGrowthRules.OrchardSeedItem,
            carriedSeed ? actor : farm.HouseholdId!, 1, storageBuildingId: carriedSeed ? null : farm.InstanceId);
        var initial = RawInventoryTestState.WithInventory(original, inventory) with
        {
            Agents = original.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? farm.Position : person.Position,
                FullnessBasisPoints = 9000,
                Survival = new(),
                Project = null,
                LastDecisionContext = null,
            }).ToArray(),
        };
        var choices = new Choices(true);
        using var world = Restore(initial, actor, choices);
        var physical = world.Agents.Single(person => person.AgentId == actor);
        Assert.Equal(canPlant && !carriedSeed ? 1 : 0,
            PersonalEquipmentRules.FreeCapacity(world.Society.Inventory, actor, physical.Equipment));
        for (var tick = 0; tick < 3; tick++) await Tick(world);
        Assert.Equal(canPlant, choices.Offered > 0);
        Assert.Equal(canPlant, choices.Selected > 0);

        var checkpoint = world.ExportState();
        using var replay = Restore(checkpoint, actor, new Choices(true));
        Assert.Equal(WorldRuntimeCodec.Encode(checkpoint), WorldRuntimeCodec.Encode(replay.ExportState()));
        for (var tick = 0; tick < (canPlant ? 30 : 3); tick++)
        {
            await Tick(world);
            await Tick(replay);
            if (world.ExportState().Events.Any(IsPlant)) break;
        }
        await Tick(world);
        await Tick(replay);
        var final = world.ExportState();
        Assert.Equal(WorldRuntimeCodec.Encode(final), WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.Equal(stone, world.Society.Inventory.GetLot("planting-stone").Quantity);
        Assert.Equal(canPlant ? 1 : 0, final.Events.Count(IsPlant));
        Assert.Equal(canPlant && !carriedSeed ? 1 : 0, final.Events.Count(item =>
            item.Kind == "equipment_collected" && item.Detail == actor + ":orchard_seed"));
        if (canPlant)
        {
            Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == "planting-seed");
            var planted = Assert.Single(final.Map.Resources, resource =>
                resource.Id.StartsWith(TreeGrowthRules.PlantedTreeIdPrefix, StringComparison.Ordinal));
            Assert.Equal(TreeGrowthRules.Orchard, planted.TreeKind);
            Assert.True(world.WorldSystems.Ecology.GetResource(planted.Id).IsPlanted);
        }
        else
        {
            var seed = world.Society.Inventory.GetLot("planting-seed");
            Assert.Equal(1, seed.Quantity);
            Assert.Equal(farm.HouseholdId, seed.OwnerId);
            Assert.Equal(farm.InstanceId, seed.StorageBuildingId);
            Assert.Equal(farm.Position, world.Agents.Single(person => person.AgentId == actor).Position);
        }
        using var loaded = Restore(final, actor, new Choices(true));
        Assert.Equal(WorldRuntimeCodec.Encode(final), WorldRuntimeCodec.Encode(loaded.ExportState()));

        bool IsPlant(WorldEventEntry item) => item.Kind == "tree_planted" &&
            item.Detail.StartsWith(actor + ":", StringComparison.Ordinal);
    }

    private static WorldRuntime Restore(WorldRuntimeState state, string actor, Choices choices)
    {
        var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == actor ? choices : new Choices(false));
        world.Validate();
        return world;
    }

    private static async Task Tick(WorldRuntime world)
    {
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        world.Validate();
    }

    private sealed class Choices(bool plant) : IDecisionProvider
    {
        public DecisionProviderKind Kind => plant ? DecisionProviderKind.LargeLanguageModel : DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public int Offered { get; private set; }
        public int Selected { get; private set; }
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var offered = request.Observation.Candidates.Any(candidate => candidate.Id == "plant_orchard");
            if (offered) Offered++;
            var selected = plant && offered ? "plant_orchard" : "safe_idle";
            if (selected == "plant_orchard") Selected++;
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId,
                request.Observation.AgentId, Kind, request.ProviderEpoch, request.Observation.RunEpoch,
                request.Observation.DecisionGeneration, request.Observation.ObservationDigest, selected, 1,
                new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
