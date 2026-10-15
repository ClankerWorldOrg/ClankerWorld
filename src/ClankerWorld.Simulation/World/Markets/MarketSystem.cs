namespace ClankerWorld.Simulation.World;

internal sealed partial class MarketSystem
{
    private readonly List<ShopTradeState> shopTrades = [];
    internal IReadOnlyList<ShopTradeState> Trades => shopTrades;

    internal void RestoreTrades(IEnumerable<ShopTradeState> trades)
    {
        var restored = trades.ToArray();
        shopTrades.Clear();
        shopTrades.AddRange(restored);
    }

    internal void ProposeShopTrade(ShopTradeState trade)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalShopTrades(shopTrades.Append(trade));
        shopTrades.Add(trade);
    }

    internal void RecordShopTrade(ShopTradeState trade)
    {
        var index = shopTrades.FindIndex(item => item.OfferId == trade.OfferId);
        if (SystemInvariantChecks.Enabled) ValidateLocalShopTrades(shopTrades.Select((item, at) => at == index ? trade : item));
        shopTrades[index] = trade;
    }

    internal void RetireShopTrades(IReadOnlySet<string> retired) => shopTrades.RemoveAll(trade => retired.Contains(trade.OfferId));

    internal static void ValidateLocalShopTrades(IEnumerable<ShopTradeState> trades)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var trade in trades)
            if (!ids.Add(trade.OfferId) || string.IsNullOrWhiteSpace(trade.OfferId) ||
                string.IsNullOrWhiteSpace(trade.BuildingInstanceId) || string.IsNullOrWhiteSpace(trade.SellerHouseholdId) ||
                string.IsNullOrWhiteSpace(trade.BuyerId) || string.IsNullOrWhiteSpace(trade.GoodsKind) ||
                string.IsNullOrWhiteSpace(trade.PaymentKind) || trade.ProposedTick < 0)
                throw new InvalidDataException("Market shop trades have invalid local state.");
    }

    internal static void ValidateLocalMarket(TownMarketState market)
    {
        if (string.IsNullOrWhiteSpace(market.Id) || string.IsNullOrWhiteSpace(market.ProjectId) ||
            string.IsNullOrWhiteSpace(market.HallBuildingId) || market.Stalls.Select(item => item.BuildingId).Distinct(StringComparer.Ordinal).Count() != market.Stalls.Count ||
            market.Occupancies.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != market.Occupancies.Count ||
            market.Trades.Select(item => item.OfferId).Distinct(StringComparer.Ordinal).Count() != market.Trades.Count ||
            market.StockReceipts.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != market.StockReceipts.Count || market.NextStockReceiptSequence < 0)
            throw new InvalidDataException("Market records have invalid local identities or sequence.");
    }
}
