using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class MarketInvariantTests
{
    private static TownMarketState Market => new(MarketContent.MarketId("project"), "project", "hall", new(5, 5),
        [new("stall-first", 0, "project-first", 0), new("stall-second", 4, "project-second", 0)], [], []);

    [Theory]
    [InlineData("slot")]
    [InlineData("sequence")]
    [InlineData("occupancy")]
    public void InvalidMarketReplacementFailsBeforeItsTownStateCanPublish(string field)
    {
        var system = new TownSystem(CivicState.Empty);
        var before = system.State;
        var market = Market;
        var invalid = field switch
        {
            "slot" => market with { Stalls = [market.Stalls[0], market.Stalls[1] with { SlotIndex = 0 }] },
            "sequence" => market with { NextStockReceiptSequence = 1 },
            "occupancy" => market with { Occupancies = [new("visit", "stall-first", "seller", 0, ReleaseReason: "released") { SellerHouseholdId = "home" }] },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        Assert.Throws<InvalidDataException>(() => system.ReplaceMarketState(null!, "missing-town", invalid));
        Assert.Same(before, system.State);
    }

    [Fact]
    public void ALocalMarketCheckDoesNotRequireItsExternalBuildingsOrPeople()
    {
        var market = Market with
        {
            Occupancies = [new("visit", "stall-first", "seller", 0) { SellerHouseholdId = "home" }],
        };

        MarketSystem.ValidateLocalMarket(market);

        Assert.Equal("seller", Assert.Single(market.Occupancies).SellerAgentId);
    }
}
