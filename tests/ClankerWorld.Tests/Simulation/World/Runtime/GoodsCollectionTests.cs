using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class GoodsCollectionTests
{
    [Fact]
    public async Task CollectionUsesActualCarryRoomFreeVesselFamiliesAndCommittedCheckpointChanges()
    {
        using var generated = NormalPathWorld.CreateGenerated("goods-collection-boundaries", _ => new Choice());
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var state = generated.ExportState();
        var actor = state.Agents[0].AgentId;
        var household = PaidMarketWorld.HouseholdOf(state, actor);
        var house = PaidMarketWorld.HouseOf(state, household);
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor &&
                (lot.OwnerId != household || lot.ItemKind is not ("wood" or "water_jug" or "fresh_water"))).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "collect-carried-stone", "stone", actor, 5);
        inventory = InventoryAuthority.AddLot(inventory, "collect-partial-wood", "wood", household, 5, storageBuildingId: house.InstanceId);
        inventory = InventoryAuthority.Reserve(inventory, "collect-held-wood", household, "collect-partial-wood", 2, "other-work", inventory.WorldTick + 100);
        foreach (var (id, quantity) in new[] { ("broken", 2), ("reserved", 2), ("fitting", 1), ("oversize", 3) })
        {
            inventory = InventoryAuthority.AddLot(inventory, "collect-" + id + "-jug", "water_jug", household, 1, storageBuildingId: house.InstanceId);
            inventory = InventoryAuthority.AddLot(inventory, "collect-" + id + "-water", "fresh_water", household, quantity,
                storageBuildingId: house.InstanceId, containerLotId: "collect-" + id + "-jug");
        }
        inventory = InventoryAuthority.Reserve(inventory, "collect-held-water", household, "collect-reserved-water", 1, "other-work", inventory.WorldTick + 100);
        inventory = inventory with
        {
            Lots = inventory.Lots.Select(lot => lot.Id == "collect-broken-jug" ? lot with { ConditionBasisPoints = 0 } : lot).ToArray(),
        };
        state = Prepared(PaidMarketWorld.WithInventory(state, inventory), actor, house.Position);
        using var world = WorldRuntime.Restore(state, _ => new Choice());
        var request = new GoodsRequest(GoodsUse.Collect, actor, GoodsOwners.One(household),
            new GoodsKinds(["wood", "water_jug", "fresh_water"]), Explain: true);
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        var answer = world.FindGoods(request);
        Assert.Equal(new[] { ("collect-fitting-jug", 1, 2), ("collect-fitting-water", 1, 2), ("collect-partial-wood", 3, 3) },
            answer.Matches.Select(match => (match.Lot.Id, match.Quantity, match.MoveUnits)).ToArray());
        Assert.Contains(("collect-broken-jug", GoodsReason.Damaged), answer.Excluded);
        Assert.Contains(("collect-broken-water", GoodsReason.Vessel), answer.Excluded);
        Assert.Contains(("collect-reserved-jug", GoodsReason.Reservation), answer.Excluded);
        Assert.Contains(("collect-reserved-water", GoodsReason.Reservation), answer.Excluded);
        Assert.Contains(("collect-oversize-jug", GoodsReason.CarryRoom), answer.Excluded);
        Assert.Contains(("collect-oversize-water", GoodsReason.CarryRoom), answer.Excluded);
        var preparation = world.FindGoods(request with { Use = GoodsUse.ReachableHoldings });
        Assert.Contains(preparation.Matches, match => match.Lot.Id == "collect-oversize-water" && match.Quantity == 3);
        Assert.DoesNotContain(preparation.Matches, match => match.Lot.Id == "collect-broken-water");
        Assert.Equal(preparation.Matches, world.FindGoods(request with { Use = GoodsUse.ReachableHoldings, Explain = false }).Matches);
        Assert.Equal(answer.Matches, world.FindGoods(request with { Explain = false }).Matches);
        Assert.Equal(5, answer.Total);
        foreach (var match in answer.Matches)
        {
            Assert.Equal(match, world.RecheckGoods(request, match.Lot.Id));
            var moving = match.Root;
            var quantity = InventoryContainerRules.IsContainer(moving.ItemKind) ? 1 : match.Quantity;
            var operation = "collect-proof-" + match.Lot.Id;
            var accepted = InventoryAuthority.Relocate(inventory, operation,
                moving.Id, moving.OwnerId, quantity, carrierId: actor);
            var movedId = quantity < moving.Quantity ? moving.Id + "#move:" + operation : match.Lot.Id;
            Assert.True(PersonalEquipmentRules.IsPhysicallyCarried(accepted, accepted.GetLot(movedId), actor));
            if (quantity < moving.Quantity)
            {
                Assert.Equal((house.InstanceId, 2), (accepted.GetLot(moving.Id).StorageBuildingId, accepted.GetLot(moving.Id).Quantity));
                Assert.Equal(3, accepted.GetLot(movedId).Quantity);
            }
        }
        Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
        var changed = InventoryAuthority.Relocate(inventory, "other-housemate-collected", "collect-fitting-jug", household, 1,
            carrierId: state.Agents.Single(person => person.AgentId != actor && PaidMarketWorld.HouseholdOf(state, person.AgentId) == household).AgentId);
        using var next = WorldRuntime.Restore(PaidMarketWorld.WithInventory(state, changed), _ => new Choice());
        Assert.Null(next.RecheckGoods(request, "collect-fitting-jug"));
        Assert.Null(next.RecheckGoods(request, "collect-fitting-water"));
        Assert.Contains(("collect-fitting-jug", GoodsReason.Custody), next.FindGoods(request).Excluded);
        world.Validate();
        next.Validate();
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new Choice());
        Assert.Equal(answer.Matches, reload.FindGoods(request).Matches);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(reload.ExportState()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeToolPickupUsesTheSameBoundariesAndKeepsHouseholdOwnership(bool reserved)
    {
        using var generated = NormalPathWorld.CreateGenerated("goods-collection-tool", _ => new Choice());
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var state = generated.ExportState();
        var actor = state.Agents[0].AgentId;
        var home = PaidMarketWorld.HouseholdOf(state, actor);
        var house = PaidMarketWorld.HouseOf(state, home);
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor &&
                (lot.OwnerId != home || ToolProgressionRules.Find(lot.ItemKind) is not { Family: ToolFamily.Axe })).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "collect-household-axe", "wooden_axe", home, 1, storageBuildingId: house.InstanceId);
        if (reserved) inventory = InventoryAuthority.Reserve(inventory, "collect-axe-held", home, "collect-household-axe", 1, "other-work", inventory.WorldTick + 100);
        state = Prepared(PaidMarketWorld.WithInventory(state, inventory), actor, house.Position);
        var chooser = new Choice("collect_wooden_axe");
        using var world = WorldRuntime.Restore(state, id => id == actor ? chooser : new Choice());
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), id => id == actor ? new Choice("collect_wooden_axe") : new Choice());
        var request = new GoodsRequest(GoodsUse.Collect, actor, GoodsOwners.One(home), GoodsKinds.One("wooden_axe"));
        Assert.Equal(!reserved, world.RecheckGoods(request, "collect-household-axe") is not null);
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
        var samePlan = false;
        for (var tick = 0; tick < 4; tick++)
        {
            var step = await world.AdvanceOneTickAsync();
            Assert.True(step.Advanced);
            samePlan |= step.Decisions.Any(decision => decision.AgentId == actor &&
                chooser.Plan is not null && ReferenceEquals(decision.ActionPlan, chooser.Plan));
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        if (!reserved)
        {
            Assert.True(samePlan);
            Assert.Equal("collect-household-axe", Assert.IsType<ToolCollectionActionPlan>(chooser.Plan).Pickup.StockId);
        }
        var axe = world.Society.Inventory.GetLot("collect-household-axe");
        Assert.Equal((home, 1), (axe.OwnerId, axe.Quantity));
        Assert.Equal(!reserved, PersonalEquipmentRules.IsCarried(axe, actor));
        Assert.Equal(reserved ? house.InstanceId : null, axe.StorageBuildingId);
        world.Validate();
        var final = WorldRuntimeCodec.Encode(world.ExportState());
        using var loaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(final));
        Assert.Equal(final, WorldRuntimeCodec.Encode(loaded.ExportState()));
    }

    [Fact]
    public async Task AnEarlierToolPickupRefusesTheLaterActorsOriginalStock()
    {
        using var generated = NormalPathWorld.CreateGenerated("goods-collection-tool", _ => new Choice());
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var state = generated.ExportState();
        var household = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-blacksmith").HouseholdId!;
        var actors = state.Society.Society.GetHousehold(household).MemberIds.Order(StringComparer.Ordinal).ToArray();
        var house = PaidMarketWorld.HouseOf(state, household);
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => !actors.Contains(lot.OwnerId, StringComparer.Ordinal) &&
                (lot.OwnerId != household || ToolProgressionRules.Find(lot.ItemKind) is not { Family: ToolFamily.Pickaxe })).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "collect-competing-pick", "wooden_pickaxe", household, 1, storageBuildingId: house.InstanceId);
        state = Prepared(PaidMarketWorld.WithInventory(state, inventory), actors[0], house.Position) with
        {
            Agents = state.Agents.Select(person => person with
            {
                Position = actors.Contains(person.AgentId, StringComparer.Ordinal) ? house.Position : person.Position,
                Equipment = null,
                LastDecisionContext = null,
                Project = null,
                FullnessBasisPoints = 9_500,
            }).ToArray(),
        };
        var choices = actors.ToDictionary(actor => actor, _ => new Choice("collect_wooden_pickaxe"), StringComparer.Ordinal);
        using var world = WorldRuntime.Restore(state, actor => choices.TryGetValue(actor, out var provider) ? provider : new Choice());
        for (var tick = 0; tick < 8 && !world.ExportState().Events.Any(item => item.Kind == "choice_refused"); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var first = Assert.IsType<ToolCollectionActionPlan>(choices[actors[0]].Plan);
        var later = Assert.IsType<ToolCollectionActionPlan>(choices[actors[1]].Plan);
        Assert.Equal("collect-competing-pick", first.Pickup.StockId);
        Assert.Equal(first.Pickup.StockId, later.Pickup.StockId);
        Assert.Equal(actors[0], world.Society.Inventory.GetLot(first.Pickup.StockId).CarrierId);
        Assert.Single(world.ExportState().Events, item => item.Kind == "equipment_collected");
        Assert.Single(world.ExportState().Events, item => item.Kind == "choice_refused" &&
            item.Detail.StartsWith(world.Society.GetAgent(actors[1]).Name + " didn't collect the wooden pickaxe:", StringComparison.Ordinal));
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
    }

    [Fact]
    public async Task ToolCollectionKeepsItsSelectedStockWhenAnotherToolAppearsAndRefusesANewClaim()
    {
        using var generated = NormalPathWorld.CreateGenerated("goods-collection-tool", _ => new Choice());
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var state = generated.ExportState();
        var actor = state.Agents[0].AgentId;
        var household = PaidMarketWorld.HouseholdOf(state, actor);
        var house = PaidMarketWorld.HouseOf(state, household);
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor &&
                (lot.OwnerId != household || ToolProgressionRules.Find(lot.ItemKind) is not { Family: ToolFamily.Axe })).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "collect-selected-axe", "wooden_axe", household, 1, storageBuildingId: house.InstanceId);
        state = Prepared(PaidMarketWorld.WithInventory(state, inventory), actor, house.Position);
        var choices = new Choice("collect_wooden_axe", choose: false);
        using var world = WorldRuntime.Restore(state, id => id == actor ? choices : new Choice());
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<ToolCollectionActionPlan>(choices.Plan);
        Assert.Equal("collect-selected-axe", plan.Pickup.StockId);
        var checkpoint = world.ExportState();
        inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-new-shared-axe", "wooden_axe", household, 1,
            storageBuildingId: house.InstanceId);
        using var added = WorldRuntime.Restore(PaidMarketWorld.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "collect-selected-claim", household, plan.Pickup.StockId, 1, "other_work", long.MaxValue);
        using var claimed = WorldRuntime.Restore(PaidMarketWorld.WithInventory(checkpoint, inventory));
        Assert.Equal(ActionRefusalReason.StockUnavailable, claimed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal("collect-selected-axe", plan.Pickup.StockId);
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(claimed.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(claimed.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
    }

    internal static WorldRuntimeState Prepared(WorldRuntimeState state, string actor, GridPoint position) => state with
    {
        Agents = state.Agents.Select(person => person with
        {
            Position = person.AgentId == actor ? position : person.Position,
            Equipment = person.AgentId == actor ? null : person.Equipment,
            LastDecisionContext = null,
            Project = null,
            FullnessBasisPoints = 9_500,
            Survival = new SurvivalCondition { WarmthBasisPoints = 10_000, NutritionBasisPoints = 10_000 },
        }).ToArray(),
        JevEnabled = false,
        RoutineHelper = RoutineHelperSettings.Off,
    };

    private sealed class Choice(string wanted = "safe_idle", bool choose = true) : IDecisionProvider
    {
        public ActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id == wanted);
            if (candidate is not null) Plan = candidate.ActionPlan;
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId,
                request.Observation.AgentId, Kind, request.ProviderEpoch, request.Observation.RunEpoch,
                request.Observation.DecisionGeneration, request.Observation.ObservationDigest, selected, 1,
                request.Observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == selected ? 1d : 0d)));
        }
    }
}
