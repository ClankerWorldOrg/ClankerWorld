using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class MarketAnimalFoodReserveTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public async Task AnimalCareProtectsMealsWhileFamilyReserveIsOnSale(bool leaveFirst, bool removeStock, bool canCare)
    {
        var state = await PaidMarketWorld.StateAsync();
        var seller = state.Agents[0].AgentId;
        var household = PaidMarketWorld.HouseholdOf(state, seller);
        var members = state.Society.Society.GetHousehold(household).MemberIds;
        Assert.Equal(2, members.Count);
        var actor = members.Single(id => id != seller);
        var market = PaidMarketWorld.Market(state);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "reserve-yard-wood", "wood", household, 8);
        inventory = InventoryAuthority.AddLot(inventory, "reserve-yard-rope", "rope", household, 2);
        using var placing = WorldRuntime.Restore(PaidMarketWorld.WithInventory(state, inventory));
        var definition = placing.WorldContent.Buildings.Single(item => item.Tags.Contains("animal-yard"));
        var yard = state.Map.Tiles.OrderBy(tile => state.Map.FootDistance(tile.Position, state.Agents[0].Position))
            .Select(tile => placing.PlaceBuilding("reserve-yard", definition.CanonicalId, tile.Position, household))
            .First(result => result.Applied);
        state = placing.ExportState();
        var house = PaidMarketWorld.HouseOf(state, household);
        inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot =>
                !InventoryContainerRules.IsFood(lot.ItemKind) || lot.OwnerId != household && !members.Contains(lot.OwnerId)).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "retained-berries", "berries", household, 4, storageBuildingId: house.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, "sale-berries", "berries", household, 4);
        inventory = InventoryAuthority.Relocate(inventory, "seller-berries", "sale-berries", household, 4, carrierId: seller);
        state = PaidMarketWorld.At(PaidMarketWorld.WithInventory(state, inventory), seller, MarketContent.StallEntrance(market.Site, 0));
        var deposit = new MarketRulesPolicy
        {
            Choose = (id, candidates) => id == seller ? candidates.FirstOrDefault(candidate =>
                candidate.Id.StartsWith("market_borrow:", StringComparison.Ordinal)) ?? candidates.FirstOrDefault(candidate =>
                candidate.Id.StartsWith("market_deposit:", StringComparison.Ordinal) && candidate.Description.Contains(" berries ", StringComparison.Ordinal)) ??
                candidates.Single(candidate => candidate.Id == "safe_idle") : candidates.Single(candidate => candidate.Id == "safe_idle"),
        };
        using var stocking = WorldRuntime.Restore(state, deposit.CreateProvider);
        for (var tick = 0; tick < 20 && PaidMarketWorld.Market(stocking).StockReceipts.Count == 0; tick++)
            Assert.True((await stocking.AdvanceOneTickAsync()).Advanced);
        var receipt = Assert.Single(PaidMarketWorld.Market(stocking).StockReceipts);
        Assert.Equal(4, receipt.Quantity);
        state = stocking.ExportState();
        if (leaveFirst)
        {
            var leave = new MarketRulesPolicy
            {
                Choose = (id, candidates) => id == seller ? candidates.FirstOrDefault(candidate =>
                    candidate.Id.StartsWith("market_leave:", StringComparison.Ordinal)) ?? candidates.Single(candidate => candidate.Id == "safe_idle") :
                    candidates.Single(candidate => candidate.Id == "safe_idle"),
            };
            using var leaving = WorldRuntime.Restore(state, leave.CreateProvider);
            for (var tick = 0; tick < 20 && PaidMarketWorld.Market(leaving).Occupancies.Any(item => item.EndedTick is null); tick++)
                Assert.True((await leaving.AdvanceOneTickAsync()).Advanced);
            Assert.DoesNotContain(PaidMarketWorld.Market(leaving).Occupancies, item => item.EndedTick is null);
            Assert.Contains(leaving.ExportState().Events, item => item.Kind == "market_stall_left");
            state = leaving.ExportState();
        }
        inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.Id != "retained-berries" &&
                (!removeStock || !MarketTradeRules.IsReceiptLot(receipt, lot))).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "care-greens", "cultivated_greens", actor, 2);
        inventory = InventoryAuthority.AddLot(inventory, "care-jug", "water_jug", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "care-water", "fresh_water", actor, 2, containerLotId: "care-jug");
        var placedYard = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == yard.InstanceId);
        var cow = new AnimalState("market-reserve-cow", "Moss", "cow", "female",
            state.Society.Society.WorldTick - AnimalRules.Definition("cow").AdultDays * state.WorldSystems!.Config.TicksPerDay,
            placedYard.Position, "household:" + household, household, placedYard.InstanceId);
        state = PaidMarketWorld.At(PaidMarketWorld.WithInventory(state, inventory), actor, placedYard.Position) with
        {
            AnimalWorld = new(true, [cow], []),
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Agents = state.Agents.Select(person => person with
            { Position = person.AgentId == actor ? placedYard.Position : person.Position, FullnessBasisPoints = 9_500, LastDecisionContext = null, Project = null }).ToArray(),
        };
        var care = new MarketRulesPolicy
        {
            Choose = (id, candidates) => id == actor ? candidates.FirstOrDefault(candidate =>
                candidate.Id.StartsWith("animal:care:", StringComparison.Ordinal)) ?? candidates.Single(candidate => candidate.Id == "safe_idle") :
                candidates.Single(candidate => candidate.Id == "safe_idle"),
        };
        var initial = WorldRuntimeCodec.Encode(state);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(initial), care.CreateProvider);
        Assert.Equal(initial, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 8; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(canCare, world.Animals.Single().CareUntilTick > world.WorldTick);
        Assert.Equal(canCare ? 1 : 0, world.ExportState().Events.Count(item => item.Kind == "animal_cared"));
        Assert.Equal(canCare ? 0 : 2, world.Society.Inventory.Lots.Where(lot => lot.Id == "care-greens").Sum(lot => lot.Quantity));
        Assert.Equal(canCare ? 0 : 2, world.Society.Inventory.Lots.Where(lot => lot.Id == "care-water").Sum(lot => lot.Quantity));
        Assert.Equal(removeStock ? 0 : 4, world.Society.Inventory.Lots.Where(lot => MarketTradeRules.IsReceiptLot(receipt, lot)).Sum(lot => lot.Quantity));
        Assert.Equal(leaveFirst ? 0 : 1, PaidMarketWorld.Market(world).Occupancies.Count(item => item.EndedTick is null));
        Assert.Equal(canCare, care.OfferedTo(actor).Any(candidate => candidate.Id.StartsWith("animal:care:", StringComparison.Ordinal)));
        world.Validate();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), care.CreateProvider);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(reload.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await reload.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reload.ExportState()));
    }
}
