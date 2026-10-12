using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class BlacksmithHarvestCapacityTests
{
    [Theory]
    [InlineData("stone_axe", false, 4)]
    [InlineData("iron_axe", false, 4)]
    [InlineData("stone_axe", true, 7)]
    [InlineData("iron_axe", true, 8)]
    public async Task UpgradedAxeCanHarvestAndDeliverTheLastTreeUnitAcrossReload(string tool, bool basket, int expectedWood)
    {
        using var setup = NormalPathWorld.CreateGenerated("smith-whole-load", _ => new HarvestChoices(false));
        Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        var state = setup.ExportState();
        var smith = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-blacksmith");
        var household = smith.HouseholdId!;
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == household).Id;
        var tree = state.Map.Resources.Where(resource => TreeGrowthRules.IsWoodTree(resource.TreeKind) &&
                state.Map.IsPassable(resource.Position) && state.Map.IsReachableOnFoot(smith.Position, resource.Position) &&
                state.Agents.All(person => person.AgentId == actor || person.Position != resource.Position))
            .OrderBy(resource => state.Map.FootDistance(smith.Position, resource.Position)).ThenBy(resource => resource.Id, StringComparer.Ordinal).First();
        var inventory = state.Society.Society.Inventory with { Lots = [], Reservations = [] };
        inventory = InventoryAuthority.AddLot(inventory, "harvest-axe", tool, actor, 1);
        if (basket) inventory = InventoryAuthority.AddLot(inventory, "harvest-basket", "basket", actor, 1);
        foreach (var input in state.WorldContent!.Recipes.Where(recipe => recipe.WorkstationBuildingId == smith.DefinitionId &&
                     !recipe.Outputs.Any(output => output.ResourceId == InventoryContainerRules.Handcart))
                     .SelectMany(recipe => recipe.Inputs).Where(input => input.ResourceId != "wood").GroupBy(input => input.ResourceId))
            inventory = InventoryAuthority.AddLot(inventory, "smith-input-" + input.Key, input.Key, household,
                input.Max(item => item.Amount) * 2, storageBuildingId: smith.InstanceId);
        state = FarmFieldTests.WithInventory(state, inventory) with
        {
            WorldSystems = state.WorldSystems! with
            {
                Ecology = state.WorldSystems.Ecology with
                {
                    Resources = state.WorldSystems.Ecology.Resources.Select(resource => resource.Id == tree.Id
                        ? resource with { Quantity = 1 } : resource).ToArray(),
                },
            },
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                Position = tree.Position,
                HungerBasisPoints = 10_000,
                Equipment = basket ? new PersonalEquipment(CarryAidLotId: "harvest-basket") : null,
                Project = null,
                LastDecisionContext = null,
                Survival = new SurvivalCondition(),
            } : person).ToArray(),
        };
        using var world = Restore(state);
        for (var tick = 0; tick < 12 && !world.ExportState().Events.Any(item => item.Kind == "material_gathered" &&
                 item.Detail == actor + ":wood:" + expectedWood); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "material_gathered" && item.Detail == actor + ":wood:" + expectedWood);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "tree_seed_collected" && item.Detail == actor + ":" + tree.Id + ":1");
        Assert.Equal((actor, 1, tool == "stone_axe" ? 8_750 : 9_000),
            (world.Society.Inventory.GetLot("harvest-axe").OwnerId, world.Society.Inventory.GetLot("harvest-axe").Quantity,
                world.Society.Inventory.GetLot("harvest-axe").ConditionBasisPoints));
        Assert.Equal(0, world.WorldSystems.Ecology.GetResource(tree.Id).Quantity);
        world.Validate();
        using var resumed = Restore(world.ExportState());
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(resumed.ExportState()));
        for (var tick = 0; tick < 40 && !Delivered(world); tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await resumed.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(resumed.ExportState()));
        }
        Assert.True(Delivered(world));
        Assert.Equal(expectedWood, world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "wood").Sum(lot => lot.Quantity));
        Assert.Contains(world.Society.Inventory.Lots, lot => lot.ItemKind == "wood" && lot.OwnerId == household &&
            lot.StorageBuildingId == smith.InstanceId && lot.Quantity > 0);
        Assert.Equal(1, world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "tree_seed").Sum(lot => lot.Quantity));
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "carrying_full" && item.Detail == actor);
        Assert.Equal(inventory.Reservations, world.Society.Inventory.Reservations);
        world.Validate();
        resumed.Validate();

        bool Delivered(WorldRuntime runtime) => runtime.ExportState().Events.Any(item => item.Kind == "smith_input_delivered" &&
            item.Detail.StartsWith(actor + ":", StringComparison.Ordinal));
        WorldRuntime Restore(WorldRuntimeState saved) => WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(saved)), id => new HarvestChoices(id == actor));
    }

    private sealed class HarvestChoices(bool active) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var selected = active ? request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id == "haul_smith_input") ??
                request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id == "gather_smith_input:wood") : null;
            selected ??= request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected.Id, 1, new Dictionary<string, double> { [selected.Id] = 1 }));
        }
    }
}
