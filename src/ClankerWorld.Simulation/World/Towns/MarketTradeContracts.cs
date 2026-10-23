using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>The paid buildings and retained physical trading history of one Council-approved Market.</summary>
public sealed record TownMarketState(string Id, string ProjectId, string HallBuildingId, GridPoint Site,
    IReadOnlyList<MarketStallState> Stalls, IReadOnlyList<MarketStallOccupancy> Occupancies,
    IReadOnlyList<MarketStockReceipt> StockReceipts)
{
    [JsonRequired]
    public IReadOnlyList<MarketTradeState> Trades { get; init; } = [];

    [JsonRequired]
    public long NextStockReceiptSequence { get; init; }

    [JsonRequired]
    public long RetiredStockReceiptThrough { get; init; } = -1;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? RemovedTick { get; init; }
}

/// <summary>Validates trading authority separately from the Council's paid-building receipts.</summary>
public static class MarketTradeValidation
{
    internal static bool HasExactOpenClaim(InventoryCheckpoint inventory, DirectBarterOffer offer, bool first)
    {
        var lotId = first ? offer.FirstLotId : offer.SecondLotId;
        var index = InventoryIndex.For(inventory);
        if (index.Find(lotId) is not { } lot) return false;
        var holds = InventoryHoldRules.For(index, lot).OrderBy(hold => hold.Purpose)
            .ThenBy(hold => hold.HolderId, StringComparer.Ordinal).ToArray();
        return inventory.Reservations.FirstOrDefault(claim => claim.Id == DerivedId.Create(offer.Id + (first ? ":first" : ":second"))) ==
            InventoryStore.CreateInventoryReservation(Identity.Parse(DerivedId.Create(offer.Id + (first ? ":first" : ":second"))), first ? offer.FirstPartyId : offer.SecondPartyId, lotId, first ? offer.FirstQuantity : offer.SecondQuantity, "barter:" + offer.Id, offer.ExpiryTick, true, InventoryReservationState.Reserved, holds.Length == 0 ? null : new InventoryHoldSet(holds));
    }

