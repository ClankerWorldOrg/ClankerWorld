using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class OrnamentTradeTests
{
    private const string Smith = "first-town-blacksmith";
    private sealed record MadeFixture(byte[] State, string Seller, string Buyer, string Lot);
    private static readonly Lazy<Task<MadeFixture>> GoldFixture = new(() => CreateMadeOrnament(false));
    private static readonly Lazy<Task<MadeFixture>> DiamondFixture = new(() => CreateMadeOrnament(true));

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task SmithPaymentCanUseASpareSingletonOrnamentButNeverTheWornUnit(bool diamond, bool worn)
    {
        var (state, seller, buyer, made) = await MadeOrnament(diamond);
        var owner = state.Society.Society.GetAgent(seller).HouseholdId!;
        var inventory = InventoryAuthority.Transfer(state.Society.Society.Inventory, "own-ornament-payment", owner,
            buyer, made, 1, "collect");
        inventory = InventoryAuthority.AddLot(inventory, "payment-smith-iron", "iron", owner, 2, storageBuildingId: Smith);
        inventory = InventoryAuthority.AddLot(inventory, "payment-smith-wood", "wood", owner, 2, storageBuildingId: Smith);
        var shop = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == Smith);
        using var producing = WorldRuntime.Restore(At(WithInventory(state, inventory), seller, shop.Position), _ => new Choices([]));
        var tool = producing.WorldContent.Recipes.Single(recipe => recipe.LocalId == "iron-axe");
        var started = producing.StartProduction(tool.CanonicalId, Smith, seller);
        Assert.True(started.Applied, started.Failure);
        var axe = started.JobId + ":output:00";
        await Until(producing, () => producing.Society.Inventory.Lots.Any(lot => lot.Id == axe), 30);
        state = producing.ExportState();
        state = At(state, buyer, FreePlace(state, shop.Position, buyer, 1, 1));
        state = At(state, seller, FreePlace(state, shop.Position, seller, 0, 1));
        if (worn) state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == buyer
                ? person with { Equipment = (person.Equipment ?? new PersonalEquipment()) with { OrnamentLotId = made } } : person).ToArray(),
        };
        var choices = new Choices(["shop_buy:" + Smith, "shop_continue:"]);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == buyer ? choices : id == seller ? new Choices(["shop_continue:"]) : new Choices([]));
        for (var tick = 0; tick < 35 && (worn || world.ShopTrades.Count == 0); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.NotEmpty(choices.Offered);
        if (worn)
        {
            Assert.DoesNotContain("shop_buy:" + Smith, choices.Offered);
            Assert.Empty(world.ShopTrades);
            Assert.Equal(buyer, world.Society.Inventory.GetLot(made).OwnerId);
            Assert.Equal(made, world.Agents.Single(person => person.AgentId == buyer).Equipment!.OrnamentLotId);
            Assert.Equal(owner, world.Society.Inventory.GetLot(axe).OwnerId);
        }
        else
        {
            var trade = Assert.Single(world.ShopTrades);
            var offer = world.Society.Inventory.GetOffer(trade.OfferId);
            Assert.Equal((axe, 1, made, 1), (offer.FirstLotId, offer.FirstQuantity, offer.SecondLotId, offer.SecondQuantity));
            await Until(world, () => world.Society.Inventory.GetOffer(offer.Id).State == DirectBarterState.Settled, 120);
            Assert.Equal((owner, Smith, 1), (world.Society.Inventory.GetLot(made).OwnerId,
                world.Society.Inventory.GetLot(made).StorageBuildingId, world.Society.Inventory.GetLot(made).Quantity));
            Assert.Equal(buyer, world.Society.Inventory.GetLot(axe).OwnerId);
        }
        world.Validate();
        Assert.Equal(1, world.Society.Inventory.GetLot(made).Quantity);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()),
            WorldRuntimeCodec.Encode(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AVisitorBuysOneActualMadeOrnamentAtThePrivateSmithAcrossReservedReload(bool diamond)
    {
        var (state, seller, buyer, made) = await MadeOrnament(diamond);
        var shop = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == Smith);
        var owner = shop.HouseholdId!;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "ornament-buyer-payment", "wood", buyer, 2);
        state = WithInventory(state, inventory);
        var buyerPlace = FreePlace(state, shop.Position, buyer, 1, 1);
        var sellerPlace = FreePlace(state, shop.Position, seller, 4, 6);
        state = At(At(state, buyer, buyerPlace), seller, sellerPlace);
        var buyerHousehold = state.Society.Society.GetAgent(buyer).HouseholdId;
        var buyerChoices = new Choices(["shop_buy:" + Smith, "shop_continue:"]);
        var sellerChoices = new Choices(["shop_continue:"]);
        IDecisionProvider Provider(string id) => id == buyer ? buyerChoices : id == seller ? sellerChoices : new Choices([]);
        using var world = WorldRuntime.Restore(state, Provider);
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        var setting = world.WorldContent.Recipes.Single(recipe => recipe.LocalId == "set-diamond");
        Assert.False(world.StartProduction(setting.CanonicalId, Smith, buyer).Applied);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        await Until(world, () => world.ShopTrades.Count == 1, 60);
        var trade = Assert.Single(world.ShopTrades);
        var offer = world.Society.Inventory.GetOffer(trade.OfferId);
        Assert.Equal((DirectBarterState.Open, made, 1, "ornament-buyer-payment", 1),
            (offer.State, offer.FirstLotId, offer.FirstQuantity, offer.SecondLotId, offer.SecondQuantity));
        Assert.True(world.ExportState().Map.FootDistance(world.Agents.Single(person => person.AgentId == seller).Position,
            shop.Position) > 1);
        Assert.Equal((owner, Smith, 1), (world.Society.Inventory.GetLot(made).OwnerId,
            world.Society.Inventory.GetLot(made).StorageBuildingId, world.Society.Inventory.GetLot(made).Quantity));
        var reservations = world.Society.Inventory.Reservations.Where(item => item.Purpose == "barter:" + offer.Id).ToArray();
        Assert.Equal(2, reservations.Length);
        Assert.All(reservations, item => Assert.Equal(InventoryReservationState.Reserved, item.State));
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var resumed = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), Provider);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(resumed.ExportState()));
        await Until(resumed, () => resumed.Society.Inventory.GetOffer(offer.Id).State == DirectBarterState.Settled, 120);
        var ornament = resumed.Society.Inventory.GetLot(made);
        Assert.Equal((buyer, 1, diamond ? OrnamentContent.DiamondOrnament : OrnamentContent.GoldOrnament),
            (ornament.OwnerId, ornament.Quantity, ornament.ItemKind));
        Assert.True(PersonalEquipmentRules.IsCarried(ornament, buyer));
        Assert.Null(ornament.StorageBuildingId);
        Assert.Null(ornament.DeliveryBuildingId);
        var payment = Assert.Single(resumed.Society.Inventory.Lots, lot => lot.ProvenanceLotId == "ornament-buyer-payment");
        Assert.Equal((owner, Smith, 1), (payment.OwnerId, payment.StorageBuildingId, payment.Quantity));
        Assert.Equal(1, resumed.Society.Inventory.GetLot("ornament-buyer-payment").Quantity);
        Assert.Equal(buyerHousehold, resumed.Society.GetAgent(buyer).HouseholdId);
        Assert.False(resumed.StartProduction(setting.CanonicalId, Smith, buyer).Applied);
        Assert.Contains("shop_buy:" + Smith, buyerChoices.Selected);
        Assert.Contains(sellerChoices.Selected, candidate => candidate.StartsWith("shop_continue:", StringComparison.Ordinal));
        Assert.All(reservations, item => Assert.Equal(InventoryReservationState.Completed,
            resumed.Society.Inventory.GetReservation(item.Id).State));
        Assert.Equal(1, resumed.Society.Inventory.Lots.Where(lot => lot.ItemKind == ornament.ItemKind).Sum(lot => lot.Quantity));
        resumed.Validate();
        Assert.Equal(WorldRuntimeCodec.Encode(resumed.ExportState()),
            WorldRuntimeCodec.Encode(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(resumed.ExportState()))));
    }

    [Fact]
    public async Task AOneUnitPersonalOrnamentBarterWalksBothPartiesToTheMeetingBeforeOwnershipChanges()
    {
        var (state, first, second, made) = await MadeOrnament(false);
        var owner = state.Society.Society.GetAgent(first).HouseholdId!;
        var inventory = InventoryAuthority.Transfer(state.Society.Society.Inventory, "personal-ornament", owner,
            first, made, 1, "collect");
        inventory = InventoryAuthority.AddLot(inventory, "ornament-barter-clothing", "clothing", second, 2);
        state = WithInventory(state, inventory);
        var startingSite = MeetingPoint(state);
        var firstStart = FreePlace(state, startingSite, first, 5, 7);
        state = At(state, first, firstStart);
        var secondStart = FreePlace(state, startingSite, second, 4, 6);
        state = At(state, second, secondStart);
        var firstChoices = new Choices(["trade_wait:", "trade_propose:" + second]);
        var secondChoices = new Choices(["trade_accept:"]);
        IDecisionProvider Provider(string id) => id == first ? firstChoices : id == second ? secondChoices : new Choices([]);
        using var world = WorldRuntime.Restore(state, Provider);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Empty(world.Society.Inventory.Offers);
        Assert.Equal(first, world.Society.Inventory.GetLot(made).OwnerId);
        Assert.NotEqual(firstStart, world.Agents.Single(person => person.AgentId == first).Position);
        await Until(world, () => world.Society.Inventory.Offers.Count == 1, 60);
        var offer = Assert.Single(world.Society.Inventory.Offers);
        Assert.Equal((DirectBarterState.Open, made, 1, "ornament-barter-clothing", 1),
            (offer.State, offer.FirstLotId, offer.FirstQuantity, offer.SecondLotId, offer.SecondQuantity));
        Assert.Equal([first], offer.AcceptedBy);
        Assert.InRange(state.Map.FootDistance(world.Agents.Single(person => person.AgentId == first).Position, startingSite), 0, 1);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var resumed = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), Provider);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(resumed.ExportState()));
        for (var tick = 0; tick < 120 && resumed.Society.Inventory.GetOffer(offer.Id).State == DirectBarterState.Open; tick++)
        {
            Assert.True((await resumed.AdvanceOneTickAsync()).Advanced);
            if (resumed.Society.Inventory.GetOffer(offer.Id).State == DirectBarterState.Open)
                Assert.Equal(first, resumed.Society.Inventory.GetLot(made).OwnerId);
        }
        Assert.Equal(DirectBarterState.Settled, resumed.Society.Inventory.GetOffer(offer.Id).State);
        Assert.Equal((second, 1), (resumed.Society.Inventory.GetLot(made).OwnerId,
            resumed.Society.Inventory.GetLot(made).Quantity));
        Assert.InRange(state.Map.FootDistance(resumed.Agents.Single(person => person.AgentId == first).Position, startingSite), 0, 1);
        Assert.InRange(state.Map.FootDistance(resumed.Agents.Single(person => person.AgentId == second).Position, startingSite), 0, 1);
        var clothes = Assert.Single(resumed.Society.Inventory.Lots, lot => lot.OwnerId == first &&
            lot.ProvenanceLotId == "ornament-barter-clothing");
        Assert.Equal(1, clothes.Quantity);
        Assert.Equal(1, resumed.Society.Inventory.GetLot("ornament-barter-clothing").Quantity);
        var reservations = resumed.Society.Inventory.Reservations.Where(item => item.Purpose == "barter:" + offer.Id).ToArray();
        Assert.Equal(2, reservations.Length);
        Assert.All(reservations, item => Assert.Equal(InventoryReservationState.Completed, item.State));
        resumed.Validate();
    }

    [Theory]
    [InlineData("worn")]
    [InlineData("reserved")]
    [InlineData("promised")]
    [InlineData("overloaded-recipient")]
    public async Task PersonalBarterCannotTakeWornReservedPromisedOrOverCapacityOrnamentStock(string boundary)
    {
        var (state, first, second, made) = await MadeOrnament(false);
        var owner = state.Society.Society.GetAgent(first).HouseholdId!;
        var inventory = InventoryAuthority.Transfer(state.Society.Society.Inventory, "guarded-personal-ornament", owner,
            first, made, 1, "collect");
        inventory = InventoryAuthority.AddLot(inventory, "ornament-barter-clothing", "clothing", second, 2);
        state = WithInventory(state, inventory);
        var startingSite = MeetingPoint(state);
        state = At(state, first, FreePlace(state, startingSite, first, 0, 1));
        state = At(state, second, FreePlace(state, startingSite, second, 0, 1));
        var controlChoices = new Choices(["trade_propose:" + second]);
        using (var control = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
                   id => id == first ? controlChoices : new Choices([])))
        {
            await Until(control, () => controlChoices.Offered.Contains("safe_idle"), 35);
            Assert.Contains("trade_propose:" + second, controlChoices.Offered);
            Assert.Single(control.Society.Inventory.Offers);
            control.Validate();
        }
        if (boundary == "promised") inventory = inventory with
        {
            Lots = inventory.Lots.Select(lot => lot.Id == made ? lot with { DeliveryBuildingId = Smith } : lot).ToArray(),
        };
        if (boundary == "reserved") inventory = InventoryAuthority.Reserve(inventory, "ornament-reserved-other-task",
            first, made, 1, "other_work", inventory.WorldTick + 120);
        if (boundary == "overloaded-recipient")
        {
            var carried = PersonalEquipmentRules.CarriedQuantity(inventory, second,
                state.Agents.Single(person => person.AgentId == second).Equipment);
            var capacity = PersonalEquipmentRules.Capacity(inventory, second,
                state.Agents.Single(person => person.AgentId == second).Equipment);
            inventory = InventoryAuthority.AddLot(inventory, "barter-capacity-ballast", "stone", second,
                capacity - carried + 1);
        }
        state = WithInventory(state, inventory);
        if (boundary == "worn") state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == first
                ? person with { Equipment = (person.Equipment ?? new PersonalEquipment()) with { OrnamentLotId = made } } : person).ToArray(),
        };
        var choices = new Choices(["trade_propose:" + second]);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
            WorldRuntimeCodec.Encode(state)), id => id == first ? choices : new Choices([]));
        await Until(world, () => choices.Offered.Contains("safe_idle"), 35);
        Assert.NotEmpty(choices.Offered);
        Assert.DoesNotContain("trade_propose:" + second, choices.Offered);
        Assert.Empty(world.Society.Inventory.Offers);
        Assert.Equal((first, 1), (world.Society.Inventory.GetLot(made).OwnerId, world.Society.Inventory.GetLot(made).Quantity));
        Assert.Equal(2, world.Society.Inventory.GetLot("ornament-barter-clothing").Quantity);
        if (boundary == "worn") Assert.Equal(made, world.Agents.Single(person => person.AgentId == first).Equipment!.OrnamentLotId);
        if (boundary == "reserved") Assert.Equal(InventoryReservationState.Reserved,
            world.Society.Inventory.GetReservation("ornament-reserved-other-task").State);
        if (boundary == "promised") Assert.Equal(Smith, world.Society.Inventory.GetLot(made).DeliveryBuildingId);
        world.Validate();
    }

    [Fact]
    public async Task FinalPersonalAcceptanceRechecksCapacityAfterAnOfferWasReserved()
    {
        var (state, first, second, made) = await MadeOrnament(false);
        var owner = state.Society.Society.GetAgent(first).HouseholdId!;
        var inventory = InventoryAuthority.Transfer(state.Society.Society.Inventory, "personal-capacity-ornament",
            owner, first, made, 1, "collect");
        inventory = InventoryAuthority.AddLot(inventory, "ornament-barter-clothing", "clothing", second, 2);
        state = WithInventory(state, inventory);
        var startingSite = MeetingPoint(state);
        state = At(state, first, FreePlace(state, startingSite, first, 0, 1));
        state = At(state, second, FreePlace(state, startingSite, second, 0, 1));
        using var offered = WorldRuntime.Restore(state, id => id == first
            ? new Choices(["trade_propose:" + second]) : new Choices([]));
        await Until(offered, () => offered.Society.Inventory.Offers.Count == 1, 20);
        var offer = Assert.Single(offered.Society.Inventory.Offers);
        state = offered.ExportState();
        var recipient = state.Agents.Single(person => person.AgentId == second);
        inventory = state.Society.Society.Inventory;
        var capacity = PersonalEquipmentRules.Capacity(inventory, second, recipient.Equipment);
        var carried = PersonalEquipmentRules.CarriedQuantity(inventory, second, recipient.Equipment);
        inventory = InventoryAuthority.AddLot(inventory, "arrival-capacity-ballast", "stone", second, capacity - carried + 1);
        state = WithInventory(state, inventory);
        var choices = new Choices(["trade_accept:"]);
        using var resumed = WorldRuntime.Restore(WorldRuntimeCodec.Decode(
            WorldRuntimeCodec.Encode(state)), id => id == second ? choices : new Choices(["trade_wait:"]));
        for (var tick = 0; tick < 35; tick++) Assert.True((await resumed.AdvanceOneTickAsync()).Advanced);
        Assert.Contains("trade_accept:" + offer.Id, choices.Selected);
        Assert.Equal(DirectBarterState.Open, resumed.Society.Inventory.GetOffer(offer.Id).State);
        Assert.Equal(first, resumed.Society.Inventory.GetLot(made).OwnerId);
        Assert.Equal(2, resumed.Society.Inventory.GetLot("ornament-barter-clothing").Quantity);
        Assert.Equal(capacity + 1, PersonalEquipmentRules.CarriedQuantity(resumed.Society.Inventory, second, recipient.Equipment));
        resumed.Validate();
        Assert.Equal(WorldRuntimeCodec.Encode(resumed.ExportState()),
            WorldRuntimeCodec.Encode(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(resumed.ExportState()))));
    }

    private static async Task<(WorldRuntimeState State, string Seller, string Buyer, string Made)> MadeOrnament(bool diamond)
    {
        // Cache only strict checkpoint bytes from actual paid native production.
        // Every case decodes fresh state and creates its own runtime/providers.
        var made = await (diamond ? DiamondFixture : GoldFixture).Value;
        return (WorldRuntimeCodec.Decode(made.State), made.Seller, made.Buyer, made.Lot);
    }

    private static async Task<MadeFixture> CreateMadeOrnament(bool diamond)
    {
        using var setup = NormalPathWorld.CreateGenerated("ornament-local-trade", _ => new Choices([]));
        var state = setup.ExportState();
        var smith = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == Smith);
        var owner = smith.HouseholdId!;
        var seller = state.Society.Society.Agents.First(person => person.HouseholdId == owner).Id;
        var buyer = state.Society.Society.Agents.First(person => person.HouseholdId != owner).Id;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "ornament-trade-gold", OrnamentContent.Gold,
            owner, 2, storageBuildingId: Smith);
        if (diamond) inventory = InventoryAuthority.AddLot(inventory, "ornament-trade-diamond", "diamond",
            owner, 1, storageBuildingId: Smith);
        using var made = WorldRuntime.Restore(At(WithInventory(state, inventory), seller, smith.Position), _ => new Choices([]));
        var recipe = made.WorldContent.Recipes.Single(item => item.LocalId == "gold-ornament");
        var started = made.StartProduction(recipe.CanonicalId, Smith, seller);
        Assert.True(started.Applied, started.Failure);
        var output = started.JobId + ":output:00";
        await Until(made, () => made.Society.Inventory.Lots.Any(lot => lot.Id == output), 24);
        var paid = made.WorldSimulation.ProductionJobs.Single(item => item.JobId == started.JobId).InputReservationIds
            .Select(made.Society.Inventory.GetReservation).ToArray();
        var gold = Assert.Single(paid);
        Assert.Equal(("ornament-trade-gold", owner, 2, InventoryReservationState.Completed),
            (gold.LotId, gold.OwnerId, gold.Quantity, gold.State));
        if (diamond)
        {
            recipe = made.WorldContent.Recipes.Single(item => item.LocalId == "set-diamond");
            started = made.StartProduction(recipe.CanonicalId, Smith, seller);
            Assert.True(started.Applied, started.Failure);
            var setOutput = started.JobId + ":output:00";
            await Until(made, () => made.Society.Inventory.Lots.Any(lot => lot.Id == setOutput), 28);
            var set = made.WorldSimulation.ProductionJobs.Single(item => item.JobId == started.JobId).InputReservationIds
                .Select(made.Society.Inventory.GetReservation).ToArray();
            Assert.Contains(set, item => item.LotId == output && item.Quantity == 1 && item.State == InventoryReservationState.Completed);
            Assert.Contains(set, item => item.LotId == "ornament-trade-diamond" && item.Quantity == 1 && item.State == InventoryReservationState.Completed);
            output = setOutput;
        }
        Assert.Equal((owner, Smith, 1), (made.Society.Inventory.GetLot(output).OwnerId,
            made.Society.Inventory.GetLot(output).StorageBuildingId, made.Society.Inventory.GetLot(output).Quantity));
        return new(WorldRuntimeCodec.Encode(made.ExportState()), seller, buyer, output);
    }

    private static GridPoint MeetingPoint(WorldRuntimeState state) =>
        state.Map.MapObjects.FirstOrDefault(item => item.Id == "storage")?.Position ??
        state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-warehouse").Position;

    private static GridPoint FreePlace(WorldRuntimeState state, GridPoint destination, string actor, int minimum, int maximum) =>
        state.Map.Tiles.Select(tile => tile.Position).Where(point => state.Map.IsPassable(point) &&
            state.Map.IsReachableOnFoot(point, destination) &&
            state.Map.FootDistance(point, destination) >= minimum && state.Map.FootDistance(point, destination) <= maximum &&
            state.Agents.All(person => person.AgentId == actor || person.Position != point))
        .OrderBy(point => point.Y).ThenBy(point => point.X).First();

    private static WorldRuntimeState At(WorldRuntimeState state, string actor, GridPoint position) => state with
    {
        Agents = state.Agents.Select(person => person.AgentId == actor ? person with
        { Position = position, FullnessBasisPoints = 7_500, Project = null, LastDecisionContext = null, TravelCooldownTicks = 0 } : person).ToArray(),
    };

    private static WorldRuntimeState WithInventory(WorldRuntimeState state, InventoryCheckpoint inventory) => state with
    { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };

    private static async Task Until(WorldRuntime world, Func<bool> complete, int limit)
    {
        for (var tick = 0; tick < limit && !complete(); tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True(complete(), "The native setup or exchange did not reach its required phase.");
    }

    private sealed class Choices(string[] preferences) : IDecisionProvider
    {
        public HashSet<string> Selected { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Offered { get; } = new(StringComparer.Ordinal);
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            foreach (var candidate in request.Observation.Candidates) Offered.Add(candidate.Id);
            var chosen = preferences.Select(prefix => request.Observation.Candidates.FirstOrDefault(candidate =>
                candidate.Id.StartsWith(prefix, StringComparison.Ordinal))).FirstOrDefault(candidate => candidate is not null)
                ?? request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            Selected.Add(chosen.Id);
            return new DeterministicDecisionProvider().DecideAsync(request with
            { Observation = request.Observation with { Candidates = [chosen] } }, cancellationToken);
        }
    }
}
