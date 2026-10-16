using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class WorldRuntimeTests
{
    [Theory]
    [InlineData("gather berries from berry-patch")]
    [InlineData("go to the berry patch")]
    public async Task UrgentCarriedFoodInterruptsFoodGatheringAndTravelOrders(string text)
    {
        using var setup = CreateKnownBerryOrderWorld(_ => new DeterministicDecisionProvider());
        var state = setup.ExportState();
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "urgent-order-fruit", "fruit", HarvestInstructionActor, 1);
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == HarvestInstructionActor
                ? person with { FullnessBasisPoints = 1_000 }
                : person).ToArray(),
            Society = state.Society with
            {
                Society = state.Society.Society with { Inventory = inventory },
            },
        };
        using var world = WorldRuntime.Restore(state, _ => new DeterministicDecisionProvider());
        var positionBefore = world.ExportState().Agents
            .Single(item => item.AgentId == HarvestInstructionActor).Position;
        var order = world.SubmitInstruction(new OwnerInstructionRequest(
            "urgent-order", "owner:test", HarvestInstructionActor, OwnerInstructionKind.Order, text));

        Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        var after = world.ExportState();
        Assert.Single(after.Events, item => item.Kind == "food_consumed" && item.Detail == HarvestInstructionActor);
        Assert.DoesNotContain(after.Events, item => item.Kind == "food_harvested" &&
            item.Detail.StartsWith(HarvestInstructionActor + ":", StringComparison.Ordinal));
        Assert.DoesNotContain(after.Society.Society.Inventory.Lots, item => item.Id == "urgent-order-fruit");
        Assert.Equal(positionBefore, after.Agents.Single(item => item.AgentId == HarvestInstructionActor).Position);
        var savedOrder = Assert.Single(after.Instructions!, item => item.InstructionId == order.InstructionId);
        Assert.Equal("interrupted", savedOrder.Task!.Status);
        Assert.Equal(0, savedOrder.Task.CompletedUnits);
        Assert.DoesNotContain(order.InstructionId.Value, after.CompletedInstructionIds ?? []);
        world.Validate();
    }

    [Fact]
    public async Task FullCarrierBlocksKnownFoodHarvestWithStableReasonAcrossReload()
    {
        var provider = new CountingModelOrderProvider();
        using var setup = CreateKnownBerryOrderWorld(id => id == HarvestInstructionActor
            ? provider
            : new DeterministicDecisionProvider());
        var state = setup.ExportState();
        var person = state.Agents.Single(item => item.AgentId == HarvestInstructionActor);
        var inventory = FillActorCarry(state.Society.Society.Inventory, person, "full-harvest-cargo");
        state = state with
        {
            Agents = state.Agents.Select(item => item.AgentId == HarvestInstructionActor
                ? item with { FullnessBasisPoints = 3_000 }
                : item).ToArray(),
            Society = state.Society with
            {
                Society = state.Society.Society with { Inventory = inventory },
            },
        };
        using var world = WorldRuntime.Restore(state, id => id == HarvestInstructionActor
            ? provider
            : new DeterministicDecisionProvider());
        var order = world.SubmitInstruction(new OwnerInstructionRequest("full-harvest", "owner:test",
            HarvestInstructionActor, OwnerInstructionKind.Order, "gather berries from berry-patch"));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        var blocked = world.ExportState();
        var blockedOrder = Assert.Single(blocked.Instructions!, item => item.InstructionId == order.InstructionId).Task!;
        Assert.Equal("blocked", blockedOrder.Status);
        Assert.Contains("carry", blockedOrder.BlockedReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, provider.CallCount);
        Assert.DoesNotContain(blocked.Events, item => item.Kind is "food_harvested" or "carrying_full");
        var savedInventory = blocked.Society.Society.Inventory;

        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
            WorldRuntimeCodec.Encode(blocked)), id => id == HarvestInstructionActor
                ? provider
                : new DeterministicDecisionProvider());
        for (var tick = 0; tick < 5; tick++)
            Assert.True((await restored.AdvanceOneTickAsync()).Advanced);

        var after = restored.ExportState();
        Assert.Equal(
            savedInventory.Lots.Where(item => item.OwnerId == HarvestInstructionActor)
                .Select(item => item with { LastProcessedTick = 0 })
                .OrderBy(item => item.Id, StringComparer.Ordinal),
            after.Society.Society.Inventory.Lots.Where(item => item.OwnerId == HarvestInstructionActor)
                .Select(item => item with { LastProcessedTick = 0 })
                .OrderBy(item => item.Id, StringComparer.Ordinal));
        Assert.Equal(0, PersonalEquipmentRules.FreeCapacity(after.Society.Society.Inventory,
            HarvestInstructionActor,
            after.Agents.Single(item => item.AgentId == HarvestInstructionActor).Equipment));
        Assert.Single(after.Instructions!, item => item.InstructionId == order.InstructionId &&
            item.Task!.Status == "blocked" && item.Task.BlockedReason == blockedOrder.BlockedReason);
        Assert.Single(after.Events, item => item.Kind == "instruction_order_status" &&
            item.Detail == $"{HarvestInstructionActor}:{order.InstructionId}:blocked");
        Assert.Equal(1, provider.CallCount);
        Assert.DoesNotContain(after.Events, item => item.Kind is "food_harvested" or "carrying_full");
        restored.Validate();
    }

    [Fact]
    public async Task FullCarrierDoesNotCollectHouseholdFoodForAnEatOrder()
    {
        var provider = new CountingModelOrderProvider();
        using var setup = CreateOrderWorldWithoutFood("full-household-food-order", id => id == OrderedAgent
            ? provider
            : new DeterministicDecisionProvider());
        var state = setup.ExportState();
        var person = state.Agents.Single(item => item.AgentId == OrderedAgent);
        var householdId = state.Society.Society.GetAgent(OrderedAgent).HouseholdId!;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "shared-order-berries", "berries", householdId, 1);
        inventory = FillActorCarry(inventory, person, "full-shared-food-cargo");
        state = state with
        {
            Agents = state.Agents.Select(item => item.AgentId == OrderedAgent
                ? item with { FullnessBasisPoints = 3_000 }
                : item).ToArray(),
            Society = state.Society with
            {
                Society = state.Society.Society with { Inventory = inventory },
            },
        };
        using var world = WorldRuntime.Restore(state, id => id == OrderedAgent
            ? provider
            : new DeterministicDecisionProvider());
        var order = world.SubmitInstruction(new OwnerInstructionRequest("full-shared-food", "owner:test",
            OrderedAgent, OwnerInstructionKind.Order, "eat food"));

        Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        var blocked = world.ExportState();
        var blockedOrder = Assert.Single(blocked.Instructions!, item => item.InstructionId == order.InstructionId).Task!;
        Assert.Equal("blocked", blockedOrder.Status);
        Assert.Contains("carry", blockedOrder.BlockedReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(1, Assert.Single(blocked.Society.Society.Inventory.Lots,
            item => item.Id == "shared-order-berries").Quantity);
        Assert.DoesNotContain(blocked.Events, item => item.Kind is "household_food_collected" or "food_consumed");

        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
            WorldRuntimeCodec.Encode(blocked)), id => id == OrderedAgent
                ? provider
                : new DeterministicDecisionProvider());
        for (var tick = 0; tick < 5; tick++)
            Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
        var after = restored.ExportState();
        Assert.Equal(1, Assert.Single(after.Society.Society.Inventory.Lots,
            item => item.Id == "shared-order-berries").Quantity);
        Assert.Equal(0, PersonalEquipmentRules.FreeCapacity(after.Society.Society.Inventory, OrderedAgent,
            after.Agents.Single(item => item.AgentId == OrderedAgent).Equipment));
        Assert.Single(after.Events, item => item.Kind == "instruction_order_status" &&
            item.Detail == $"{OrderedAgent}:{order.InstructionId}:blocked");
        Assert.Equal(1, provider.CallCount);
        Assert.DoesNotContain(after.Events, item => item.Kind is "household_food_collected" or "food_consumed");
        restored.Validate();
    }

    [Fact]
    public async Task EatOrderCollectsOnlyTheRequestedHouseholdFoodKind()
    {
        using var setup = CreateOrderWorldWithoutFood("requested-household-food-kind",
            _ => new DeterministicDecisionProvider());
        var state = setup.ExportState();
        var householdId = state.Society.Society.GetAgent(OrderedAgent).HouseholdId!;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "wrong-kind-fruit", "fruit", householdId, 1);
        inventory = InventoryAuthority.AddLot(inventory,
            "requested-kind-berries", "berries", householdId, 1);
        state = state with
        {
            Agents = state.Agents.Select(item => item.AgentId == OrderedAgent
                ? item with { FullnessBasisPoints = 3_000 }
                : item).ToArray(),
            Society = state.Society with
            {
                Society = state.Society.Society with { Inventory = inventory },
            },
        };
        using var world = WorldRuntime.Restore(state, _ => new DeterministicDecisionProvider());
        var order = world.SubmitInstruction(new OwnerInstructionRequest("requested-kind-eat", "owner:test",
            OrderedAgent, OwnerInstructionKind.Order, "eat berries"));

        for (var tick = 0; tick < 120 && !world.ExportState().Events.Any(item =>
                 item.Kind == "food_consumed" && item.Detail == OrderedAgent); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        var after = world.ExportState();
        Assert.Contains(order.InstructionId.Value, after.CompletedInstructionIds ?? []);
        Assert.DoesNotContain(after.Society.Society.Inventory.Lots, item => item.Id == "requested-kind-berries");
        var otherFood = Assert.Single(after.Society.Society.Inventory.Lots, item => item.Id == "wrong-kind-fruit");
        Assert.Equal(("fruit", householdId, 1), (otherFood.ItemKind, otherFood.OwnerId, otherFood.Quantity));
        Assert.Single(after.Events, item => item.Kind == "food_consumed" && item.Detail == OrderedAgent);
        Assert.Single(after.Events, item => item.Kind == "instruction_applied" &&
            item.Detail == order.InstructionId + ":consume_food");
        world.Validate();
    }

    [Fact]
    public async Task RepeatedEatOrderWaitsForNormalHungerBeforeConsumingMoreCarriedOrHouseholdFood()
    {
        using var setup = CreateOrderWorldWithoutFood("repeat-eat-order-fullness",
            _ => new DeterministicDecisionProvider());
        var state = setup.ExportState();
        var householdId = state.Society.Society.GetAgent(OrderedAgent).HouseholdId!;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "repeat-eat-carried-berries", "berries", OrderedAgent, 2);
        inventory = InventoryAuthority.AddLot(inventory,
            "repeat-eat-household-berries", "berries", householdId, 1);
        state = state with
        {
            Agents = state.Agents.Select(item => item.AgentId == OrderedAgent
                ? item with { FullnessBasisPoints = 3_000 }
                : item).ToArray(),
            Society = state.Society with
            {
                Society = state.Society.Society with { Inventory = inventory },
            },
        };
        using var world = WorldRuntime.Restore(state, _ => new DeterministicDecisionProvider());
        var order = world.SubmitInstruction(new OwnerInstructionRequest("repeat-eat", "owner:test",
            OrderedAgent, OwnerInstructionKind.Order, "eat food until cancelled"));
        Assert.True(Assert.Single(world.ExportState().Instructions!, item => item.InstructionId == order.InstructionId)
            .Task!.RepeatUntilCancelled);

        for (var tick = 0; tick < 20; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        var full = world.ExportState();
        Assert.Single(full.Events, item => item.Kind == "food_consumed" && item.Detail == OrderedAgent);
        Assert.Equal(1, Assert.Single(full.Society.Society.Inventory.Lots,
            item => item.Id == "repeat-eat-carried-berries").Quantity);
        Assert.Equal(1, Assert.Single(full.Society.Society.Inventory.Lots,
            item => item.Id == "repeat-eat-household-berries").Quantity);
        Assert.True(full.Agents.Single(item => item.AgentId == OrderedAgent).FullnessBasisPoints >= 4_000);
        var waitingOrder = Assert.Single(full.Instructions!, item => item.InstructionId == order.InstructionId).Task!;
        Assert.Equal("blocked", waitingOrder.Status);
        Assert.Contains("hungry", waitingOrder.BlockedReason, StringComparison.OrdinalIgnoreCase);

        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
            WorldRuntimeCodec.Encode(full)), _ => new DeterministicDecisionProvider());
        for (var tick = 0; tick < 10; tick++)
            Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
        Assert.Single(restored.ExportState().Events,
            item => item.Kind == "food_consumed" && item.Detail == OrderedAgent);
        Assert.Equal(1, Assert.Single(restored.ExportState().Society.Society.Inventory.Lots,
            item => item.Id == "repeat-eat-carried-berries").Quantity);
        Assert.Equal(1, Assert.Single(restored.ExportState().Society.Society.Inventory.Lots,
            item => item.Id == "repeat-eat-household-berries").Quantity);

        for (var tick = 0; tick < 300 && restored.ExportState().Events.Count(item =>
                 item.Kind == "food_consumed" && item.Detail == OrderedAgent) < 2; tick++)
            Assert.True((await restored.AdvanceOneTickAsync()).Advanced);

        var resumed = restored.ExportState();
        Assert.Equal(2, resumed.Events.Count(item => item.Kind == "food_consumed" && item.Detail == OrderedAgent));
        Assert.DoesNotContain(resumed.Society.Society.Inventory.Lots,
            item => item.Id == "repeat-eat-carried-berries");
        Assert.Equal(1, Assert.Single(resumed.Society.Society.Inventory.Lots,
            item => item.Id == "repeat-eat-household-berries").Quantity);
        Assert.DoesNotContain(order.InstructionId.Value, resumed.CompletedInstructionIds ?? []);
        restored.Validate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HeldPersonalResponseDoesNotStallOrRepeatAQuantifiedHarvestAndCanReplyAfterItFinishes(bool deferReturn)
    {
        var provider = new HeldOrderReplyProvider(deferReturn);
        using var setup = CreateKnownBerryOrderWorld(_ => new DeterministicDecisionProvider());
        var state = setup.ExportState();
        var source = state.Map.Resources.Single(item => item.Id == "berry-patch");
        var actor = state.Agents.Single(item => item.AgentId == HarvestInstructionActor);
        var occupied = state.Agents.Where(item => item.AgentId != HarvestInstructionActor)
            .Select(item => item.Position).ToHashSet();
        var farStand = state.Map.Tiles.Select(item => item.Position)
            .Where(point => state.Map.IsPassable(point) && !occupied.Contains(point) &&
                state.Map.Resources.All(resource => resource.Position != point))
            .Select(point => (Point: point, Distance: state.Map.FootDistance(point, source.Position)))
            .Where(item => item.Distance is >= 7 and < 40)
            .OrderByDescending(item => item.Distance).ThenBy(item => item.Point.X).ThenBy(item => item.Point.Y)
            .Select(item => item.Point).First();
        state = state with
        {
            Agents = state.Agents.Select(item => item.AgentId == HarvestInstructionActor
                ? item with { Position = farStand, FullnessBasisPoints = 10_000 }
                : item).ToArray(),
        };
        using var restoredWorld = WorldRuntime.Restore(state, id => id == HarvestInstructionActor
            ? provider
            : new DeterministicDecisionProvider());
        var order = restoredWorld.SubmitInstruction(new OwnerInstructionRequest("held-harvest", "owner:test",
            HarvestInstructionActor, OwnerInstructionKind.Order, "gather 2 berries from berry-patch"));
        Assert.True((await restoredWorld.TickModelWorkAsync()).Advanced);
        var enteredFrame = WorldRuntimeCodec.Encode(restoredWorld.ExportState());
        var call = HeldHarvestWork(restoredWorld);
        Assert.False(call.Task.IsCompleted);
        await ModelWorkHarness.For(restoredWorld).WaitAsync(provider.Started.Task);
        Assert.Equal(enteredFrame, WorldRuntimeCodec.Encode(restoredWorld.ExportState()));

        var initialRequest = Assert.Single(provider.Requests);
        Assert.Equal(initialRequest.ObservationDigest, call.Request.Observation.ObservationDigest);
        Assert.Equal(state.Society.Society.WorldId, initialRequest.WorldId);
        Assert.Equal(HarvestInstructionActor, initialRequest.AgentId);
        var exactMessage = Assert.Single(initialRequest.ObserverGuidance!, item => item.InstructionId == order.InstructionId);
        Assert.Equal("order", exactMessage.Kind);
        Assert.Equal("gather 2 berries from berry-patch", exactMessage.Text);
        Assert.True(exactMessage.ReplyAllowed);
        Assert.Equal("gather several food servings from a nearby food source", exactMessage.UnderstoodTask);

        for (var tick = 0; tick < 80 && Assert.Single(restoredWorld.ExportState().Instructions!,
                 item => item.InstructionId == order.InstructionId).Task!.Status != "finished"; tick++)
            Assert.True((await restoredWorld.TickModelWorkAsync()).Advanced);

        var finished = restoredWorld.ExportState();
        var finishedInstruction = Assert.Single(finished.Instructions!, item => item.InstructionId == order.InstructionId);
        Assert.Equal("finished", finishedInstruction.Task!.Status);
        Assert.True(finishedInstruction.Task.CompletedUnits >= finishedInstruction.Task.RequestedUnits);
        Assert.Null(finishedInstruction.ObservedTick);
        Assert.Equal(1, provider.CallCount);
        Assert.Single(finished.Events, item => item.Kind == "food_harvested" &&
            item.Detail.StartsWith(HarvestInstructionActor + ":", StringComparison.Ordinal));

        var finishedFrame = WorldRuntimeCodec.Encode(finished);
        Assert.False(call.Task.IsCompleted);
        provider.Release.TrySetResult(true);
        var completedCall = ModelWorkHarness.For(restoredWorld).WaitAsync(call.Task);
        if (deferReturn)
        {
            await ModelWorkHarness.For(restoredWorld).WaitAsync(provider.Returned.Task);
            Assert.False(call.Task.IsCompleted);
            Assert.False(completedCall.IsCompleted);
            Assert.Equal(finishedFrame, WorldRuntimeCodec.Encode(restoredWorld.ExportState()));
            provider.ReturnAllowed.TrySetResult(true);
        }
        await completedCall;
        Assert.True(provider.Returned.Task.IsCompletedSuccessfully, "The captured hosted call must construct its actual reply.");
        var reply = await provider.Returned.Task;
        Assert.Equal((call.Request.RequestId, HarvestInstructionActor, initialRequest.ObservationDigest, "seek_food"),
            (reply.RequestId, reply.AgentId, reply.ObservationDigest, reply.SelectedCandidateId));
        Assert.Equal(finishedFrame, WorldRuntimeCodec.Encode(restoredWorld.ExportState()));
        var admittedTick = await restoredWorld.TickModelWorkAsync();
        Assert.True(admittedTick.Advanced);
        var admittedDecision = Assert.Single(admittedTick.Decisions,
            item => item.AgentId == HarvestInstructionActor);
        Assert.Single(admittedTick.Events, item => item.Kind == "hosted_decision_completed" &&
            item.Detail == $"{HarvestInstructionActor}:{admittedDecision.Admission.Outcome}");

        var afterReplyBytes = WorldRuntimeCodec.Encode(restoredWorld.ExportState());
        var afterReply = WorldRuntimeCodec.Decode(afterReplyBytes);
        var repliedInstruction = Assert.Single(afterReply.Instructions!, item => item.InstructionId == order.InstructionId);
        Assert.Equal("finished", repliedInstruction.Task!.Status);
        Assert.Equal(finishedInstruction.Task.CompletedUnits, repliedInstruction.Task.CompletedUnits);
        Assert.Equal(1, afterReply.Events.Count(item => item.Kind == "food_harvested" &&
            item.Detail.StartsWith(HarvestInstructionActor + ":", StringComparison.Ordinal)));
        Assert.NotNull(repliedInstruction.ObservedTick);
        Assert.Equal("I will gather the berries.", repliedInstruction.ObserverReply);
        Assert.Contains(afterReply.Events, item => item.Kind == "instruction_order_stale_decision" &&
            item.Detail == HarvestInstructionActor);
        Assert.Single(afterReply.Events, item => item.Kind == "instruction_applied" &&
            item.Detail == order.InstructionId + ":harvest_food");
        Assert.Equal(1, provider.CallCount);
        using var validated = WorldRuntime.Restore(afterReply, id => id == HarvestInstructionActor
            ? provider : new DeterministicDecisionProvider());
        Assert.Equal(afterReplyBytes, WorldRuntimeCodec.Encode(validated.ExportState()));
        validated.Validate();
    }

    [Fact]
    public async Task HeldOrderResponseAndUrgentFoodCannotConsumeTwiceInOneTick()
    {
        var provider = new HeldOrderReplyProvider();
        using var setup = CreateKnownBerryOrderWorld(_ => new DeterministicDecisionProvider());
        var state = setup.ExportState();
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "held-urgent-fruit", "fruit", HarvestInstructionActor, 2);
        state = state with
        {
            Agents = state.Agents.Select(item => item.AgentId == HarvestInstructionActor
                ? item with { FullnessBasisPoints = 0 }
                : item).ToArray(),
            Society = state.Society with
            {
                Society = state.Society.Society with { Inventory = inventory },
            },
        };
        using var world = WorldRuntime.Restore(state, id => id == HarvestInstructionActor
            ? provider
            : new DeterministicDecisionProvider());
        var order = world.SubmitInstruction(new OwnerInstructionRequest("held-urgent-order", "owner:test",
            HarvestInstructionActor, OwnerInstructionKind.Order, "gather berries from berry-patch"));

        Assert.True((await world.TickModelWorkAsync()).Advanced);
        await ModelWorkHarness.For(world).WaitAsync(provider.Started.Task);

        var afterOneTick = world.ExportState();
        Assert.Single(afterOneTick.Events, item => item.Kind == "food_consumed" &&
            item.Detail == HarvestInstructionActor);
        Assert.Equal(1, Assert.Single(afterOneTick.Society.Society.Inventory.Lots,
            item => item.Id == "held-urgent-fruit").Quantity);
        Assert.Equal("interrupted", Assert.Single(afterOneTick.Instructions!,
            item => item.InstructionId == order.InstructionId).Task!.Status);
        Assert.Equal(1, provider.CallCount);
        Assert.DoesNotContain(afterOneTick.Events, item => item.Kind == "food_harvested" &&
            item.Detail.StartsWith(HarvestInstructionActor + ":", StringComparison.Ordinal));
        world.Validate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HeldReplyDoesNotEatNewHarvestUntilTheNextTickAfterTheOrderFinishes(
        bool delayedStartup)
    {
        var provider = new HeldOrderReplyProvider();
        using var setup = CreateKnownBerryOrderWorld(_ => new DeterministicDecisionProvider());
        var state = setup.ExportState();
        state = state with
        {
            Agents = state.Agents.Select(item => item.AgentId == HarvestInstructionActor
                ? item with { FullnessBasisPoints = 0 }
                : item).ToArray(),
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Inventory = WithoutFoodLots(state.Society.Society.Inventory),
                },
            },
        };
        using var world = WorldRuntime.Restore(state, id => id == HarvestInstructionActor
            ? provider
            : new DeterministicDecisionProvider());
        var preparation = new InlineCompletionSource<bool>(TaskCreationOptions.None);
        if (delayedStartup) provider.PreparationGate = preparation.Task;
        var order = world.SubmitInstruction(new OwnerInstructionRequest("held-urgent-harvest", "owner:test",
            HarvestInstructionActor, OwnerInstructionKind.Order, "gather berries from berry-patch"));

        Assert.True((await world.TickModelWorkAsync()).Advanced);
        var call = HeldHarvestWork(world);
        Assert.False(call.Task.IsCompleted);
        try
        {
            var afterHarvestTick = world.ExportState();
            Assert.Equal("finished", Assert.Single(afterHarvestTick.Instructions!,
                item => item.InstructionId == order.InstructionId).Task!.Status);
            Assert.Single(afterHarvestTick.Events, item => item.Kind == "food_harvested" &&
                item.Detail.StartsWith(HarvestInstructionActor + ":", StringComparison.Ordinal));
            Assert.DoesNotContain(afterHarvestTick.Events, item => item.Kind == "food_consumed" &&
                item.Detail == HarvestInstructionActor);
            Assert.Null(Assert.Single(afterHarvestTick.Instructions!,
                item => item.InstructionId == order.InstructionId).ObservedTick);

            Assert.True((await world.TickModelWorkAsync()).Advanced);
            var afterFollowingTick = world.ExportState();
            Assert.Single(afterFollowingTick.Events, item => item.Kind == "food_harvested" &&
                item.Detail.StartsWith(HarvestInstructionActor + ":", StringComparison.Ordinal));
            Assert.Single(afterFollowingTick.Events, item => item.Kind == "food_consumed" &&
                item.Detail == HarvestInstructionActor);

            if (delayedStartup)
            {
                Assert.False(provider.Started.Task.IsCompleted);
                Assert.Equal(0, provider.CallCount);
            }
            Assert.False(call.Task.IsCompleted);
            var beforeEntry = WorldRuntimeCodec.Encode(world.ExportState());
            preparation.TrySetResult(true);
            await ModelWorkHarness.For(world).WaitAsync(provider.Started.Task);
            Assert.Equal(beforeEntry, WorldRuntimeCodec.Encode(world.ExportState()));
            var enteredCall = HeldHarvestWork(world);
            Assert.Same(call.Request, enteredCall.Request);
            Assert.Same(call.Task, enteredCall.Task);
            Assert.Equal(1, provider.CallCount);
            Assert.Equal(call.Request.Observation.ObservationDigest, Assert.Single(provider.Requests).ObservationDigest);

            provider.Release.TrySetResult(true);
            await ModelWorkHarness.For(world).WaitAsync(call.Task);
            Assert.Equal(beforeEntry, WorldRuntimeCodec.Encode(world.ExportState()));
            Assert.True(provider.Returned.Task.IsCompletedSuccessfully);
            Assert.Equal(call.Request.RequestId, (await provider.Returned.Task).RequestId);
            for (var tick = 0; tick < 5 && !world.ExportState().Events.Any(item =>
                     item.Kind == "hosted_decision_completed" && item.Detail == HarvestInstructionActor); tick++)
                Assert.True((await world.TickModelWorkAsync()).Advanced);

            var final = world.ExportState();
            var savedOrder = Assert.Single(final.Instructions!, item => item.InstructionId == order.InstructionId);
            Assert.Equal("finished", savedOrder.Task!.Status);
            Assert.NotNull(savedOrder.ObservedTick);
            Assert.Equal("I will gather the berries.", savedOrder.ObserverReply);
            Assert.Single(final.Events, item => item.Kind == "food_harvested" &&
                item.Detail.StartsWith(HarvestInstructionActor + ":", StringComparison.Ordinal));
            Assert.Contains(final.Events, item => item.Kind == "food_consumed" &&
                item.Detail == HarvestInstructionActor);
            Assert.Contains(final.Events, item => item.Kind == "instruction_order_stale_decision" &&
                item.Detail == HarvestInstructionActor);
            Assert.Single(provider.Requests, request =>
                request.OperativeOrderInstructionId == order.InstructionId ||
                request.ObserverGuidance?.Any(message => message.InstructionId == order.InstructionId) == true);
            using var validated = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
                WorldRuntimeCodec.Encode(final)));
            validated.Validate();
        }
        finally
        {
            preparation.TrySetResult(true);
            provider.Release.TrySetResult(true);
            provider.ReturnAllowed.TrySetResult(true);
            world.CancelPendingModelCalls();
            await ModelWorkHarness.For(world).WaitAsync(call.Task);
        }
    }

    [Fact]
    public async Task FailedOrderDecisionPersistsWaitAndRecoversAtTheNormalRetryInterval()
    {
        var provider = new FailOnceModelOrderProvider();
        using var world = CreateKnownBerryOrderWorld(id => id == HarvestInstructionActor
            ? provider
            : new DeterministicDecisionProvider());
        var order = world.SubmitInstruction(new OwnerInstructionRequest("retry-order", "owner:test",
            HarvestInstructionActor, OwnerInstructionKind.Order, "gather berries from berry-patch"));

        Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        var failed = world.ExportState();
        var failedOrder = Assert.Single(failed.Instructions!, item => item.InstructionId == order.InstructionId).Task!;
        Assert.Equal("blocked", failedOrder.Status);
        Assert.True(failedOrder.WaitForDecisionAfterFailure);
        Assert.Equal(1, provider.CallCount);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
            WorldRuntimeCodec.Encode(failed)), id => id == HarvestInstructionActor
                ? provider
                : new DeterministicDecisionProvider());

        for (var tick = 0; tick < 15; tick++)
            Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(1, provider.CallCount);
        Assert.DoesNotContain(restored.ExportState().Events, item => item.Kind == "food_harvested");

        for (var tick = 0; tick < 20 && provider.CallCount < 2; tick++)
            Assert.True((await restored.AdvanceOneTickAsync()).Advanced);

        var recovered = restored.ExportState();
        Assert.Equal(2, provider.CallCount);
        Assert.Contains(order.InstructionId.Value, recovered.CompletedInstructionIds ?? []);
        Assert.Single(recovered.Events, item => item.Kind == "food_harvested" &&
            item.Detail.StartsWith(HarvestInstructionActor + ":", StringComparison.Ordinal));
        Assert.False(Assert.Single(recovered.Instructions!, item => item.InstructionId == order.InstructionId)
            .Task!.WaitForDecisionAfterFailure);
        restored.Validate();
    }

    private static WorldRuntime CreateKnownBerryOrderWorld(Func<string, IDecisionProvider> providerFactory)
    {
        using var setup = CreateHarvestInstructionWorld(orchard: false);
        var state = setup.ExportState();
        var source = state.Map.Resources.Single(item => item.Id == "berry-patch");
        var occupied = state.Agents.Where(item => item.AgentId != HarvestInstructionActor)
            .Select(item => item.Position).ToHashSet();
        var stand = state.Map.FootNeighbors(source.Position)
            .First(point => state.Map.IsPassable(point) && !occupied.Contains(point));
        state = state with
        {
            Agents = state.Agents.Select(item => item.AgentId == HarvestInstructionActor
                ? item with { Position = stand }
                : item).ToArray(),
        };
        return WorldRuntime.Restore(state, providerFactory);
    }

    private static InventoryCheckpoint FillActorCarry(
        InventoryCheckpoint inventory,
        AgentState person,
        string lotId)
    {
        var free = PersonalEquipmentRules.FreeCapacity(inventory, person.AgentId, person.Equipment);
        if (free > 0)
            inventory = InventoryAuthority.AddLot(inventory, lotId, "stone", person.AgentId, free);
        Assert.Equal(0, PersonalEquipmentRules.FreeCapacity(inventory, person.AgentId, person.Equipment));
        return inventory;
    }

    private static InventoryCheckpoint WithoutFoodLots(InventoryCheckpoint inventory)
    {
        var foodKinds = new HashSet<string>(StringComparer.Ordinal)
        {
            "food", "fruit", "berries", "wild_greens", "cultivated_greens", "bread", "porridge", "stew",
        };
        var lots = inventory.Lots.Where(item => !foodKinds.Contains(item.ItemKind)).ToArray();
        var lotIds = lots.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        return inventory with
        {
            Lots = lots,
            Reservations = inventory.Reservations.Where(item => lotIds.Contains(item.LotId)).ToArray(),
            Offers = inventory.Offers.Where(item => lotIds.Contains(item.FirstLotId) &&
                lotIds.Contains(item.SecondLotId)).ToArray(),
        };
    }

    private static CognitionDecisionResponse ModelResponse(
        CognitionDecisionRequest request,
        DecisionProviderKind kind,
        long providerEpoch,
        string preferredCandidate,
        IReadOnlyList<CognitionObserverReply>? replies = null)
    {
        var candidates = request.Observation.Candidates;
        var selected = candidates.FirstOrDefault(item => item.Id == preferredCandidate) ??
            candidates.First(item => item.Id == "safe_idle");
        return new CognitionDecisionResponse(
            request.RequestId,
            request.Observation.AgentId,
            kind,
            providerEpoch,
            request.Observation.RunEpoch,
            request.Observation.DecisionGeneration,
            request.Observation.ObservationDigest,
            selected.Id,
            1d,
            candidates.ToDictionary(item => item.Id, item => item.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal),
            ObserverReplies: replies);
    }

    private static (CognitionDecisionRequest Request, Task Task) HeldHarvestWork(WorldRuntime world)
    {
        var pending = Assert.Single(world.PendingModelWork, item => item.Transition.Kind == ModelWorkKind.Decision &&
            item.Transition.ActorId == HarvestInstructionActor);
        return (Assert.IsType<CognitionDecisionRequest>(pending.DecisionRequest), pending.Completion);
    }

    private sealed class HeldOrderReplyProvider(bool deferReturn = false) : IDecisionProvider
    {
        private int callCount;

        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public int CallCount => Volatile.Read(ref callCount);
        public ConcurrentQueue<AgentObservation> Requests { get; } = new();
        public InlineCompletionSource<bool> Started { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> Release { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<CognitionDecisionResponse> Returned { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> ReturnAllowed { get; } = new(TaskCreationOptions.None);
        public Task? PreparationGate { get; set; }

        public async ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (PreparationGate is { } preparation) await preparation.WaitAsync(cancellationToken);

            request.Validate();
            Interlocked.Increment(ref callCount);
            Requests.Enqueue(request.Observation);
            Started.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
            var message = request.Observation.ObserverGuidance!.Single();
            var response = ModelResponse(request, Kind, ProviderEpoch, "seek_food",
                [new CognitionObserverReply(message.InstructionId, "I will gather the berries.")]);
            Returned.TrySetResult(response);
            if (deferReturn) await ReturnAllowed.Task.WaitAsync(cancellationToken);
            return response;
        }
    }

    private sealed class CountingModelOrderProvider : IDecisionProvider
    {
        private int callCount;

        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public int CallCount => Volatile.Read(ref callCount);

        public ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            request.Validate();
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref callCount);
            return ValueTask.FromResult(ModelResponse(request, Kind, ProviderEpoch, "harvest_food"));
        }
    }

    private sealed class FailOnceModelOrderProvider : IDecisionProvider
    {
        private int callCount;

        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public int CallCount => Volatile.Read(ref callCount);

        public ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            request.Validate();
            cancellationToken.ThrowIfCancellationRequested();
            var call = Interlocked.Increment(ref callCount);
            if (call == 1)
                throw new InvalidOperationException("temporary provider outage");
            return ValueTask.FromResult(ModelResponse(request, Kind, ProviderEpoch, "harvest_food"));
        }
    }
}
