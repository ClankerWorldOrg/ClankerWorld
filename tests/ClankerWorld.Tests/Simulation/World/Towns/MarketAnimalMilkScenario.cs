using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

internal static class MarketAnimalMilkScenario
{
    internal static async Task AssertPipelineAsync(WorldRuntimeState boundary)
    {
        var town = boundary.Towns![0];
        var market = town.Markets.Single();
        Assert.Empty(market.StockReceipts);
        Assert.Empty(market.Trades);
        // A valid restored retirement boundary makes the next sequence differ from the retained row count.
        market = market with { NextStockReceiptSequence = 40, RetiredStockReceiptThrough = 39 };
        var stall = market.Stalls[0];
        var site = MarketContent.StallSite(market.Site, stall.SlotIndex);
        var seller = town.ResidentIds.First(id => boundary.Society.Society.GetInhabitant(id).AgeBand == SocietyAgeBand.Adult);
        var home = boundary.Society.Society.GetInhabitant(seller).HouseholdId!;
        var buyer = town.ResidentIds.First(id => boundary.Society.Society.GetInhabitant(id).AgeBand == SocietyAgeBand.Adult &&
            boundary.Society.Society.GetInhabitant(id).HouseholdId != home);
        var inventory = InventoryAuthority.AddLot(boundary.Society.Society.Inventory, "market-milk", "milk", home, 2);
        inventory = InventoryAuthority.Relocate(inventory, "milk-fixture-carried", "market-milk", home, 2, carrierId: seller);
        inventory = InventoryAuthority.AddLot(inventory, "market-milk-payment", "stone", buyer, 2);
        var house = boundary.WorldSimulation!.Buildings.First(item => item.HouseholdId == home &&
            boundary.WorldContent!.Buildings.Any(definition => definition.CanonicalId == item.DefinitionId && definition.Tags.Contains("house")));
        inventory = InventoryAuthority.AddLot(inventory, "market-kept-milk", "milk", home,
            2 * boundary.Society.Society.GetHousehold(home).MemberIds.Count, storageBuildingId: house.InstanceId);
        var state = boundary with
        {
            Towns = boundary.Towns!.Select(item => item.Id == town.Id ? item with { Markets = [market] } : item).ToArray(),
            Society = boundary.Society with { Society = boundary.Society.Society with { Inventory = inventory } },
            Inhabitants = boundary.Inhabitants.Select(person => person with
            {
                Position = person.InhabitantId == seller ? site : person.InhabitantId == buyer ? new(site.X + 1, site.Y) : person.Position,
                HungerBasisPoints = person.InhabitantId == buyer ? 5000 : 9500,
                LastDecisionContext = null,
                Project = null,
            }).ToArray(),
        };
        var choices = new Choices(seller, buyer);
        using var world = WorldRuntime.Restore(state, _ => choices);
        await Until(() => world.Towns[0].Markets[0].Occupancies.Any(item => item.EndedTick is null && item.SellerAgentId == seller));
        choices.Mode = "stock";
        await Until(() => world.Towns[0].Markets[0].StockReceipts.Any(receipt => receipt.LotId == "market-milk"));
        Assert.Equal((home, new InventoryGroundPosition(site.X, site.Y)), (world.Society.Inventory.GetLot("market-milk").OwnerId,
            world.Society.Inventory.GetLot("market-milk").GroundPosition));
        choices.Mode = "sale";
        await Until(() => world.Society.Inventory.Lots.Any(lot => lot.ItemKind == "milk" && lot.OwnerId == buyer));
        Assert.Equal((home, buyer, 1), (world.Society.Inventory.GetLot("market-milk").OwnerId,
            Assert.Single(world.Society.Inventory.Lots, lot => lot.ItemKind == "milk" && lot.OwnerId == buyer).OwnerId, world.Society.Inventory.GetLot("market-milk").Quantity));
        var payment = Assert.Single(world.Society.Inventory.Lots, lot => lot.ProvenanceLotId == "market-milk-payment");
        Assert.Equal(home, payment.OwnerId);
        Assert.Contains(world.Towns[0].Markets[0].StockReceipts, receipt => receipt.LotId == payment.Id);
        Assert.Equal(new long[] { 40, 41 }, world.Towns[0].Markets[0].StockReceipts.Select(receipt => receipt.Sequence));
        Assert.Equal(42, world.Towns[0].Markets[0].NextStockReceiptSequence);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => choices);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reload.ExportState()));
        choices.Mode = "leave";
        await Until(() => world.Towns[0].Markets[0].Occupancies.All(item => item.EndedTick is not null));
        choices.Mode = "recover";
        await Until(() => PersonalEquipmentRules.IsCarried(world.Society.Inventory.GetLot("market-milk"), seller));
        Assert.Equal(home, world.Society.Inventory.GetLot("market-milk").OwnerId);
        Assert.True(PersonalEquipmentRules.IsPhysicallyCarried(world.Society.Inventory, world.Society.Inventory.GetLot("market-milk"), seller));
        world.Validate();

        async Task Until(Func<bool> reached)
        {
            for (var tick = 0; tick < 40 && !reached(); tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True(reached(), "Milk Market phase did not complete: " + choices.Mode);
        }
    }

    private sealed class Choices(string seller, string buyer) : IDecisionProvider
    {
        public string Mode { get; set; } = "borrow";
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            var candidates = observation.Candidates;
            var choice = candidates.FirstOrDefault(candidate => observation.InhabitantId == seller && (Mode switch
            {
                "borrow" => candidate.Description.Contains("borrow this empty", StringComparison.Ordinal),
                "stock" => candidate.Id.StartsWith("market_deposit:", StringComparison.Ordinal) && candidate.Description.Contains(" milk at your borrowed stall;", StringComparison.Ordinal),
                "sale" => candidate.Id.StartsWith("market_continue:", StringComparison.Ordinal),
                "leave" => candidate.Description.StartsWith("Give up this stall", StringComparison.Ordinal),
                "recover" => candidate.Id.StartsWith("market_collect:", StringComparison.Ordinal) && candidate.Description.Contains("milk", StringComparison.Ordinal),
                _ => false,
            }) || observation.InhabitantId == buyer && Mode == "sale" && candidate.Id.StartsWith("market_buy:", StringComparison.Ordinal) && candidate.Description.Contains(" for 1 milk;", StringComparison.Ordinal)) ??
                candidates.Single(candidate => candidate.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.InhabitantId, Kind, 0,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, choice.Id, 1,
                candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == choice.Id ? 1d : 0d)));
        }
    }
}
