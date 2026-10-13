using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed class WorldCartRepairOrderTests
{
    private static readonly string[] RepairKinds = ["wood", "iron_fittings", "rope"];
    private static readonly Lazy<Task<byte[]>> Fixture = new(async () =>
    {
        using var setup = NormalPathWorld.CreateGenerated("cart-order-repairs", _ => new IdleProvider());
        Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        var state = WeatherTestFixture.WithWeather(setup.ExportState(), WeatherKind.Clear);
        return WorldRuntimeCodec.Encode(state with
        {
            Agents = state.Agents.Select(person => person with
            { FullnessBasisPoints = 9_500, Survival = new(), LastDecisionContext = null, Project = null, TravelCooldownTicks = 0 }).ToArray(),
        });
    });

    [Theory]
    [InlineData(0)]
    [InlineData(4_000)]
    public async Task NamedRepairConsumesRealInputsAndKeepsTheSelectedCartsDamagedCargoAcrossReplay(int condition)
    {
        var state = await CartState(condition);
        var actor = state.Agents[0].AgentId;
        using var world = WorldRuntime.Restore(state, _ => new IdleProvider());
        world.SubmitInstruction(Request("repair", actor, "Repair my handcart z-cart"));
        var submitted = Assert.Single(world.ExportState().Instructions!).Task!;
        Assert.Equal("repair_handcart", submitted.Action);
        Assert.Equal("z-cart", submitted.TargetCartLotId);
        Assert.Equal("z-cart", Assert.Single(new OwnerWorldObservationStore(world).GetSnapshot().Instructions).Task!.TargetCartLotId);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new IdleProvider());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 16 && world.ExportState().Instructions![0].Task!.Status != "finished"; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        var final = world.ExportState();
        var order = Assert.Single(final.Instructions!).Task!;
        Assert.Equal("finished", order.Status);
        Assert.Equal(1, order.CompletedUnits);
        Assert.Equal(10_000, world.Society.Inventory.GetLot("z-cart").ConditionBasisPoints);
        Assert.Equal(3_000, world.Society.Inventory.GetLot("a-cart").ConditionBasisPoints);
        Assert.Equal(state.Society.Society.Inventory.GetLot("cargo") with
        { LastProcessedTick = final.Society.Society.WorldTick }, world.Society.Inventory.GetLot("cargo"));
        Assert.Equal(state.Society.Society.Inventory.GetLot("z-cart").GroundPosition, world.Society.Inventory.GetLot("z-cart").GroundPosition);
        Assert.Equal(actor, world.Society.Inventory.GetLot("z-cart").OwnerId);
        Assert.Empty(final.HandcartHitches!);
        Assert.All(RepairKinds, kind =>
            Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == "repair-input-" + kind));
        Assert.Equal(3, world.Society.Inventory.Reservations.Count(item => item.Id.StartsWith("cart-repair:", StringComparison.Ordinal) &&
            item.OwnerId == actor && item.State == InventoryReservationState.Completed));
        Assert.Single(final.Events, item => item.Kind == "handcart_repaired" && item.Detail == actor + ":z-cart");
        Assert.Single(final.Events, item => item.Kind == "instruction_order_finished");
        var repairInputs = final.Society.Society.Inventory.Reservations.Where(item => item.Purpose == "equipment_repair").ToArray();
        final = RepairHistoryTests.SaveAfterLaterRepairs(final);
        Assert.All(repairInputs, input => Assert.Contains(input, final.Society.Society.Inventory.Reservations));
        using var loaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(final)), _ => new IdleProvider());
        Assert.True((await loaded.AdvanceOneTickAsync()).Advanced);
        Assert.Single(loaded.ExportState().Events, item => item.Kind == "instruction_order_finished");
    }

    [Theory]
    [InlineData("foreign", "own")]
    [InlineData("reserved", "reserved")]
    [InlineData("route", "route")]
    [InlineData("healthy", "healthy")]
    [InlineData("missing", "available")]
    [InlineData("capacity", "carrying")]
    public async Task PhysicalBlockersDoNotRepairAnotherCartOrSpendProtectedInputs(string blocker, string reason)
    {
        var state = await CartState(blocker switch { "healthy" => 10_000, "reserved" => 4_000, _ => 0 }, carriedInputs: blocker != "capacity");
        var actor = state.Agents[0].AgentId;
        var inventory = state.Society.Society.Inventory;
        if (blocker == "foreign") inventory = inventory with
        {
            Lots = inventory.Lots.Select(lot => lot.Id is "z-cart" or "cargo"
                ? lot with { OwnerId = state.Agents[1].AgentId } : lot).ToArray(),
        };
        if (blocker == "reserved") inventory = InventoryAuthority.Reserve(inventory, "protected-cargo", actor, "cargo", 1,
            "other_work", inventory.WorldTick + 100);
        if (blocker == "missing")
            foreach (var lot in inventory.Lots.Where(lot => lot.ItemKind == "iron_fittings").ToArray())
            {
                var available = Math.Max(0, InventoryRules.AvailableQuantity(InventoryIndex.For(inventory), lot));
                if (available > 0) inventory = InventoryAuthority.Reserve(inventory, "protected-" + lot.Id, lot.OwnerId,
                    lot.Id, available, "other_work", inventory.WorldTick + 100);
            }
        if (blocker == "capacity")
        {
            var equipment = state.Agents[0].Equipment;
            var space = PersonalEquipmentRules.Capacity(inventory, actor, equipment) - PersonalEquipmentRules.CarriedQuantity(inventory, actor, equipment);
            inventory = InventoryAuthority.AddLot(inventory, "full-carried-load", "stone", actor, space);
        }
        state = WithInventory(state, inventory);
        if (blocker == "route")
        {
            var site = inventory.GetLot("z-cart").GroundPosition!.Value;
            state = state with
            {
                Agents = state.Agents.Select((person, index) => index == 1
                ? person with { Position = new(site.X, site.Y) } : person).ToArray()
            };
        }
        using var world = WorldRuntime.Restore(state, _ => new IdleProvider());
        world.SubmitInstruction(Request("blocked-repair", actor, "Repair cart z-cart"));
        for (var tick = 0; tick < 3; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var final = world.ExportState();
        var order = Assert.Single(final.Instructions!).Task!;
        Assert.Equal("blocked", order.Status);
        Assert.Contains(reason, order.BlockedReason!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("z-cart", order.TargetCartLotId);
        Assert.Equal(0, order.CompletedUnits);
        Assert.Null(order.LastEffectId);
        Assert.Equal(inventory.GetLot("z-cart").ConditionBasisPoints, world.Society.Inventory.GetLot("z-cart").ConditionBasisPoints);
        Assert.Equal(3_000, world.Society.Inventory.GetLot("a-cart").ConditionBasisPoints);
        Assert.DoesNotContain(final.Events, item => item.Kind == "handcart_repaired");
        Assert.DoesNotContain(world.Society.Inventory.Reservations, item => item.Id.StartsWith("cart-repair:", StringComparison.Ordinal));
        using var loaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(final)), _ => new IdleProvider());
        Assert.Equal(WorldRuntimeCodec.Encode(final), WorldRuntimeCodec.Encode(loaded.ExportState()));
    }

    [Fact]
    public async Task SharedSuppliesAreCollectedPhysicallyAndTheBoundRepairResumesAcrossReload()
    {
        var state = await SharedSupplyState();
        var actor = state.Agents[0].AgentId;
        using var world = WorldRuntime.Restore(state, _ => new IdleProvider());
        world.SubmitInstruction(Request("shared-repair", actor, "Repair cart z-cart"));
        await ReachFirstCollection(world, actor);
        var collecting = world.ExportState();
        Assert.Equal("z-cart", Assert.Single(collecting.Instructions!).Task!.TargetCartLotId);
        Assert.Equal(0, collecting.Instructions![0].Task!.CompletedUnits);
        Assert.Equal(actor, world.Society.Inventory.GetLot("000-repair-shared-wood").OwnerId);
        Assert.Equal(0, world.Society.Inventory.GetLot("z-cart").ConditionBasisPoints);
        var bytes = WorldRuntimeCodec.Encode(collecting);
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new IdleProvider());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 48 && world.ExportState().Instructions![0].Task!.Status != "finished"; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.Equal("finished", world.ExportState().Instructions![0].Task!.Status);
        Assert.Equal(10_000, world.Society.Inventory.GetLot("z-cart").ConditionBasisPoints);
        Assert.All(RepairKinds, kind => Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == "000-repair-shared-" + kind));
        Assert.Equal(4, world.Society.Inventory.GetLot("cargo").Quantity);
        Assert.Equal("z-cart", world.Society.Inventory.GetLot("cargo").ContainerLotId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelOrReplaceAfterCollectionKeepsActualGoodsAndDoesNotRepairTheCart(bool replace)
    {
        var state = await SharedSupplyState();
        var actor = state.Agents[0].AgentId;
        using var world = WorldRuntime.Restore(state, _ => new IdleProvider());
        var instruction = world.SubmitInstruction(Request("cancel-repair", actor, "Repair cart z-cart"));
        await ReachFirstCollection(world, actor);
        var collected = world.Society.Inventory.GetLot("000-repair-shared-wood");
        world.SubmitInstruction(Request("typo", actor, "Repair cart z-cart tomorrow"));
        Assert.NotEqual("cancelled", world.ExportState().Instructions!.Single(item => item.InstructionId == instruction.InstructionId).Task!.Status);
        if (replace)
        {
            var position = world.Agents[0].Position;
            world.SubmitInstruction(Request("replacement", actor, $"Move to ({position.X}, {position.Y})"));
        }
        else world.CancelOrder(new("cancel", "owner:test", state.Society.Society.WorldId, actor, instruction.InstructionId));
        using var loaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), _ => new IdleProvider());
        for (var tick = 0; tick < 4; tick++) Assert.True((await loaded.AdvanceOneTickAsync()).Advanced);
        var final = loaded.ExportState();
        var old = final.Instructions!.Single(item => item.InstructionId == instruction.InstructionId).Task!;
        Assert.Equal("cancelled", old.Status);
        Assert.Equal(0, old.CompletedUnits);
        Assert.Equal(collected with { LastProcessedTick = final.Society.Society.WorldTick }, loaded.Society.Inventory.GetLot(collected.Id));
        Assert.Equal(0, loaded.Society.Inventory.GetLot("z-cart").ConditionBasisPoints);
        Assert.DoesNotContain(final.Events, item => item.Kind == "handcart_repaired");
        Assert.DoesNotContain(loaded.Society.Inventory.Reservations, item => item.Id.StartsWith("cart-repair:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnspecifiedRepairKeepsItsCartWhenThatCartChangesOwner()
    {
        var state = await SharedSupplyState();
        var actor = state.Agents[0].AgentId;
        using var world = WorldRuntime.Restore(state, _ => new IdleProvider());
        world.SubmitInstruction(Request("choose-repair", actor, "Please repair my cart"));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var started = world.ExportState();
        Assert.Equal("a-cart", Assert.Single(started.Instructions!).Task!.TargetCartLotId);
        var inventory = started.Society.Society.Inventory with
        {
            Lots = started.Society.Society.Inventory.Lots.Select(lot => lot.Id == "a-cart"
                ? lot with { OwnerId = state.Agents[1].AgentId } : lot).ToArray(),
        };
        using var transferred = WorldRuntime.Restore(WithInventory(started, inventory), _ => new IdleProvider());
        Assert.True((await transferred.AdvanceOneTickAsync()).Advanced);
        var order = Assert.Single(transferred.ExportState().Instructions!).Task!;
        Assert.Equal("blocked", order.Status);
        Assert.Contains("own", order.BlockedReason!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("a-cart", order.TargetCartLotId);
        Assert.Equal(0, order.CompletedUnits);
        Assert.Equal(0, transferred.Society.Inventory.GetLot("z-cart").ConditionBasisPoints);
    }

    [Fact]
    public async Task QueuedRepairsEachConsumeTheirOwnRealSetAndFinishOnlyTheirSelectedCart()
    {
        var state = await SharedSupplyState(2);
        var actor = state.Agents[0].AgentId;
        using var world = WorldRuntime.Restore(state, _ => new IdleProvider());
        world.SubmitInstruction(Request("first-repair", actor, "Repair cart a-cart"));
        world.SubmitInstruction(Request("second-repair", actor, "Repair cart z-cart", queue: true));
        for (var tick = 0; tick < 64 && world.ExportState().Instructions!.Any(item => item.Task!.Status != "finished"); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var final = world.ExportState();
        Assert.All(final.Instructions!, item => { Assert.Equal("finished", item.Task!.Status); Assert.Equal(1, item.Task.CompletedUnits); });
        Assert.Equal(10_000, world.Society.Inventory.GetLot("a-cart").ConditionBasisPoints);
        Assert.Equal(10_000, world.Society.Inventory.GetLot("z-cart").ConditionBasisPoints);
        Assert.Equal(6, world.Society.Inventory.Reservations.Count(item => item.Id.StartsWith("cart-repair:", StringComparison.Ordinal) && item.State == InventoryReservationState.Completed));
        Assert.Equal(2, final.Events.Count(item => item.Kind == "handcart_repaired"));
        Assert.Equal(4, world.Society.Inventory.GetLot("cargo").Quantity);
    }

    [Fact]
    public async Task UrgentFoodInterruptsRepairThenResumesItsSelectedCart()
    {
        var state = await CartState();
        var actor = state.Agents[0].AgentId;
        state = WithInventory(state, InventoryAuthority.AddLot(state.Society.Society.Inventory, "urgent-food", "berries", actor, 4));
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
            ? person with { FullnessBasisPoints = 1 } : person).ToArray()
        };
        using var world = WorldRuntime.Restore(state, _ => new IdleProvider());
        world.SubmitInstruction(Request("urgent-repair", actor, "Repair cart z-cart"));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var interrupted = Assert.Single(world.ExportState().Instructions!).Task!;
        Assert.Equal("interrupted", interrupted.Status);
        Assert.Equal("z-cart", interrupted.TargetCartLotId);
        Assert.Equal(0, interrupted.CompletedUnits);
        using var loaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), _ => new IdleProvider());
        for (var tick = 0; tick < 32 && loaded.ExportState().Instructions![0].Task!.Status != "finished"; tick++)
            Assert.True((await loaded.AdvanceOneTickAsync()).Advanced);
        Assert.Equal("finished", loaded.ExportState().Instructions![0].Task!.Status);
        Assert.Equal(10_000, loaded.Society.Inventory.GetLot("z-cart").ConditionBasisPoints);
        Assert.Equal(3_000, loaded.Society.Inventory.GetLot("a-cart").ConditionBasisPoints);
    }

    [Fact]
    public async Task RepairCompletionRequiresItsNativeReceiptAndConsumedMaterialEvidence()
    {
        var state = await CartState();
        var actor = state.Agents[0].AgentId;
        using var world = WorldRuntime.Restore(state, _ => new IdleProvider());
        world.SubmitInstruction(Request("receipt-repair", actor, "Repair cart z-cart"));
        for (var tick = 0; tick < 16 && world.ExportState().Instructions![0].Task!.Status != "finished"; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var final = world.ExportState();
        Assert.Equal("finished", final.Instructions![0].Task!.Status);
        var forged = final with
        {
            Instructions = final.Instructions.Select(item => item with
            { Task = item.Task! with { LastEffectId = Identity.Parse("cart-repair-order:0:" + new string('0', 64)) } }).ToArray()
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(forged));
        var missingInputs = final.Society.Society.Inventory with
        { Reservations = final.Society.Society.Inventory.Reservations.Where(item => !item.Id.StartsWith("cart-repair:", StringComparison.Ordinal)).ToArray() };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(WithInventory(final, missingInputs)));
        var foreignInputs = final.Society.Society.Inventory with
        {
            Reservations = final.Society.Society.Inventory.Reservations.Select(item => item.Id.StartsWith("cart-repair:", StringComparison.Ordinal)
            ? item with { OwnerId = state.Agents[1].AgentId } : item).ToArray()
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(WithInventory(final, foreignInputs)));
    }

    private static async Task ReachFirstCollection(WorldRuntime world, string actor)
    {
        for (var tick = 0; tick < 24 && world.Society.Inventory.GetLot("000-repair-shared-wood").OwnerId != actor; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(actor, world.Society.Inventory.GetLot("000-repair-shared-wood").OwnerId);
    }

    private static async Task<WorldRuntimeState> SharedSupplyState(int quantity = 1)
    {
        var state = await CartState(carriedInputs: false);
        var household = state.Society.Society.GetAgent(state.Agents[0].AgentId).HouseholdId!;
        var inventory = state.Society.Society.Inventory;
        foreach (var kind in RepairKinds)
            inventory = InventoryAuthority.AddLot(inventory, "000-repair-shared-" + kind, kind, household, quantity);
        return WithInventory(state, inventory);
    }

    private static async Task<WorldRuntimeState> CartState(int condition = 0, bool carriedInputs = true)
    {
        var state = WorldRuntimeCodec.Decode(await Fixture.Value);
        var actor = state.Agents[0].AgentId;
        var position = state.Map.FootNeighbors(state.Agents[0].Position)
            .First(point => state.Map.IsPassable(point) && state.Agents.All(person => person.Position != point));
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "a-cart", "handcart", actor, 1,
            groundPosition: new(position.X, position.Y), conditionBasisPoints: 3_000);
        inventory = InventoryAuthority.AddLot(inventory, "z-cart", "handcart", actor, 1,
            groundPosition: new(position.X, position.Y));
        inventory = InventoryAuthority.AddLot(inventory, "cargo", "stone", actor, 4, containerLotId: "z-cart", conditionBasisPoints: 2_500);
        if (condition < 10_000) inventory = InventoryAuthority.WearSingleUnit(inventory, "z-cart", 10_000 - condition);
        if (carriedInputs)
            foreach (var kind in RepairKinds)
                inventory = InventoryAuthority.AddLot(inventory, "repair-input-" + kind, kind, actor, 1);
        return WithInventory(state, inventory);
    }

    private static WorldRuntimeState WithInventory(WorldRuntimeState state, InventoryCheckpoint inventory) =>
        state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };

    private static OwnerInstructionRequest Request(string key, string actor, string text, bool queue = false) =>
        new(key, "owner:test", actor, OwnerInstructionKind.Order, text, queue);

    private sealed class IdleProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default) =>
            new DeterministicDecisionProvider().DecideAsync(request with
            { Observation = request.Observation with { Candidates = [request.Observation.Candidates.Single(item => item.Id == "safe_idle")] } }, cancellationToken);
    }
}
