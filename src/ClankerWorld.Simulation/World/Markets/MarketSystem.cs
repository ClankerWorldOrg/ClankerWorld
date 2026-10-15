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
                !Bounded(trade.BuildingInstanceId, 256) || string.IsNullOrWhiteSpace(trade.SellerHouseholdId) ||
                string.IsNullOrWhiteSpace(trade.BuyerId) || !ResourceKind(trade.GoodsKind) ||
                !ResourceKind(trade.PaymentKind) || trade.ProposedTick < 0 ||
                trade.CancellationReason is { } reason && !Bounded(reason, 160))
                throw new InvalidDataException("Market shop trades have invalid local state.");
    }

}
