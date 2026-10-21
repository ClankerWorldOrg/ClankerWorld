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
                !BoundedText.IsValid(trade.BuildingInstanceId, 256, requireTrimmed: true) || string.IsNullOrWhiteSpace(trade.SellerHouseholdId) ||
                string.IsNullOrWhiteSpace(trade.BuyerId) || !IdentityRules.Reference(trade.GoodsKind, int.MaxValue, requireTrimmed: true) ||
                !IdentityRules.Reference(trade.PaymentKind, int.MaxValue, requireTrimmed: true) || trade.ProposedTick < 0 ||
                trade.CancellationReason is { } reason && !BoundedText.IsValid(reason, 160, requireTrimmed: true))
                throw new InvalidDataException("Market shop trades have invalid local state.");
    }

}
