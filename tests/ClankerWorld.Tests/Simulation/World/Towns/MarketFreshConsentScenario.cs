using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

// Invoked only after the existing generated-world test builds a genuinely paid Market.
// Spare goods and comfortable initial clothing/food state are explicit controlled fixtures.
// No position, title, Council approval, project or building is invented here.
internal static class MarketFreshConsentScenario
{
    private const string StockId = "market-consent-personal-spare-axes";

    internal static async Task AssertFreshArrivalConsentAsync(WorldRuntime paidWorld)
    {
        var boundary = paidWorld.ExportState();
        var market = Assert.Single(Assert.Single(boundary.Towns!).Markets);
        Assert.Equal("completed", Assert.Single(boundary.Towns![0].Projects,
            project => project.Id == market.ProjectId).Stage);
        Assert.Empty(market.Occupancies);
        Assert.Empty(market.StockReceipts);
        var stall = market.Stalls.OrderBy(item => item.SlotIndex).First();
        var position = boundary.WorldSimulation!.Buildings.Single(building => building.InstanceId == stall.BuildingId).Position;
        var actor = boundary.Agents.OrderBy(person => boundary.Map.FootDistance(person.Position, position))
            .First(person => boundary.Map.FootDistance(person.Position, position) > 1 &&
                boundary.Society.Society.GetAgent(person.AgentId) is
                { Status: SocietyAgentStatus.Active, HouseholdId: not null, AgeBand: SocietyAgeBand.Adult }).AgentId;
        var initial = WithInventory(boundary, InventoryAuthority.AddLot(boundary.Society.Society.Inventory,
            StockId, "wooden_axe", actor, 4));
        initial = WithInventory(initial, InventoryAuthority.AddLot(initial.Society.Society.Inventory,
            "market-consent-best-axe", "iron_axe", actor, 1));
        initial = WithInventory(initial, InventoryAuthority.AddLot(initial.Society.Society.Inventory,
            "market-consent-cloak", "rain_cloak", actor, 1));
        initial = WithInventory(initial, InventoryAuthority.AddLot(initial.Society.Society.Inventory,
            "market-consent-sack", "sack", actor, 1)) with
        {
            Agents = initial.Agents.Select(person => person.AgentId == actor ? person with
            {
                FullnessBasisPoints = 9_500,
                Survival = new SurvivalCondition(),
                Equipment = new PersonalEquipment("market-consent-cloak", "market-consent-sack"),
            } : person).ToArray(),
        };

        var policy = new ConsentPolicy(actor, stall.BuildingId);
        using var world = WorldRuntime.Restore(initial, policy.CreateProvider);
        policy.AtSite = () => initial.Map.FootDistance(Physical(world, actor).Position, position) <= 1;
        policy.Borrowed = () => CurrentMarket(world).Occupancies.Any(item => item.SellerAgentId == actor && item.EndedTick is null);
        policy.Deposited = () => CurrentMarket(world).StockReceipts.Any(item => item.SourceLotId == StockId);
        var walked = 0;
        try
        {
            // One runtime and one provider throughout this physical trip; no phase reload/context reset.
            await UntilAsync(world, () => policy.HeldBorrow.Task.IsCompleted, 400, () =>
            {
                Assert.Empty(CurrentMarket(world).Occupancies);
                Assert.Empty(CurrentMarket(world).StockReceipts);
                AssertOwnFourAxes(world, actor);
            }, actor, () => walked++);
            Assert.True(walked > 0);
            Assert.True(policy.AtSite());
            Assert.Contains(policy.Selected, item => item.Id.StartsWith("market_borrow:", StringComparison.Ordinal));
            for (var tick = 0; tick < 8; tick++)
            {
                await StepAsync(world);
                Assert.Empty(CurrentMarket(world).Occupancies);
                Assert.Empty(CurrentMarket(world).StockReceipts);
                AssertOwnFourAxes(world, actor);
            }
            var borrowHeldTick = policy.HeldBorrow.Task.Result;
            policy.ReleaseBorrow.TrySetResult(true);
            await UntilAsync(world, () => policy.Borrowed(), 32);
            var occupancy = Assert.Single(CurrentMarket(world).Occupancies);
            Assert.Equal(actor, occupancy.SellerAgentId);
            Assert.True(occupancy.StartedTick > borrowHeldTick);

            await UntilAsync(world, () => policy.HeldDeposit.Task.IsCompleted, 400, () =>
            {
                Assert.Empty(CurrentMarket(world).StockReceipts);
                AssertOwnFourAxes(world, actor);
            });
            Assert.True(policy.AtSite());
            var depositHeldTick = policy.HeldDeposit.Task.Result;
            for (var tick = 0; tick < 8; tick++)
            {
                await StepAsync(world);
                Assert.Empty(CurrentMarket(world).StockReceipts);
                AssertOwnFourAxes(world, actor);
                Assert.True(PersonalEquipmentRules.IsCarried(world.Society.Inventory.GetLot(StockId), actor));
                Assert.Null(world.Society.Inventory.GetLot(StockId).ContainerLotId);
            }
            var atSite = world.ExportState();
            var atSiteBytes = WorldRuntimeCodec.Encode(atSite);
            Assert.Equal(atSiteBytes, WorldRuntimeCodec.Encode(WorldRuntimeCodec.Decode(atSiteBytes)));

            // Exact offered deposit selection cannot bypass the routine/fallback boundary.
            await AssertNonPersonalDepositRefusedAsync(atSite, actor, stall.BuildingId, DecisionProviderKind.Deterministic, false);
            await AssertNonPersonalDepositRefusedAsync(atSite, actor, stall.BuildingId, DecisionProviderKind.LargeLanguageModel, true);
            await AssertOperativeFoodOrderAsync(atSite, actor, stall.BuildingId);

            policy.ReleaseDeposit.TrySetResult(true);
            await UntilAsync(world, () => policy.Deposited(), 32);
            var receipt = Assert.Single(CurrentMarket(world).StockReceipts);
            Assert.Equal((StockId, actor, 4), (receipt.SourceLotId, receipt.OwnerId, receipt.Quantity));
            Assert.True(receipt.DepositedTick > depositHeldTick);
            var deposited = world.Society.Inventory.GetLot(receipt.LotId);
            Assert.Equal((actor, 4), (deposited.OwnerId, deposited.Quantity));
            Assert.Null(deposited.CarrierId);
            Assert.Null(deposited.StorageBuildingId);
            Assert.Equal(new InventoryGroundPosition(position.X, position.Y), deposited.GroundPosition);
            AssertOwnFourAxes(world, actor);
            Assert.Single(world.ExportState().Events, item => item.Kind == "market_stall_borrowed" &&
                item.Detail.Contains(occupancy.Id, StringComparison.Ordinal));
            Assert.Single(world.ExportState().Events, item => item.Kind == "market_stock_delivered" &&
                item.Detail.Contains(receipt.Id, StringComparison.Ordinal));
            var saved = WorldRuntimeCodec.Encode(world.ExportState());
            using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), policy.CreateProvider);
            Assert.Equal(saved, WorldRuntimeCodec.Encode(replay.ExportState()));
            replay.Validate();
        }
        finally
        {
            policy.ReleaseBorrow.TrySetResult(true);
            policy.ReleaseDeposit.TrySetResult(true);
        }
    }

    private static async Task AssertNonPersonalDepositRefusedAsync(WorldRuntimeState atSite,
        string actor, string stallId, DecisionProviderKind kind, bool fail)
    {
        var provider = new Choices(actor, observation => Deposit(observation, stallId), kind, fail);
        using var world = WorldRuntime.Restore(atSite, id => id == actor ? provider : new Choices(id));
        await UntilAsync(world, () => provider.Offered.Any(candidate =>
            candidate.Id.StartsWith("market_deposit:", StringComparison.Ordinal)), 301);
        for (var tick = 0; tick < 8; tick++) await StepAsync(world);
        Assert.Empty(CurrentMarket(world).StockReceipts);
        AssertOwnFourAxes(world, actor);
        Assert.Single(CurrentMarket(world).Occupancies, item => item.EndedTick is null);
        world.Validate();
    }

    private static async Task AssertOperativeFoodOrderAsync(WorldRuntimeState atSite, string actor, string stallId)
    {
        var state = WithInventory(atSite, InventoryAuthority.AddLot(atSite.Society.Society.Inventory,
            "market-consent-order-berry", "berries", actor, 1)) with
        {
            Agents = atSite.Agents.Select(person => person.AgentId == actor
                ? person with { FullnessBasisPoints = 3_000 } : person).ToArray(),
        };
        var provider = new Choices(actor, observation => Deposit(observation, stallId));
        using var world = WorldRuntime.Restore(state, id => id == actor ? provider : new Choices(id));
        var foodBefore = CarriedFoodQuantity(world, actor);
        var order = world.SubmitInstruction(new("market-consent-eat-order", "owner:test", actor,
            OwnerInstructionKind.Order, "eat food"));
        Assert.Contains(world.ExportState().Instructions!, item => item.InstructionId == order.InstructionId &&
            item.State == OwnerInstructionState.Queued);
        await UntilAsync(world, () => world.ExportState().CompletedInstructionIds!.Contains(order.InstructionId), 32);
        Assert.DoesNotContain(provider.Offered, candidate => candidate.Id.StartsWith("market_", StringComparison.Ordinal));
        Assert.Equal(foodBefore - 1, CarriedFoodQuantity(world, actor));
        Assert.Contains(order.InstructionId.Value, world.ExportState().CompletedInstructionIds!);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "instruction_applied" &&
            item.Detail == order.InstructionId + ":consume_food");
        Assert.Empty(CurrentMarket(world).StockReceipts);
        AssertOwnFourAxes(world, actor);
        world.Validate();
    }

    // Call from MarketTradeScenario after its real open-offer checkpoint, before explicit-leave testing.
    internal static async Task AssertAutomaticDepartureAsync(WorldRuntimeState pending, string offerId)
    {
        var market = Assert.Single(Assert.Single(pending.Towns!).Markets);
        var trade = market.Trades.Single(item => item.OfferId == offerId);
        var originalOffer = pending.Society.Society.Inventory.GetOffer(offerId);
        Assert.Equal(DirectBarterState.Open, originalOffer.State);
        var seller = trade.SellerAgentId;
        var sellerPosition = pending.Agents.Single(person => person.AgentId == seller).Position;
        var marketTiles = MarketContent.SiteTiles(market.Site).ToArray();
        // The actual House adjoins this plaza, so a House errand can stop inside it.
        // Use controlled personal ground stock on existing reachable Town land;
        // neither a building nor an actor's position is changed to force departure.
        var destination = pending.TownLandTitles!.SelectMany(title => title.Tiles)
            .Where(point => pending.Map.IsBuildable(point) &&
                marketTiles.All(tile => pending.Map.FootDistance(tile, point) > 1) &&
                pending.Map.IsReachableOnFoot(sellerPosition, point))
            .OrderBy(point => pending.Map.FootDistance(sellerPosition, point)).ThenBy(point => point.Y).ThenBy(point => point.X).First();
        Assert.False(MarketTradeRules.IsInside(market, destination));
        Assert.DoesNotContain(marketTiles, point => pending.Map.FootDistance(point, destination) <= 1);
        const string errand = "market-consent-private-ground-cloth";
        var initial = WithInventory(pending, InventoryAuthority.AddLot(pending.Society.Society.Inventory,
            errand, "cloth", seller, 1, groundPosition: new InventoryGroundPosition(destination.X, destination.Y)));
        var beforeGoods = initial.Society.Society.Inventory.GetLot(originalOffer.FirstLotId);
        var beforePayment = initial.Society.Society.Inventory.GetLot(originalOffer.SecondLotId);
        var claims = initial.Society.Society.Inventory.Reservations.Where(claim => claim.Purpose == "barter:" + offerId).ToArray();
        Assert.Equal(2, claims.Length);
        Assert.All(claims, claim => Assert.Equal(InventoryReservationState.Reserved, claim.State));
        var choices = new Choices(seller, observation => observation.Candidates.SingleOrDefault(candidate =>
            candidate.Id == "household_collect:" + errand));
        using var world = WorldRuntime.Restore(initial, id => id == seller ? choices : new Choices(id));
        var steps = 0;
        await UntilAsync(world, () => CurrentMarket(world).Occupancies.Single(item => item.Id == trade.OccupancyId).EndedTick is not null,
            460, actor: seller, moved: () => steps++);
        Assert.True(steps > 0);
        Assert.Contains(choices.Selected, item => item.Id == "household_collect:" + errand);
        Assert.DoesNotContain(choices.Selected, item => item.Id.StartsWith("market_leave:", StringComparison.Ordinal));
        Assert.False(MarketTradeRules.IsInside(CurrentMarket(world), Physical(world, seller).Position));
        Assert.Equal(DirectBarterState.Cancelled, world.Society.Inventory.GetOffer(offerId).State);
        var cancelled = Assert.Single(world.ExportState().Events, item =>
            item.Payload is MarketTradeCancelledEventPayload payload && payload.OfferId == offerId);
        Assert.Null(cancelled.Payload.ActorId);
        Assert.Equal(trade.Position, cancelled.Position);
        Assert.All(claims, claim => Assert.Equal(InventoryReservationState.Released,
            world.Society.Inventory.GetReservation(claim.Id).State));
        AssertLotPlacementUnchanged(beforeGoods, world.Society.Inventory.GetLot(beforeGoods.Id));
        AssertLotPlacementUnchanged(beforePayment, world.Society.Inventory.GetLot(beforePayment.Id));
        Assert.Single(world.ExportState().Events, item => item.Kind == "market_stall_left" &&
            item.Detail.Contains(trade.OccupancyId, StringComparison.Ordinal));
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), id => new Choices(id));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(replay.ExportState()));
        replay.Validate();
    }

    private static void AssertLotPlacementUnchanged(InventoryLot before, InventoryLot after) =>
        Assert.Equal((before.Id, before.ItemKind, before.OwnerId, before.Quantity, before.CarrierId,
            before.StorageBuildingId, before.GroundPosition, before.DeliveryBuildingId),
            (after.Id, after.ItemKind, after.OwnerId, after.Quantity, after.CarrierId,
            after.StorageBuildingId, after.GroundPosition, after.DeliveryBuildingId));

    private static void AssertOwnFourAxes(WorldRuntime world, string actor)
    {
        var lots = world.Society.Inventory.Lots.Where(lot => lot.Id == StockId ||
            lot.Id.StartsWith(StockId + "#", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(lots);
        Assert.Equal(4, lots.Sum(lot => lot.Quantity));
        Assert.All(lots, lot => Assert.Equal(actor, lot.OwnerId));
    }

    private static int CarriedFoodQuantity(WorldRuntime world, string actor) =>
        world.Society.Inventory.Lots.Where(lot => lot.OwnerId == actor &&
            lot.ContainerLotId is null && lot.DeliveryBuildingId is null &&
            PersonalEquipmentRules.IsCarried(lot, actor) &&
            lot.ItemKind is "food" or "fruit" or "berries" or "wild_greens" or "cultivated_greens").Sum(lot => lot.Quantity);

    private static CognitionCandidate? Deposit(AgentObservation observation, string stallId) =>
        observation.Candidates.FirstOrDefault(candidate => candidate.DestinationId == stallId &&
            candidate.Id.StartsWith("market_deposit:", StringComparison.Ordinal) &&
            candidate.Description.StartsWith("Place 4 wooden_axe ", StringComparison.Ordinal));

    private static TownMarketState CurrentMarket(WorldRuntime world) => Assert.Single(Assert.Single(world.Towns).Markets);
    private static AgentState Physical(WorldRuntime world, string actor) =>
        world.Agents.Single(person => person.AgentId == actor);
    private static WorldRuntimeState WithInventory(WorldRuntimeState state, InventoryCheckpoint inventory) =>
        state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
    private static async Task StepAsync(WorldRuntime world)
    {
        Assert.True((await world.TickModelWorkAsync()).Advanced);
        await ModelWorkHarness.For(world).SettleAsync();
    }

    private static async Task UntilAsync(WorldRuntime world, Func<bool> reached, int maximum,
        Action? unchanged = null, string? actor = null, Action? moved = null)
    {
        var map = world.ExportState().Map;
        for (var tick = 0; tick < maximum && !reached(); tick++)
        {
            var before = actor is null ? default : Physical(world, actor).Position;
            await StepAsync(world);
            unchanged?.Invoke();
            if (actor is not null && before != Physical(world, actor).Position)
            {
                Assert.True(map.CanFootStep(before, Physical(world, actor).Position));
                moved?.Invoke();
            }
        }
        Assert.True(reached(), $"Market consent boundary was not reached within {maximum} ordinary ticks (now {world.WorldTick}).");
    }

    private sealed class ConsentPolicy(string actor, string stallId)
    {
        internal Func<bool> AtSite { get; set; } = () => false;
        internal Func<bool> Borrowed { get; set; } = () => false;
        internal Func<bool> Deposited { get; set; } = () => false;
        internal InlineCompletionSource<long> HeldBorrow { get; } = new(TaskCreationOptions.None);
        internal InlineCompletionSource<long> HeldDeposit { get; } = new(TaskCreationOptions.None);
        internal InlineCompletionSource<bool> ReleaseBorrow { get; } = new(TaskCreationOptions.None);
        internal InlineCompletionSource<bool> ReleaseDeposit { get; } = new(TaskCreationOptions.None);
        internal ConcurrentQueue<(string Id, long Tick)> Selected { get; } = new();

        internal Choices CreateProvider(string id) => id != actor ? new Choices(id) : new Choices(id,
            observation => Deposited() ? null : Borrowed() ? Deposit(observation, stallId) :
                observation.Candidates.FirstOrDefault(candidate => candidate.DestinationId == stallId &&
                    candidate.Id.StartsWith("market_borrow:", StringComparison.Ordinal)), beforeReply: async (request, selected) =>
            {
                Selected.Enqueue((selected.Id, request.Observation.WorldTick));
                if (!AtSite()) return;
                if (selected.Id.StartsWith("market_borrow:", StringComparison.Ordinal))
                {
                    HeldBorrow.TrySetResult(request.Observation.WorldTick);
                    await ReleaseBorrow.Task;
                }
                if (selected.Id.StartsWith("market_deposit:", StringComparison.Ordinal))
                {
                    HeldDeposit.TrySetResult(request.Observation.WorldTick);
                    await ReleaseDeposit.Task;
                }
            });
    }

    private sealed class Choices(string actor, Func<AgentObservation, CognitionCandidate?>? choose = null,
        DecisionProviderKind kind = DecisionProviderKind.LargeLanguageModel, bool fail = false,
        Func<CognitionDecisionRequest, CognitionCandidate, Task>? beforeReply = null) : IDecisionProvider
    {
        public DecisionProviderKind Kind => kind;
        public long ProviderEpoch => 1;
        internal ConcurrentQueue<CognitionCandidate> Offered { get; } = new();
        internal ConcurrentQueue<(string Id, long Tick)> Selected { get; } = new();

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            foreach (var candidate in request.Observation.Candidates) Offered.Enqueue(candidate);
            if (fail) throw new InvalidOperationException("Market personal provider fixture unavailable.");
            var candidates = request.Observation.Candidates;
            var selected = choose?.Invoke(request.Observation) ?? candidates.Where(candidate =>
                    candidate.DeterministicPriority <= 5 && candidate.Id is "consume_food" or "collect_shared_food" or
                        "take_food_from_pot" or "make_room_for_food" or "harvest_food" or "seek_food" or "wear_clothing" or "seek_warmth")
                .OrderBy(candidate => candidate.DeterministicPriority).FirstOrDefault() ??
                candidates.Single(candidate => candidate.Id == "safe_idle");
            Selected.Enqueue((selected.Id, request.Observation.WorldTick));
            if (beforeReply is not null) await beforeReply(request, selected);
            return new(request.RequestId, actor, Kind, request.ProviderEpoch, request.Observation.RunEpoch,
                request.Observation.DecisionGeneration, request.Observation.ObservationDigest, selected.Id, 1,
                candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == selected.Id ? 1d : 0d,
                    StringComparer.Ordinal));
        }
    }
}
