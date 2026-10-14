using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Server.Observation;
using ClankerWorld.GodotClient.UI;
using ClankerWorld.GodotClient.Protocol;
using System.Text.Json;
using System.Globalization;

namespace ClankerWorld.Tests;

public sealed partial class TownLandTransferRuntimeTests
{
    [Fact]
    public async Task AHouseholdCanPublishAPaidUseRightOfferWithoutGivingAwayTheRight()
    {
        var provider = new TransferProvider { Sell = true, AllowPropose = true };
        using var world = NewWorld(provider);
        Configure(provider, world);
        var before = Permissions(world.ExportState());
        await UntilAsync(world, () => world.Towns[0].LandRecords.Transfers.Count == 1, 12, provider);
        var request = Assert.Single(world.Towns[0].LandRecords.Transfers);
        var price = Assert.IsType<TownLandSalePrice>(request.Price);
        Assert.Equal(provider.SourceHouseholdId, price.SellerHouseholdId);
        Assert.Equal("wood", price.ItemKind);
        Assert.Equal(2, price.Quantity);
        Assert.Equal("pending", request.Status);
        Assert.Equal(before, Permissions(world.ExportState()));
        Assert.Null(request.Receipt);
    }
    [Fact]
    public async Task ActualGoodsPaymentAndPermissionTransferSurvivePendingAndCompletedReload()
    {
        var provider = new TransferProvider { Sell = true, AllowPropose = true, PaySales = false };
        using var world = NewWorld(provider);
        Configure(provider, world);
        var before = world.ExportState();
        var sellerWood = world.Society.Inventory.Lots.Where(lot => lot.OwnerId == provider.SourceHouseholdId && lot.ItemKind == "wood").Sum(lot => lot.Quantity);
        var totalWood = world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "wood").Sum(lot => lot.Quantity);
        await UntilAsync(world, () => world.Towns[0].LandRecords.Transfers.Count > 0 && world.Towns[0].LandRecords.Transfers[0].Responses.Count == 4, 40, provider);
        var pending = Assert.Single(world.Towns[0].LandRecords.Transfers);
        Assert.Equal("pending", pending.Status);
        Assert.Null(pending.Receipt);
        Assert.Equal(Permissions(before), Permissions(world.ExportState()));
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => provider);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        var pendingClient = SaleClient(restored);
        Assert.NotNull(pendingClient.Price);
        Assert.Contains("awaiting goods payment", LandTransferText.Summary(pendingClient), StringComparison.Ordinal);
        Assert.Contains(LandTransferText.Details(pendingClient, tick => tick.ToString(CultureInfo.InvariantCulture)), line => line.Contains("Goods price: 2 wood", StringComparison.Ordinal));
        provider.PaySales = true;
        foreach (var actor in pending.Parties.SelectMany(party => party.AdultIds))
            restored.SubmitInstruction(new("sale-payment-" + actor, "owner:test", actor, OwnerInstructionKind.Suggestion,
                "Consider bringing the actual agreed goods to the notice place to finish the accepted land-use sale."));
        var collectionPlans = new List<LandSaleCollectionActionPlan>();
        var exchangePlans = new List<LandSaleExchangeActionPlan>();
        for (var tick = 0; tick < 100 && restored.Towns[0].LandRecords.Transfers[0].Status == "pending"; tick++)
        {
            var unchanged = WorldRuntimeCodec.Encode(restored.ExportState());
            Assert.False((await restored.AdvanceOneTickAsync(() => false)).Advanced);
            Assert.Equal(unchanged, WorldRuntimeCodec.Encode(restored.ExportState()));
            var result = await restored.AdvanceOneTickAsync();
            Assert.True(result.Advanced);
            foreach (var decision in result.Decisions.Where(decision => decision.ActionPlan is LandSaleCollectionActionPlan && decision.Admission.Accepted))
            {
                var plan = Assert.IsType<LandSaleCollectionActionPlan>(decision.ActionPlan);
                Assert.Equal(plan.Choice, LandSaleCollectionChoice.Decode(plan.CandidateId));
                Assert.Equal(pending.Id, plan.Request.Id);
                Assert.Equal(pending.Price, plan.Request.Price);
                Assert.Equal(provider.TargetHouseholdId, plan.Pickup.OwnerId);
                Assert.Equal("wood", plan.Pickup.ItemKind);
                Assert.InRange(plan.Quantity, 1, pending.Price!.Quantity);
                collectionPlans.Add(plan);
            }
            foreach (var decision in result.Decisions.Where(decision => decision.ActionPlan is LandSaleExchangeActionPlan && decision.Admission.Accepted))
            {
                var plan = Assert.IsType<LandSaleExchangeActionPlan>(decision.ActionPlan);
                Assert.Equal(plan.Choice, LandSaleExchangeChoice.Decode(plan.CandidateId));
                Assert.Equal(pending.Id, plan.Request.Id);
                Assert.Equal(pending.Price, plan.Request.Price);
                exchangePlans.Add(plan);
            }
        }
        Assert.True(restored.Towns[0].LandRecords.Transfers[0].Status == "transferred",
            string.Join("\n", provider.Selected.TakeLast(24)) + "\n" + JsonSerializer.Serialize(restored.ExportState().Agents.Select(person => new { person.AgentId, person.Position, person.MoveWaitTicks })) +
            "\n" + JsonSerializer.Serialize(restored.Society.Inventory.Lots.Where(lot => lot.ItemKind == "wood")) + "\n" + JsonSerializer.Serialize(restored.ExportState().Events.TakeLast(16)));
        var completed = Assert.Single(restored.Towns[0].LandRecords.Transfers);
        var payment = Assert.IsType<TownLandSalePayment>(completed.Receipt!.Payment);
        Assert.Equal(2, payment.Lots.Sum(lot => lot.Quantity));
        var paidPlan = Assert.Single(exchangePlans, plan => plan.Stage == LandSaleExchangeStage.Exchange);
        Assert.Equal(LandSaleExchangeKind.Pay, paidPlan.Choice.Kind);
        Assert.Equal(payment.BuyerAgentId, paidPlan.Actor);
        Assert.Equal(payment.SellerAgentId, paidPlan.Seller);
        Assert.Equal(payment.Position, paidPlan.NoticePlace);
        Assert.Equal(payment.Lots.Select(lot => new LandSalePaymentStock(lot.SourceLotId, lot.Quantity)), paidPlan.Goods);
        Assert.All(exchangePlans, plan => Assert.Equal(ActionRefusalReason.TargetUnavailable, restored.CheckActionPlan(plan).Refusal!.Reason));
        Assert.Equal(sellerWood + 2, restored.Society.Inventory.Lots.Where(lot => lot.OwnerId == provider.SourceHouseholdId && lot.ItemKind == "wood").Sum(lot => lot.Quantity));
        Assert.Equal(totalWood, restored.Society.Inventory.Lots.Where(lot => lot.ItemKind == "wood").Sum(lot => lot.Quantity));
        Assert.All(provider.Plot, tile => Assert.Contains(restored.HouseholdLandUseRights, right => right.HouseholdId == provider.TargetHouseholdId && right.Tiles.Contains(tile)));
        Assert.Equal(JsonSerializer.Serialize(before.TownLandTitles), JsonSerializer.Serialize(restored.ExportState().TownLandTitles));
        Assert.NotEmpty(collectionPlans);
        Assert.All(collectionPlans, plan => Assert.Equal(ActionRefusalReason.TargetUnavailable, restored.CheckActionPlan(plan).Refusal!.Reason));
        var selectedCollection = collectionPlans[0];
        var pendingState = WorldRuntimeCodec.Decode(saved);
        using (var pendingWorld = WorldRuntime.Restore(pendingState, _ => provider.ReplayPolicy()))
            Assert.True(pendingWorld.CheckActionPlan(selectedCollection).Allowed);
        var pendingInventory = pendingState.Society.Society.Inventory;
        var selectedStock = pendingInventory.GetLot(selectedCollection.Pickup.StockId);
        pendingInventory = InventoryAuthority.Reserve(pendingInventory, "sale-selected-reservation", selectedStock.OwnerId, selectedStock.Id,
            InventoryRules.UnreservedQuantity(InventoryIndex.For(pendingInventory), selectedStock), "another-real-use", 1000);
        pendingInventory = InventoryAuthority.AddLot(pendingInventory, "sale-alternative-stock", selectedStock.ItemKind, selectedStock.OwnerId,
            selectedCollection.Quantity, groundPosition: new(selectedCollection.Pickup.Position.X, selectedCollection.Pickup.Position.Y));
        using (var reservedWorld = WorldRuntime.Restore(pendingState with
        {
            Society = pendingState.Society with { Society = pendingState.Society.Society with { Inventory = pendingInventory } }
        }, _ => provider.ReplayPolicy()))
        {
            Assert.Equal(ActionRefusalReason.StockUnavailable, reservedWorld.CheckActionPlan(selectedCollection).Refusal!.Reason);
            reservedWorld.Validate();
        }
        Assert.Contains(provider.Selected, choice => choice.Contains("|land_transfer_collect_payment|", StringComparison.Ordinal));
        Assert.Contains(provider.Selected, choice => choice.Contains("|land_transfer_pay|", StringComparison.Ordinal));
        var completedClient = SaleClient(restored);
        Assert.Equal("wood", completedClient.Price!.ItemKind);
        Assert.Equal(payment.BuyerAgentId, completedClient.Payment!.BuyerAgentId);
        Assert.Equal(payment.SellerAgentId, completedClient.Payment.SellerAgentId);
        Assert.Contains(LandTransferText.Details(completedClient, tick => tick.ToString(CultureInfo.InvariantCulture)), line => line.StartsWith("Paid by ", StringComparison.Ordinal));
        restored.Validate();
        var final = WorldRuntimeCodec.Encode(restored.ExportState());
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(final), _ => provider.ReplayPolicy());
        Assert.Equal(final, WorldRuntimeCodec.Encode(reloaded.ExportState()));
        var completedState = restored.ExportState();
        foreach (var forgedPayment in new[]
        {
            payment with { Lots = payment.Lots.Select((lot, index) => index == 0 ? lot with { Quantity = lot.Quantity + 1 } : lot).ToArray() },
            payment with { BuyerAgentId = payment.SellerAgentId },
            payment with { Lots = payment.Lots.Select(lot => lot with { TransferEventId = long.MaxValue }).ToArray() }
        })
        {
            var changed = completed with { Receipt = completed.Receipt with { Payment = forgedPayment } };
            Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(completedState with
            {
                Towns = completedState.Towns!.Select(town => town with
                {
                    LandRecords = town.LandRecords with { Transfers = town.LandRecords.Transfers.Select(item => item.Id == changed.Id ? changed : item).ToArray() }
                }).ToArray()
            }, _ => provider.ReplayPolicy()));
        }
        for (var tick = 0; tick < 8; tick++)
        {
            Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
            Assert.True((await reloaded.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(restored.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
        }
    }

    [Theory]
    [InlineData("decline", "rejected")]
    [InlineData("withdraw", "withdrawn")]
    public async Task RefusingAPaidSaleLeavesGoodsAndPermissionsWithTheirOwners(string mode, string status)
    {
        var provider = new TransferProvider { Sell = true, AllowPropose = true, Mode = mode };
        using var world = NewWorld(provider);
        Configure(provider, world);
        var before = world.ExportState();
        var salePlans = new List<LandTransferProposalActionPlan>();
        await UntilAsync(world, () => world.Towns[0].LandRecords.Transfers.Count == 1, 20, provider, result =>
        {
            foreach (var decision in result.Decisions.Where(decision => decision.ActionPlan is LandTransferProposalActionPlan && decision.Admission.Accepted))
                salePlans.Add(Assert.IsType<LandTransferProposalActionPlan>(decision.ActionPlan));
        });
        var selectedPlan = Assert.Single(salePlans);
        Assert.True(selectedPlan.Bound);
        Assert.True(selectedPlan.Choice.Sale);
        Assert.Equal(selectedPlan.Choice, LandTransferProposalChoice.Decode(selectedPlan.CandidateId));
        Assert.Equal(provider.TargetHouseholdId, selectedPlan.TargetHousehold);
        Assert.Equal(provider.Plot, selectedPlan.Tiles);
        Assert.Equal(world.Towns[0].LandRecords.Transfers[0].Price, selectedPlan.Price);
        await UntilAsync(world, () => world.Towns[0].LandRecords.Transfers[0].Status != "pending", 80, provider);
        var rejected = Assert.Single(world.Towns[0].LandRecords.Transfers);
        Assert.Equal(status, rejected.Status);
        Assert.NotNull(rejected.Price);
        Assert.Null(rejected.Receipt);
        Assert.Equal(Permissions(before), Permissions(world.ExportState()));
        Assert.Equal(PrivateProperty(before), PrivateProperty(world.ExportState()));
        world.Validate();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => provider.ReplayPolicy());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(reloaded.ExportState()));
    }
    [Theory]
    [InlineData("missing")]
    [InlineData("reserved")]
    [InlineData("borrowed")]
    [InlineData("full")]
    public async Task ConsentCannotSpendMissingReservedBorrowedGoodsOrExceedCarrySpace(string barrier)
    {
        var provider = new TransferProvider { Sell = true, AllowPropose = true };
        using var source = NewWorld(provider);
        Configure(provider, source);
        var state = source.ExportState();
        var inventory = state.Society.Society.Inventory;
        var buyers = state.Society.Society.GetHousehold(provider.TargetHouseholdId).MemberIds;
        var stock = inventory.Lots.Where(lot => lot.OwnerId == provider.TargetHouseholdId && lot.ItemKind == "wood").ToArray();
        Assert.NotEmpty(stock);
        foreach (var lot in stock)
        {
            inventory = barrier == "reserved"
                ? InventoryAuthority.Reserve(inventory, "sale-reserved:" + lot.Id, lot.OwnerId, lot.Id, lot.Quantity, "another-real-use", 1000)
                : barrier is "missing" or "borrowed"
                    ? InventoryAuthority.Discard(inventory, lot.OwnerId, lot.Id, lot.Quantity) : inventory;
        }
        foreach (var buyer in buyers)
        {
            if (barrier == "borrowed")
            {
                var lotId = "sale-borrowed:" + buyer;
                inventory = InventoryAuthority.AddLot(inventory, lotId, "wood", provider.SourceHouseholdId, 2,
                    groundPosition: new(state.Towns![0].OriginSite!.Value.X, state.Towns[0].OriginSite!.Value.Y));
                inventory = InventoryAuthority.Relocate(inventory, "sale-loan:" + buyer, lotId, provider.SourceHouseholdId, 2, buyer);
            }
            if (barrier == "full")
            {
                var person = state.Agents.Single(item => item.AgentId == buyer);
                var free = PersonalEquipmentRules.FreeCapacity(inventory, buyer, person.Equipment);
                if (free > 0) inventory = InventoryAuthority.AddLot(inventory, "sale-full:" + buyer, "stone", buyer, free);
                Assert.Equal(0, PersonalEquipmentRules.FreeCapacity(inventory, buyer, person.Equipment));
            }
        }
        using var world = WorldRuntime.Restore(state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } }
        }, _ => provider);
        var before = world.ExportState();
        await UntilAsync(world, () => world.Towns[0].LandRecords.Transfers.Count == 1 && world.Towns[0].LandRecords.Transfers[0].Responses.Count == 4, 40, provider);
        for (var tick = 0; tick < 8; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var pending = Assert.Single(world.Towns[0].LandRecords.Transfers);
        Assert.Equal("pending", pending.Status);
        Assert.Null(pending.Receipt);
        Assert.Equal(Permissions(before), Permissions(world.ExportState()));
        Assert.Equal(PrivateProperty(before), PrivateProperty(world.ExportState()));
        Assert.DoesNotContain(provider.Selected, choice => choice.Contains("|land_transfer_pay|", StringComparison.Ordinal));
        Assert.DoesNotContain(provider.Selected, choice => choice.Contains("|land_transfer_collect_payment|", StringComparison.Ordinal));
        world.Validate();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => provider.ReplayPolicy());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Fact]
    public async Task CarriedGoodsCannotPayASellerWhoIsNotPhysicallyAtTheMeeting()
    {
        var provider = new TransferProvider { Sell = true, AllowPropose = true, PaySales = false };
        using var source = NewWorld(provider);
        Configure(provider, source);
        await UntilAsync(source, () => source.Towns[0].LandRecords.Transfers.Count == 1 && source.Towns[0].LandRecords.Transfers[0].Responses.Count == 4, 40, provider);
        var state = source.ExportState();
        var stock = state.Society.Society.Inventory.Lots.First(lot => lot.OwnerId == provider.TargetHouseholdId && lot.ItemKind == "wood" && lot.Quantity >= 2);
        var inventory = InventoryAuthority.Transfer(state.Society.Society.Inventory, "sale-prepared-buyer", stock.OwnerId,
            Beneficiary, stock.Id, 2, "household_goods_collected");
        var board = state.Towns![0].OriginSite!.Value;
        var far = Enumerable.Range(0, state.Map.Width * state.Map.Height).Select(index => new GridPoint(index % state.Map.Width, index / state.Map.Width))
            .First(point => state.Map.IsPassable(point) && state.Map.FootDistance(board, point) > 12);
        provider.PaySales = true;
        provider.HoldSellerMeeting = true;
        using var world = WorldRuntime.Restore(state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person.AgentId is Filer or SourcePartner ? person with { Position = far } : person).ToArray()
        }, _ => provider);
        var before = world.ExportState();
        world.SubmitInstruction(new("sale-meeting-payment", "owner:test", Beneficiary, OwnerInstructionKind.Suggestion,
            "Consider paying the accepted price to the seller adult at the notice place."));
        var waitingPlans = new List<LandSaleExchangeActionPlan>();
        for (var tick = 0; tick < 8; tick++)
        {
            var result = await world.AdvanceOneTickAsync();
            Assert.True(result.Advanced);
            waitingPlans.AddRange(result.Decisions.Where(decision => decision.ActionPlan is LandSaleExchangeActionPlan && decision.Admission.Accepted)
                .Select(decision => Assert.IsType<LandSaleExchangeActionPlan>(decision.ActionPlan)));
        }
        Assert.NotEmpty(waitingPlans);
        Assert.All(waitingPlans, plan => Assert.Null(plan.Seller));
        Assert.Contains(waitingPlans, plan => plan.Choice.Kind == LandSaleExchangeKind.Pay && plan.Stage == LandSaleExchangeStage.Wait);
        Assert.Contains(provider.Selected, choice => choice.Contains("|land_transfer_pay|", StringComparison.Ordinal));
        var unpaid = Assert.Single(world.Towns[0].LandRecords.Transfers);
        Assert.Equal("pending", unpaid.Status);
        Assert.Null(unpaid.Receipt);
        Assert.Equal(Permissions(before), Permissions(world.ExportState()));
        Assert.Equal(before.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId == provider.SourceHouseholdId && lot.ItemKind == "wood").Sum(lot => lot.Quantity),
            world.Society.Inventory.Lots.Where(lot => lot.OwnerId == provider.SourceHouseholdId && lot.ItemKind == "wood").Sum(lot => lot.Quantity));
        Assert.DoesNotContain(world.Society.Inventory.Events.Skip(before.Society.Society.Inventory.Events.Count),
            item => item.Detail.EndsWith(":land_use_right_payment", StringComparison.Ordinal));
        world.Validate();
    }

    [Theory]
    [InlineData(6, true)]
    [InlineData(8, false)]
    public async Task SplitCarriedFoodPaymentKeepsTheBuyersNeededReserve(int price, bool completes)
    {
        var provider = new TransferProvider
        {
            Sell = true,
            AllowPropose = true,
            PaySales = false,
            SaleItemKind = "food",
            SaleQuantity = price,
            CollectSalePayment = false
        };
        using var source = NewWorld(provider);
        Configure(provider, source);
        await UntilAsync(source, () => source.Towns[0].LandRecords.Transfers.Count == 1 &&
            source.Towns[0].LandRecords.Transfers[0].Responses.Count == 4, 40, provider);
        var state = source.ExportState();
        var inventory = state.Society.Society.Inventory;
        foreach (var lot in inventory.Lots.Where(lot => lot.OwnerId == Beneficiary && lot.ItemKind == "food").ToArray())
            inventory = InventoryAuthority.Discard(inventory, Beneficiary, lot.Id, lot.Quantity);
        inventory = InventoryAuthority.AddLot(inventory, "sale-food-a", "food", Beneficiary, 4);
        inventory = InventoryAuthority.AddLot(inventory, "sale-food-b", "food", Beneficiary, 4);
        provider.PaySales = true;
        using var world = WorldRuntime.Restore(state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person with
            { Position = state.Towns![0].OriginSite!.Value, FullnessBasisPoints = 10_000 }).ToArray()
        }, _ => provider);
        var before = world.ExportState();
        var sellerFood = inventory.Lots.Where(lot => lot.OwnerId == provider.SourceHouseholdId && lot.ItemKind == "food").Sum(lot => lot.Quantity);
        world.SubmitInstruction(new("split-food-payment", "owner:test", Beneficiary, OwnerInstructionKind.Suggestion,
            "Consider paying the accepted food price from your actual carried surplus, keeping your needed food."));
        for (var tick = 0; tick < 8 && world.Towns[0].LandRecords.Transfers[0].Status == "pending"; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var result = Assert.Single(world.Towns[0].LandRecords.Transfers);
        Assert.Equal(completes ? "transferred" : "pending", result.Status);
        Assert.True(world.Society.Inventory.Lots.Where(lot => lot.OwnerId == Beneficiary && lot.ItemKind == "food" &&
            PersonalEquipmentRules.IsCarried(lot, Beneficiary)).Sum(lot => lot.Quantity) >= 2);
        if (completes)
        {
            var payment = Assert.IsType<TownLandSalePayment>(result.Receipt!.Payment);
            Assert.Equal(price, payment.Lots.Sum(lot => lot.Quantity));
            Assert.Equal(2, payment.Lots.Count);
            Assert.Equal(sellerFood + price, world.Society.Inventory.Lots.Where(lot =>
                lot.OwnerId == provider.SourceHouseholdId && lot.ItemKind == "food").Sum(lot => lot.Quantity));
        }
        else
        {
            Assert.Null(result.Receipt);
            Assert.Equal(Permissions(before), Permissions(world.ExportState()));
            Assert.DoesNotContain(provider.Selected, choice => choice.StartsWith(Beneficiary + ":", StringComparison.Ordinal) &&
                choice.Contains("|land_transfer_pay|", StringComparison.Ordinal));
            Assert.DoesNotContain(world.Society.Inventory.Events.Skip(inventory.Events.Count),
                item => item.Detail.EndsWith(":land_use_right_payment", StringComparison.Ordinal));
        }
        world.Validate();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => provider.ReplayPolicy());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CollectingLandSaleGoodsKeepsTheLoadedReturnToTheNoticePlacePossible(bool swimOnly)
    {
        var provider = new TransferProvider
        { Sell = true, AllowPropose = true, PaySales = false, SaleQuantity = 4, PaymentActor = Beneficiary };
        using var source = NewWorld(provider, seed: "cart-set-unfinished");
        Configure(provider, source);
        await UntilAsync(source, () => source.Towns[0].LandRecords.Transfers.Count == 1 &&
            source.Towns[0].LandRecords.Transfers[0].Responses.Count == 4, 40, provider);
        var state = source.ExportState();
        var board = state.Towns![0].OriginSite!.Value;
        var approaches = state.Map.Tiles.Select(tile => tile.Position)
            .Where(point => state.Map.IsPassable(point) && state.Map.FootDistance(point, board) <= 1).ToArray();
        var pickup = state.Map.Tiles.Select(tile => tile.Position).First(point => state.Map.IsBuildable(point) &&
            state.Map.FootDistance(point, board) > 1 && SwimmingRules.IsReachable(state.Map, point, board) &&
            approaches.All(destination => !state.Map.IsReachableOnFoot(point, destination)) == swimOnly);
        Assert.NotEmpty(approaches);
        Assert.Equal(swimOnly, approaches.All(destination => !state.Map.IsReachableOnFoot(pickup, destination)));
        var inventory = state.Society.Society.Inventory;
        foreach (var lot in inventory.Lots.Where(lot => lot.OwnerId == Beneficiary ||
            lot.OwnerId == provider.TargetHouseholdId && lot.ItemKind == "wood").ToArray())
            inventory = InventoryAuthority.Discard(inventory, lot.OwnerId, lot.Id, lot.Quantity);
        inventory = InventoryAuthority.AddLot(inventory, "sale-return-stone", "stone", Beneficiary, 1);
        inventory = InventoryAuthority.AddLot(inventory, "sale-return-wood", "wood", provider.TargetHouseholdId, 4,
            groundPosition: new(pickup.X, pickup.Y));
        provider.PaySales = true;
        using var world = WorldRuntime.Restore(state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person.AgentId == Beneficiary ? person with
            {
                Position = pickup,
                FullnessBasisPoints = 10_000,
                Equipment = null,
                Project = null,
                LastDecisionContext = null,
                Survival = new SurvivalCondition(10_000)
            } : person).ToArray()
        }, _ => provider);
        world.SubmitInstruction(new("sale-loaded-return", "owner:test", Beneficiary, OwnerInstructionKind.Suggestion,
            "Consider collecting your household's agreed goods while retaining a route back to the notice place."));
        for (var tick = 0; tick < 8 && !world.Society.Inventory.Lots.Any(lot => lot.OwnerId == Beneficiary && lot.ItemKind == "wood"); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var collected = Assert.Single(world.Society.Inventory.Lots, lot => lot.OwnerId == Beneficiary && lot.ItemKind == "wood");
        Assert.Equal(swimOnly ? 3 : 4, collected.Quantity);
        Assert.Equal(swimOnly ? 4 : 5, PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, Beneficiary, null));
        Assert.Equal(4, world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "wood" &&
            (lot.OwnerId == Beneficiary || lot.OwnerId == provider.TargetHouseholdId)).Sum(lot => lot.Quantity));
        Assert.Contains(provider.Selected, choice => choice.StartsWith(Beneficiary + ":", StringComparison.Ordinal) &&
            choice.Contains("|land_transfer_collect_payment|", StringComparison.Ordinal));
        var sale = Assert.Single(world.Towns[0].LandRecords.Transfers);
        Assert.Equal("pending", sale.Status);
        Assert.Null(sale.Receipt);
        if (swimOnly)
        {
            var remaining = world.Society.Inventory.GetLot("sale-return-wood");
            Assert.Equal((provider.TargetHouseholdId, 1, new InventoryGroundPosition(pickup.X, pickup.Y)),
                (remaining.OwnerId, remaining.Quantity, remaining.GroundPosition));
        }
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => provider.ReplayPolicy());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        world.Validate();
        restored.Validate();
    }

    [Fact]
    public async Task PayingASalePublishesTheResultOfAnOverlappingOfferItInvalidates()
    {
        var provider = new TransferProvider
        { Sell = true, AllowPropose = true, PaySales = false, PaymentActor = Beneficiary };
        using var source = NewWorld(provider);
        Configure(provider, source);
        await UntilAsync(source, () => source.Towns[0].LandRecords.Transfers.Count == 1 &&
            source.Towns[0].LandRecords.Transfers[0].Responses.Count == 4, 40, provider);
        var state = source.ExportState();
        var town = state.Towns![0];
        var sale = Assert.Single(town.LandRecords.Transfers);
        var noticeId = "notice:" + (town.Council!.Notices.Count + 1);
        var hearings = TownLandTransferRules.Propose(town.LandRecords, state.Map, town.Id, Filer,
            sale.Tiles, sale.TargetHouseholdId, state.HouseholdLandUseRights!, sale.Parties,
            state.HouseholdLandUseRequests!, source.WorldTick, noticeId,
            sale.Price! with { Quantity = sale.Price.Quantity + 1 });
        var competing = hearings.Transfers[^1];
        var council = TownCouncilRules.PostNotice(town.Council, "land_transfer",
            TownLandTransferRules.TermsToken(competing), "Another exact goods price for the same plot.", source.WorldTick);
        var stock = state.Society.Society.Inventory.Lots.First(lot => lot.OwnerId == provider.TargetHouseholdId &&
            lot.ItemKind == "wood" && lot.Quantity >= 2);
        var inventory = InventoryAuthority.Transfer(state.Society.Society.Inventory, "competing-sale-carried-price",
            stock.OwnerId, Beneficiary, stock.Id, 2, "household_goods_collected");
        provider.PaySales = true;
        provider.CollectSalePayment = false;
        using var world = WorldRuntime.Restore(state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person with
            { Position = town.OriginSite!.Value, FullnessBasisPoints = 10_000 }).ToArray(),
            Towns = state.Towns.Select(item => item.Id == town.Id ? item with
            { Council = council, LandRecords = hearings } : item).ToArray()
        }, _ => provider);
        world.SubmitInstruction(new("competing-sale-payment", "owner:test", Beneficiary, OwnerInstructionKind.Suggestion,
            "Consider paying the agreed original offer at the notice place."));
        await UntilAsync(world, () => world.Towns[0].LandRecords.Transfers[0].Status == "transferred", 40, provider);
        var result = world.Towns[0].LandRecords.Transfers.Single(item => item.Id == competing.Id);
        Assert.Equal("invalidated", result.Status);
        Assert.Equal("rights_changed", result.Reason);
        Assert.Null(result.Receipt);
        Assert.Single(world.Towns[0].Council!.Notices, notice => notice.Kind == "result" && notice.SubjectId == competing.Id);
        Assert.Single(world.ExportState().Events, item => item.Kind == "land_transfer_blocked" &&
            item.Detail.StartsWith(town.Id + "|" + competing.Id + "|", StringComparison.Ordinal));
        Assert.Single(world.Towns[0].Council!.Notices, notice => notice.Kind == "result" && notice.SubjectId == sale.Id);
        world.Validate();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => provider.ReplayPolicy());
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    private static OwnerLandTransfer SaleClient(WorldRuntime world)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var snapshot = new OwnerWorldObservationStore(world).GetSnapshot();
        var client = JsonSerializer.Deserialize<ClankerWorld.GodotClient.Protocol.OwnerWorldSnapshot>(JsonSerializer.Serialize(snapshot, options), options)!;
        return Assert.Single(Assert.Single(client.Towns).LandTransfers);
    }
}
