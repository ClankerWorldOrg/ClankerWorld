using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed partial class PickupReturnJourneyTests
{
    [Theory]
    [InlineData(false, 4, true)]
    [InlineData(false, 3, true)]
    [InlineData(false, 4, false)]
    [InlineData(true, 4, true)]
    [InlineData(true, 3, true)]
    [InlineData(true, 4, false)]
    public async Task PlantingOrderKeepsPersonalAndHouseholdSeedReturnsPossible(bool personal, int stones, bool swim)
    {
        var (state, actor, home) = await Generated();
        var destination = new GridPoint(107, 64);
        var source = swim ? new GridPoint(103, 47) : new GridPoint(106, 63);
        Routes(state, source, destination, swim);
        var inventory = Empty(state);
        inventory = InventoryAuthority.AddLot(inventory, "pickup-stones", "stone", actor, stones);
        inventory = InventoryAuthority.AddLot(inventory, "pickup-seed", "tree_seed", personal ? actor : home, 1,
            groundPosition: new(source.X, source.Y));
        state = Prepare(state, inventory, actor, source);
        using var world = Restore(state, actor, new Choices());
        var receipt = world.SubmitInstruction(new("return-plant", "owner:test", actor, OwnerInstructionKind.MustDo,
            "plant a conifer tree at 107,64"));
        var blocked = swim && stones == 4;
        await AdvanceUntil(world, () => world.ExportState().Instructions!.Single(item => item.InstructionId == receipt.InstructionId).Order!.Status == "finished", blocked ? 4 : 120);
        var order = world.ExportState().Instructions!.Single(item => item.InstructionId == receipt.InstructionId).Order!;
        if (blocked)
        {
            Assert.Equal("blocked", order.Status);
            Assert.Equal(0, order.CompletedUnits);
            Assert.Equal((personal ? actor : home, 1, (string?)null, new InventoryGroundPosition(source.X, source.Y)),
                (world.Society.Inventory.GetLot("pickup-seed").OwnerId, world.Society.Inventory.GetLot("pickup-seed").Quantity,
                    world.Society.Inventory.GetLot("pickup-seed").CarrierId, world.Society.Inventory.GetLot("pickup-seed").GroundPosition));
        }
        else
        {
            Assert.Equal(("finished", 1), (order.Status, order.CompletedUnits));
            Assert.Single(world.ExportState().Events, item => item.Kind == "tree_planted");
            Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == "pickup-seed");
        }
        Assert.Equal(stones, world.Society.Inventory.GetLot("pickup-stones").Quantity);
        Checkpoint(world, actor);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public async Task AutonomousReplantingBindsTheSeedToTheSelectedStump(int stones, bool swim)
    {
        var (state, actor, home) = await Generated();
        var tree = state.Map.Resources.Single(item => item.Position == new GridPoint(111, 62) && item.TreeKind is not null);
        var source = swim ? new GridPoint(105, 49) : new GridPoint(110, 61);
        Routes(state, source, tree.Position, swim);
        var inventory = InventoryAuthority.AddLot(InventoryAuthority.AddLot(Empty(state), "pickup-stones", "stone", actor, stones),
            "pickup-seed", "tree_seed", home, 1, groundPosition: new(source.X, source.Y));
        state = Prepare(state, inventory, actor, source);
        var stump = state.WorldSystems!.Ecology.GetResource(tree.Id) with
        { Quantity = 0, State = EcologyResourceState.Regenerating, NextRegenerationDay = 100 };
        state = state with
        {
            Resources = state.Resources.Select(item => item.ResourceId == tree.Id ? item with { State = ResourceState.Depleted } : item).ToArray(),
            WorldSystems = state.WorldSystems with
            {
                Ecology = state.WorldSystems.Ecology with
                { Resources = state.WorldSystems.Ecology.Resources.Select(item => item.Id == tree.Id ? stump : item).ToArray() }
            },
        };
        var chooser = new Choices("replant_tree");
        using var world = Restore(state, actor, chooser);
        var blocked = swim && stones == 4;
        await AdvanceUntil(world, () => world.ExportState().Events.Any(item => item.Kind == "tree_replanted"), blocked ? 4 : 80);
        Assert.Equal(!blocked, world.WorldSystems.Ecology.GetResource(tree.Id).IsPlanted);
        if (blocked)
        {
            Assert.DoesNotContain(chooser.Offered, id => id == "replant_tree");
            Assert.Null(world.Society.Inventory.GetLot("pickup-seed").CarrierId);
            Assert.Equal(home, world.Society.Inventory.GetLot("pickup-seed").OwnerId);
        }
        else Assert.Single(world.ExportState().Events, item => item.Kind == "tree_replanted");
        Assert.Equal(stones, world.Society.Inventory.GetLot("pickup-stones").Quantity);
        Checkpoint(world, actor);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public async Task GarmentRepairReturnsClothToTheActualTailor(int stones, bool swim)
    {
        var (state, shopId) = TailorTestWorld.Create("cart-set-unfinished", 0);
        var shop = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == shopId);
        var home = shop.HouseholdId!;
        var actor = state.Society.Society.Agents.First(item => item.HouseholdId == home).Id;
        var source = swim ? new GridPoint(105, 49) : state.Map.FootNeighbors(shop.Position).First(state.Map.IsBuildable);
        Routes(state, source, shop.Position, swim);
        var inventory = InventoryAuthority.AddLot(Empty(state), "pickup-stones", "stone", actor, stones);
        inventory = InventoryAuthority.WearSingleUnit(InventoryAuthority.AddLot(inventory, "pickup-coat", "padded_coat", actor, 1), "pickup-coat", 7_000);
        inventory = InventoryAuthority.AddLot(inventory, "pickup-cloth", "cloth", home, 1, groundPosition: new(source.X, source.Y));
        state = Prepare(state, inventory, actor, source) with
        { Agents = Prepare(state, inventory, actor, source).Agents.Select(item => item.AgentId == actor ? item with { Equipment = new(ClothingLotId: "pickup-coat") } : item).ToArray() };
        using var world = Restore(state, actor, new Choices());
        var receipt = world.SubmitInstruction(new("return-coat", "owner:test", actor, OwnerInstructionKind.MustDo, "Repair 1 padded coat"));
        var blocked = swim && stones == 4;
        await AdvanceUntil(world, () => world.Society.Inventory.GetLot("pickup-coat").ConditionBasisPoints == 9_000, blocked ? 4 : 100);
        Assert.Equal(blocked ? 3_000 : 9_000, world.Society.Inventory.GetLot("pickup-coat").ConditionBasisPoints);
        var order = world.ExportState().Instructions!.Single(item => item.InstructionId == receipt.InstructionId).Order!;
        Assert.Equal(blocked ? 0 : 1, order.CompletedUnits);
        if (blocked) Assert.Equal((home, 1, (string?)null), (world.Society.Inventory.GetLot("pickup-cloth").OwnerId,
            world.Society.Inventory.GetLot("pickup-cloth").Quantity, world.Society.Inventory.GetLot("pickup-cloth").CarrierId));
        else Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == "pickup-cloth");
        Assert.Equal(stones, world.Society.Inventory.GetLot("pickup-stones").Quantity);
        Checkpoint(world, actor);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public async Task CartBuildFittingsRespectTheLoadedBlacksmithReturn(int wood, bool swim)
    {
        var (state, actor, home) = await Generated();
        var smith = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == "first-town-blacksmith");
        var source = swim ? new GridPoint(105, 49) : new GridPoint(110, 59);
        Routes(state, source, smith.Position, swim);
        var inventory = InventoryAuthority.AddLot(Empty(state), "pickup-wood", "wood", actor, wood);
        inventory = InventoryAuthority.AddLot(inventory, "pickup-fittings", "iron_fittings", home, 2, groundPosition: new(source.X, source.Y));
        inventory = InventoryAuthority.AddLot(inventory, "remaining-wood", "wood", home, 4 - wood, groundPosition: new(smith.Position.X, smith.Position.Y));
        inventory = InventoryAuthority.AddLot(inventory, "remaining-rope", "rope", home, 1, groundPosition: new(smith.Position.X, smith.Position.Y));
        state = Prepare(state, inventory, actor, source);
        var chooser = new Choices("collect_handcart_material:iron_fittings", "collect_handcart_material:", "build:recipe:" + state.WorldContent!.Recipes.Single(item => item.LocalId == "handcart").CanonicalId);
        using var world = Restore(state, actor, chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True(PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, actor, null) <= (swim ? 4 : 8));
        Assert.Equal(2, world.Society.Inventory.Lots.Where(item => item.ItemKind == "iron_fittings").Sum(item => item.Quantity));
        if (!swim || wood == 2)
        {
            await AdvanceUntil(world, () => world.Society.Inventory.Lots.Any(item => item.ItemKind == InventoryContainerRules.Handcart && item.OwnerId == actor), 120);
            Assert.Single(world.Society.Inventory.Lots, item => item.ItemKind == InventoryContainerRules.Handcart && item.OwnerId == actor);
        }
        Checkpoint(world, actor);
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task ToolRepairAdmissionKeepsFeasibleCollectionsAndExplainsBlockedReturns(int stones, bool swim)
    {
        var (state, actor, home) = await Generated();
        var smith = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == "first-town-blacksmith");
        var source = swim ? new GridPoint(105, 49) : new GridPoint(110, 59);
        Routes(state, source, smith.Position, swim);
        var inventory = InventoryAuthority.WearSingleUnit(InventoryAuthority.AddLot(Empty(state), "pickup-axe", "iron_axe", actor, 1), "pickup-axe", 7_000);
        inventory = InventoryAuthority.AddLot(inventory, "pickup-stones", "stone", actor, stones);
        inventory = InventoryAuthority.AddLot(inventory, "pickup-iron", "iron", home, 1, groundPosition: new(source.X, source.Y));
        inventory = InventoryAuthority.AddLot(inventory, "pickup-wood", "wood", home, 1, groundPosition: new(smith.Position.X, smith.Position.Y));
        state = Prepare(state, inventory, actor, smith.Position);
        using var world = Restore(state, actor, new Choices());
        var receipt = world.SubmitInstruction(new("return-axe", "owner:test", actor, OwnerInstructionKind.MustDo, "repair my iron axe"));
        // Two stones can now finish by collecting iron before local wood (#1716).
        // Three stones leave no room for an iron pickup across the water.
        var blocked = swim && stones == 3;
        await AdvanceUntil(world, () => world.Society.Inventory.GetLot("pickup-axe").ConditionBasisPoints == 10_000, blocked ? 4 : 120);
        var order = world.ExportState().Instructions!.Single(item => item.InstructionId == receipt.InstructionId).Order!;
        Assert.Equal(blocked ? "blocked" : "finished", order.Status);
        Assert.Equal(blocked ? 0 : 1, order.CompletedUnits);
        Assert.Equal(blocked ? 3_000 : 10_000, world.Society.Inventory.GetLot("pickup-axe").ConditionBasisPoints);
        if (blocked) Assert.Equal((home, 1, (string?)null), (world.Society.Inventory.GetLot("pickup-iron").OwnerId,
            world.Society.Inventory.GetLot("pickup-iron").Quantity, world.Society.Inventory.GetLot("pickup-iron").CarrierId));
        Assert.Equal(stones, world.Society.Inventory.GetLot("pickup-stones").Quantity);
        Checkpoint(world, actor);
    }

    private static async Task<(WorldRuntimeState State, string Actor, string Home)> Generated()
    {
        using var generated = NormalPathWorld.CreateGenerated("cart-set-unfinished", _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var state = generated.ExportState();
        var home = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == "first-town-blacksmith").HouseholdId!;
        return (state, state.Society.Society.Agents.First(item => item.HouseholdId == home).Id, home);
    }

    private static InventoryCheckpoint Empty(WorldRuntimeState state) => state.Society.Society.Inventory with { Lots = [], Reservations = [] };

    private static WorldRuntimeState Prepare(WorldRuntimeState state, InventoryCheckpoint inventory, string actor, GridPoint source) =>
        SettlementWeatherTestFixture.WithWeather(state, WeatherKind.Clear) with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(item => item with
            {
                Position = item.AgentId == actor ? source : item.Position,
                HungerBasisPoints = 10_000,
                Survival = new(),
                Project = null,
                LastDecisionContext = null,
                Equipment = null,
                TravelCooldownTicks = 0,
            }).ToArray(),
        };

    private static void Routes(WorldRuntimeState state, GridPoint source, GridPoint destination, bool swim)
    {
        Assert.True(state.Map.IsBuildable(source));
        Assert.Equal(!swim, state.Map.IsReachableOnFoot(source, destination));
        Assert.True(SwimmingRules.IsReachable(state.Map, source, destination));
    }

    private static WorldRuntime Restore(WorldRuntimeState state, string actor, Choices choices) =>
        WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), id => id == actor ? choices : new Choices());

    private static async Task AdvanceUntil(WorldRuntime world, Func<bool> done, int limit)
    {
        for (var tick = 0; tick < limit && !done(); tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
    }

    private static void Checkpoint(WorldRuntime world, string actor)
    {
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var loaded = Restore(WorldRuntimeCodec.Decode(bytes), actor, new Choices());
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(loaded.ExportState()));
        world.Validate(); loaded.Validate();
    }

    private sealed class Choices(params string[] prefixes) : IDecisionProvider
    {
        public ConcurrentQueue<string> Offered { get; } = new();
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            foreach (var candidate in observation.Candidates) Offered.Enqueue(candidate.Id);
            var selected = prefixes.Select(prefix => observation.Candidates.FirstOrDefault(item => item.Id.StartsWith(prefix, StringComparison.Ordinal)))
                .FirstOrDefault(item => item is not null) ?? observation.Candidates.Single(item => item.Id == "safe_idle");
            return new(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, selected.Id, 1,
                observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected.Id ? 1d : 0d)));
        }
    }
}
