using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class MarketSystem
{
    internal static bool ResourceKind(string? value) => value is { Length: > 0 } && value == value.Trim() && !value.Any(char.IsControl);

    internal static bool Bounded(string? value, int maximum) => value is { Length: > 0 } &&
        value.Length <= maximum && value == value.Trim() && !value.Any(char.IsControl);

    internal static void ValidateLocalMarket(TownMarketState market)
    {
        if (market.Stalls is null || market.Occupancies is null || market.Trades is null || market.StockReceipts is null ||
            market.Stalls.Any(item => item is null) || market.Occupancies.Any(item => item is null) ||
            market.Trades.Any(item => item is null) || market.StockReceipts.Any(item => item is null))
            throw new InvalidDataException("Market record collections are missing or incomplete.");
        if (string.IsNullOrWhiteSpace(market.Id) || string.IsNullOrWhiteSpace(market.ProjectId) || market.Id != MarketContent.MarketId(market.ProjectId) || !Bounded(market.HallBuildingId, 512) ||
            market.NextStockReceiptSequence < 0 || market.RetiredStockReceiptThrough < -1 ||
            market.RetiredStockReceiptThrough >= market.NextStockReceiptSequence || market.Stalls.Count is < 2 or > MarketContent.MaximumStalls ||
            market.RemovedTick is < 0 || market.Stalls.Select(item => item.BuildingId).Distinct(StringComparer.Ordinal).Count() != market.Stalls.Count ||
            market.Occupancies.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != market.Occupancies.Count ||
            market.Trades.Select(item => item.OfferId).Distinct(StringComparer.Ordinal).Count() != market.Trades.Count ||
            market.StockReceipts.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != market.StockReceipts.Count)
            throw new InvalidDataException("A Market ledger has invalid identity, bounds or retained state.");
        if (market.Stalls.Select(stall => stall.SlotIndex).Distinct().Count() != market.Stalls.Count)
            throw new InvalidDataException("A Market cannot place two stalls in one slot.");
        foreach (var stall in market.Stalls)
        {
            if (stall.SlotIndex is < 0 or >= MarketContent.MaximumStalls || !Bounded(stall.ProjectId, 512) || stall.BuiltTick < 0 ||
                stall.RemovedTick is { } removed && removed < stall.BuiltTick)
                throw new InvalidDataException("A Market stall has invalid paid-building identity or lifetime.");
            var visits = market.Occupancies.Where(item => item.StallBuildingId == stall.BuildingId)
                .OrderBy(item => item.StartedTick).ThenBy(item => item.EndedTick ?? long.MaxValue).ToArray();
            for (var index = 1; index < visits.Length; index++)
                if (visits[index - 1].EndedTick is null || visits[index - 1].EndedTick > visits[index].StartedTick)
                    throw new InvalidDataException("Two sellers cannot borrow the same stall at once.");
        }
        foreach (var occupancy in market.Occupancies)
            if (!Bounded(occupancy.Id, 128) || !market.Stalls.Any(stall => stall.BuildingId == occupancy.StallBuildingId) ||
                occupancy.StartedTick < 0 || occupancy.EndedTick is { } ended && ended < occupancy.StartedTick ||
                (occupancy.EndedTick is null) != (occupancy.ReleaseReason is null) || occupancy.ReleaseReason is { } reason && !Bounded(reason, 160))
                throw new InvalidDataException("A retained Market borrowing record is malformed.");
        if (!market.StockReceipts.Select(receipt => receipt.Sequence).SequenceEqual(market.StockReceipts.Select(receipt => receipt.Sequence).Distinct().Order()))
            throw new InvalidDataException("Market stock receipt sequences must be unique and increasing.");
        if (market.StockReceipts.Count(receipt => receipt.Sequence > market.RetiredStockReceiptThrough) !=
            market.NextStockReceiptSequence - (market.RetiredStockReceiptThrough + 1))
            throw new InvalidDataException("Unretired Market stock receipt sequences cannot be missing.");
        foreach (var receipt in market.StockReceipts)
        {
            var occupancy = market.Occupancies.SingleOrDefault(item => item.Id == receipt.OccupancyId);
            if (occupancy is null || receipt.Sequence < 0 || receipt.Sequence >= market.NextStockReceiptSequence ||
                receipt.Id != MarketTradeRules.ReceiptId(receipt.OccupancyId, receipt.SourceLotId, receipt.OwnerId, receipt.Quantity,
                    receipt.DepositedTick, receipt.Sequence, receipt.TradeOfferId) || receipt.SellerAgentId != occupancy.SellerAgentId ||
                receipt.OwnerId != occupancy.SellerAgentId && receipt.OwnerId != occupancy.SellerHouseholdId ||
                !Bounded(receipt.SourceLotId, int.MaxValue) || !Bounded(receipt.LotId, int.MaxValue) || !ResourceKind(receipt.ItemKind) ||
                receipt.Quantity is <= 0 or > MarketTradeRules.LoadQuantity || receipt.DepositedTick < occupancy.StartedTick ||
                occupancy.EndedTick is { } ended && receipt.DepositedTick > ended || receipt.InventoryEventId <= 0 ||
                receipt.LotId != receipt.SourceLotId && receipt.LotId != DerivedId.Create(receipt.SourceLotId + "#move:" + TownRecordIds.DepositOperation(receipt.Id)))
                throw new InvalidDataException("A Market stock receipt has forged or inconsistent deposit authority.");
        }
        foreach (var trade in market.Trades)
        {
            var occupancy = market.Occupancies.SingleOrDefault(item => item.Id == trade.OccupancyId);
            var stall = market.Stalls.SingleOrDefault(item => item.BuildingId == trade.StallBuildingId);
            if (trade.OfferId is null || !trade.OfferId.StartsWith(MarketTradeRules.OfferPrefix, StringComparison.Ordinal) || occupancy is null || stall is null ||
                occupancy.StallBuildingId != stall.BuildingId || occupancy.SellerAgentId != trade.SellerAgentId || occupancy.SellerHouseholdId != trade.PaymentOwnerId ||
                trade.GoodsOwnerId != occupancy.SellerAgentId && trade.GoodsOwnerId != occupancy.SellerHouseholdId || trade.BuyerId == trade.SellerAgentId ||
                trade.Position != MarketContent.StallSite(market.Site, stall.SlotIndex) || trade.ProposedTick < occupancy.StartedTick ||
                occupancy.EndedTick is { } end && trade.ProposedTick > end || !ResourceKind(trade.GoodsKind) || !ResourceKind(trade.PaymentKind) ||
                trade.GoodsKind == trade.PaymentKind || trade.SettledTick is { } settled && settled < trade.ProposedTick ||
                trade.CancellationReason is { } reason && !Bounded(reason, 160) || trade.StockReceiptSequence < 0 || trade.StockReceiptSequence >= market.NextStockReceiptSequence ||
                trade.ProposedTick > long.MaxValue - MarketTradeRules.OfferLifetimeTicks)
                throw new InvalidDataException("A Market exchange disagrees with its named seller, deposited stock or exact offer.");
        }
    }
}