    public static void Validate(IReadOnlyList<TownRuntimeState> towns, SocietyCheckpoint society,
        WorldMap map, WorldContentSimulationState simulation, DeclarativeWorldContentState content, long worldTick,
        IEnumerable<AgentState> physicalAgents)
    {
        var inventory = society.Inventory;
        var people = society.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var households = society.Households.Select(household => household.Id).ToHashSet(StringComparer.Ordinal);
        var markets = new HashSet<string>(StringComparer.Ordinal);
        var stalls = new HashSet<string>(StringComparer.Ordinal);
        var occupancies = new HashSet<string>(StringComparer.Ordinal);
        var activeSellers = new HashSet<string>(StringComparer.Ordinal);
        var receipts = new HashSet<string>(StringComparer.Ordinal);
        var trades = new HashSet<string>(StringComparer.Ordinal);
        foreach (var town in towns)
        {
            if (town.Markets is null) throw Invalid("A Town is missing its Market ledger.");
            foreach (var market in town.Markets)
            {
                if (market is null) throw Invalid("A Market ledger has invalid identity, bounds or retained state.");
                MarketSystem.ValidateLocalMarket(market);
                if (!markets.Add(market.Id) || !MarketContent.SiteTiles(market.Site).All(tile => tile.X >= 0 && tile.X < map.Width && tile.Y >= 0 && tile.Y < map.Height) ||
                    market.RemovedTick is { } removedAt && removedAt > worldTick)
                    throw Invalid("A Market ledger has invalid identity, bounds or retained state.");
                if (!content.Buildings.Any(item => item.CanonicalId == MarketContent.Hall2x2().CanonicalId) ||
                    !content.Buildings.Any(item => item.CanonicalId == MarketContent.Stall1x1().CanonicalId))
                    throw Invalid("A Market needs its authoritative building definitions.");
                if (market.RemovedTick is null && !simulation.Buildings.Any(building =>
                        building.InstanceId == market.HallBuildingId && building.TownId == town.Id &&
                        building.HouseholdId is null && building.Position == market.Site &&
                        building.DefinitionId == MarketContent.Hall2x2().CanonicalId))
                    throw Invalid("A live Market needs its actual Town-owned Hall.");
                foreach (var stall in market.Stalls)
                {
                    if (!stalls.Add(stall.BuildingId) || stall.BuiltTick > worldTick || stall.RemovedTick is { } removed && removed > worldTick)
                        throw Invalid("A Market stall has invalid paid-building identity or lifetime.");
                    var position = MarketContent.StallSite(market.Site, stall.SlotIndex);
                    if (stall.RemovedTick is null && !simulation.Buildings.Any(building =>
                            building.InstanceId == stall.BuildingId && building.TownId == town.Id &&
                            building.HouseholdId is null && building.Position == position &&
                            building.DefinitionId == MarketContent.Stall1x1().CanonicalId))
                        throw Invalid("A live stall needs its actual Town-owned paid building.");

                }
                foreach (var occupancy in market.Occupancies)
                {
                    if (!occupancies.Add(occupancy.Id) || !people.Contains(occupancy.SellerAgentId) || !households.Contains(occupancy.SellerHouseholdId) ||
                        occupancy.StartedTick > worldTick || occupancy.EndedTick is { } ended && ended > worldTick)
                        throw Invalid("A retained Market borrowing record is malformed.");
                    if (occupancy.EndedTick is null)
                    {
                        var seller = society.GetAgent(occupancy.SellerAgentId);
                        var physical = physicalAgents.FirstOrDefault(person => person.AgentId == occupancy.SellerAgentId);
                        if (!activeSellers.Add(occupancy.SellerAgentId) || market.RemovedTick is not null ||
                            market.Stalls.Single(stall => stall.BuildingId == occupancy.StallBuildingId).RemovedTick is not null ||
                            seller.Status != SocietyAgentStatus.Active || seller.HouseholdId != occupancy.SellerHouseholdId ||
                            !LifeStageRules.IsAdult(seller.AgeBand) ||
                            physical is null || !MarketTradeRules.IsInside(market, physical.Position))
                            throw Invalid("A live seller must borrow one existing stall for their actual household.");
                    }
                }
                for (var ordinal = 0; ordinal < market.StockReceipts.Count; ordinal++)
                {
                    var receipt = market.StockReceipts[ordinal];
                    if (!receipts.Add(receipt.Id) || receipt.DepositedTick > worldTick ||
                        receipt.InventoryEventId > inventory.EventHistoryFloor + inventory.Events.Count)
                        throw Invalid("A Market stock receipt has forged or inconsistent deposit authority.");
                    var retainedEvent = inventory.Events.FirstOrDefault(item => item.EventId == receipt.InventoryEventId);
                    if (receipt.InventoryEventId > inventory.EventHistoryFloor && (retainedEvent is null ||
                        retainedEvent.WorldTick != receipt.DepositedTick || retainedEvent.Kind != "inventory_relocated" ||
                        retainedEvent.Detail != receipt.Id + ":deposit:" + receipt.OwnerId + ":" + receipt.SourceLotId + ":" +
                            receipt.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                        throw Invalid("A Market receipt does not match its actual inventory deposit.");
                    if (inventory.Lots.Any(lot => MarketTradeRules.IsReceiptLot(receipt, lot) && lot.ItemKind != receipt.ItemKind))
                        throw Invalid("A Market receipt cannot relabel the deposited goods.");
                    // The historical owner is checked above against the deposit operation. A later
                    // estate, sale or physical collection may legitimately change the live lot owner.
                    if (receipt.TradeOfferId is { } offerId)
                    {
                        var trade = market.Trades.FirstOrDefault(item => item.OfferId == offerId);
                        var offer = inventory.Offers.FirstOrDefault(item => item.Id == offerId);
                        if (trade is null || offer is not { State: DirectBarterState.Settled } ||
                            trade.OccupancyId != receipt.OccupancyId || trade.SellerAgentId != receipt.SellerAgentId ||
                            trade.PaymentOwnerId != receipt.OwnerId || trade.PaymentKind != receipt.ItemKind ||
                            trade.SettledTick != receipt.DepositedTick || offer.SecondQuantity != receipt.Quantity ||
                            receipt.SourceLotId != offer.SecondLotId && receipt.SourceLotId != DerivedId.Create(offer.SecondLotId + "#barter:" + offerId))
                            throw Invalid("A Market payment deposit needs its actual exact settled exchange.");
                    }
                }
                foreach (var trade in market.Trades)
                {
                    var occupancy = market.Occupancies.Single(item => item.Id == trade.OccupancyId);
                    var stall = market.Stalls.Single(item => item.BuildingId == trade.StallBuildingId);
                    var offer = inventory.Offers.FirstOrDefault(item => item.Id == trade.OfferId);
                    var sourceReceipt = market.StockReceipts.FirstOrDefault(item =>
                        item.Sequence == trade.StockReceiptSequence);
                    if (!trades.Add(trade.OfferId) || offer is null ||
                        !people.Contains(trade.BuyerId) || trade.ProposedTick > worldTick ||
                        offer.Revision != 1 || offer.FirstPartyId != trade.GoodsOwnerId || offer.SecondPartyId != trade.BuyerId ||
                        offer.FirstQuantity != 1 || offer.SecondQuantity != 1 || offer.ExpiryTick != trade.ProposedTick + MarketTradeRules.OfferLifetimeTicks ||
                        (sourceReceipt is null
                            ? offer.State == DirectBarterState.Open || trade.StockReceiptSequence > market.RetiredStockReceiptThrough
                            : sourceReceipt.OccupancyId != occupancy.Id || sourceReceipt.OwnerId != trade.GoodsOwnerId ||
                              sourceReceipt.ItemKind != trade.GoodsKind || sourceReceipt.DepositedTick > trade.ProposedTick ||
                              offer.FirstLotId != sourceReceipt.LotId && !offer.FirstLotId.StartsWith(sourceReceipt.LotId + "#", StringComparison.Ordinal)))
                        throw Invalid("A Market exchange disagrees with its named seller, deposited stock or exact offer.");
                    if (offer.State == DirectBarterState.Open)
                    {
                        foreach (var first in new[] { true, false })
                        {
                            var claim = inventory.Reservations.FirstOrDefault(item => item.Id == DerivedId.Create(offer.Id + (first ? ":first" : ":second")));
                            // Missing or released claims are cancelled by runtime maintenance; retained live claims must be exact.
                            if (claim is not null && claim.State != InventoryReservationState.Released &&
                                !HasExactOpenClaim(inventory, offer, first))
                                throw Invalid("An open Market offer has a reservation that does not bind its exact exchange.");
                        }
                        var goods = inventory.Lots.FirstOrDefault(lot => lot.Id == offer.FirstLotId);
                        var payment = inventory.Lots.FirstOrDefault(lot => lot.Id == offer.SecondLotId);
                        if (occupancy.EndedTick is not null || market.RemovedTick is not null || stall.RemovedTick is not null ||
                            trade.SettledTick is not null || trade.CancellationReason is not null ||
                            !offer.AcceptedBy.SequenceEqual([trade.BuyerId], StringComparer.Ordinal) ||
                            goods is null || payment is null || goods.OwnerId != trade.GoodsOwnerId ||
                            goods.ItemKind != trade.GoodsKind || payment.ItemKind != trade.PaymentKind ||
                            !MarketTradeRules.IsAt(goods, trade.Position) || !MarketTradeRules.IsLoose(goods) ||
                            !MarketTradeRules.MaySell(market, occupancy, goods, society, trade.SellerAgentId) ||
                            payment.OwnerId != trade.BuyerId || !PersonalEquipmentRules.IsCarried(payment, trade.BuyerId) ||
                            !MarketTradeRules.IsLoose(payment) || payment.GroundPosition is not null || payment.StorageBuildingId is not null)
                            throw Invalid("An open Market offer must retain exact physical goods and the seller's own pending response.");
                    }
                    else if (offer.State == DirectBarterState.Settled)
                    {
                        if (trade.SettledTick is not { } settledAt || settledAt > worldTick ||
                            occupancy.EndedTick is { } ended && settledAt > ended || trade.CancellationReason is not null ||
                            !offer.AcceptedBy.SequenceEqual(new[] { trade.GoodsOwnerId, trade.BuyerId }.Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
                            market.StockReceipts.Count(receipt => receipt.TradeOfferId == offer.Id) != 1)
                            throw Invalid("A settled Market trade must keep both acceptances and its physical household payment.");
                    }
                    else if (trade.SettledTick is not null || !BoundedText.IsValid(trade.CancellationReason, 160, requireTrimmed: true) ||
                        !offer.AcceptedBy.SequenceEqual([trade.BuyerId], StringComparer.Ordinal))
                        throw Invalid("A cancelled Market trade must retain its refusal and release exact claims.");
                }
            }
        }
        if (inventory.Offers.Any(offer => offer.Id.Value.StartsWith(MarketTradeRules.OfferPrefix, StringComparison.Ordinal) &&
            !trades.Contains(offer.Id)))
            throw Invalid("A Market offer is missing its named physical stall binding.");
    }

    private static InvalidDataException Invalid(string detail) => new(detail);
}

public sealed record MarketStallState(string BuildingId, int SlotIndex, string ProjectId, long BuiltTick)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? RemovedTick { get; init; }
}

/// <summary>Borrowing never changes ownership of the Town stall or the goods left there.</summary>
public sealed record MarketStallOccupancy(string Id, string StallBuildingId, string SellerAgentId,
    long StartedTick, long? EndedTick = null, string? ReleaseReason = null)
{
    [JsonRequired]
    public string SellerHouseholdId { get; init; } = string.Empty;
}

/// <summary>OwnerId records the deposit; the live lot remains authoritative after inheritance or collection.</summary>
public sealed record MarketStockReceipt(Identity Id, string OccupancyId, string SellerAgentId, string OwnerId,
    string SourceLotId, string LotId, string ItemKind, int Quantity, long DepositedTick,
    long InventoryEventId, Identity? TradeOfferId = null)
{
    [JsonRequired]
    public long Sequence { get; init; }
}

public sealed record MarketTradeState(Identity OfferId, string OccupancyId, string StallBuildingId,
    string SellerAgentId, string GoodsOwnerId, string PaymentOwnerId, string BuyerId, GridPoint Position,
    long ProposedTick, string GoodsKind, string PaymentKind, long? SettledTick = null,
    string? CancellationReason = null)
{
    [JsonRequired]
    public long StockReceiptSequence { get; init; }
}

/// <summary>Trial quantities are provisional. Goods use physical ground custody, not private building access.</summary>
public static class MarketTradeRules
{
    public const int StallCapacity = 16;
    public const int LoadQuantity = 4;
    public const int OfferLifetimeTicks = 120;
    public const string OfferPrefix = "market-trade:";

    public static string Identity(string prefix, params string[] parts) =>
        DerivedId.Hash(JsonSerializer.Serialize(parts), prefix);

    public static string ReceiptId(string occupancyId, string sourceLotId, string ownerId,
        int quantity, long depositedTick, long ordinal, string? tradeOfferId = null) => Identity("market-stock:",
        occupancyId, sourceLotId, ownerId, quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
        depositedTick.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture), tradeOfferId ?? "");

    public static bool IsAt(InventoryLot lot, GridPoint position) => lot.ContainerLotId is null &&
        lot.CarrierId is null && lot.StorageBuildingId is null && lot.DeliveryBuildingId is null &&
        lot.GroundPosition == new InventoryGroundPosition(position.X, position.Y);

    public static bool IsLoose(InventoryLot lot) => lot.ContainerLotId is null &&
        !InventoryContainerRules.IsContainer(lot.ItemKind) && lot.DeliveryBuildingId is null;

    /// <summary>All actual on-site goods are visible, including goods from an earlier borrower.</summary>
    public static InventoryLot[] StockAt(TownMarketState market, string stallBuildingId, GridPoint position,
        InventoryCheckpoint inventory) => market.Stalls.Any(stall => stall.BuildingId == stallBuildingId)
        ? inventory.Lots.Where(lot => IsAt(lot, position)).OrderBy(lot => lot.Id, StringComparer.Ordinal).ToArray()
        : [];

    public static bool HasReceipt(TownMarketState market, MarketStallOccupancy occupancy, InventoryLot lot) =>
        market.StockReceipts.Any(receipt => receipt.OccupancyId == occupancy.Id &&
            receipt.SellerAgentId == occupancy.SellerAgentId && receipt.ItemKind == lot.ItemKind &&
            receipt.OwnerId == lot.OwnerId && IsReceiptLot(receipt, lot));

    public static bool IsReceiptLot(MarketStockReceipt receipt, InventoryLot lot) =>
        lot.Id == receipt.LotId || lot.ProvenanceLotId == receipt.LotId ||
        lot.Id.StartsWith(receipt.LotId + "#", StringComparison.Ordinal);

    public static bool IsInside(TownMarketState market, GridPoint position) =>
        MarketContent.SiteTiles(market.Site).Contains(position);

    /// <summary>Only the recorded person, never another member of their household, responds for this occupancy.</summary>
    public static bool MaySell(TownMarketState market, MarketStallOccupancy occupancy, InventoryLot lot,
        SocietyCheckpoint society, string actor) => occupancy.EndedTick is null &&
        occupancy.SellerAgentId == actor && society.Agents.Any(person => person.Id == actor &&
            person.Status == SocietyAgentStatus.Active &&
            person.HouseholdId == occupancy.SellerHouseholdId &&
            (lot.OwnerId == actor || person.HouseholdId is { } household && lot.OwnerId == household)) &&
        IsLoose(lot) && HasReceipt(market, occupancy, lot);
}
