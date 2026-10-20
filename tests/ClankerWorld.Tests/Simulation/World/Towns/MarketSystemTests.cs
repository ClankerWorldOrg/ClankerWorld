using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation;

namespace ClankerWorld.Tests;

public sealed class MarketSystemTests
{
    private static ShopTradeState Trade => new(Identity.Parse("offer-first"), "shop-first", "household-first", "buyer", new(1, 1), 0, "bread", "wood");

    [Fact]
    public void RestoredShopLedgerKeepsTheOriginalRecordsUntilTheSellerResponds()
    {
        var source = new MarketSystem();
        source.ProposeShopTrade(Trade);
        var before = Assert.Single(source.Trades);
        var restored = new MarketSystem();
        restored.RestoreTrades(source.Trades);
        Assert.Same(before, Assert.Single(restored.Trades));

        restored.RecordShopTrade(before with { SellerActorId = "seller" });

        Assert.Null(Assert.Single(source.Trades).SellerActorId);
        Assert.Equal("seller", Assert.Single(restored.Trades).SellerActorId);
    }

    [Fact]
    public void RestoringTheSameLedgerDoesNotDiscardItsTrades()
    {
        var market = new MarketSystem();
        market.ProposeShopTrade(Trade);
        var before = Assert.Single(market.Trades);

        market.RestoreTrades(market.Trades);

        Assert.Same(before, Assert.Single(market.Trades));
    }

    [Fact]
    public void DuplicateShopBindingsFailBeforeTheLedgerPublishesThem()
    {
        var market = new MarketSystem();
        market.ProposeShopTrade(Trade);
        var before = Assert.Single(market.Trades);

        var failure = Assert.Throws<InvalidDataException>(() => market.ProposeShopTrade(Trade));

        Assert.Contains("Market shop trades", failure.Message, StringComparison.Ordinal);
        Assert.Same(before, Assert.Single(market.Trades));
    }
}
