using System.Text.Json.Nodes;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ShopChoice = ClankerWorld.Tests.ShopTradeTests.ShopProvider;

namespace ClankerWorld.Tests;

public sealed class ShopTradeCheckpointTests
{
    [Theory]
    [InlineData("none")]
    [InlineData("seller-only")]
    [InlineData("both")]
    [InlineData("unrelated")]
    [InlineData("duplicate-buyer")]
    public async Task DecodingAnOpenShopExchangeRequiresTheCustomersActualAcceptance(string damage)
    {
        var (state, _) = await PendingExchangeAsync();
        var healthyBytes = WorldRuntimeCodec.Encode(state);
        var trade = Assert.Single(state.ShopTrades!);
        var offer = state.Society.Society.Inventory.GetOffer(trade.OfferId);
        var document = JsonNode.Parse(healthyBytes)!;
        var savedOffer = document["state"]!["society"]!["society"]!["inventory"]!["offers"]!.AsArray()
            .Single(item => item!["id"]!.GetValue<string>() == offer.Id)!;
        savedOffer["acceptedBy"] = new JsonArray(DamagedAcceptance(damage, offer)
            .Select(party => (JsonNode?)JsonValue.Create(party)).ToArray());
        var damagedBytes = System.Text.Encoding.UTF8.GetBytes(document.ToJsonString());

        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Decode(damagedBytes));

        AssertHealthyRoundtrip(state, healthyBytes);
    }

    private static string[] DamagedAcceptance(string damage, DirectBarterOffer offer) => damage switch
    {
        "none" => [],
        "seller-only" => [offer.FirstPartyId],
        "both" => new[] { offer.FirstPartyId, offer.SecondPartyId }.Order(StringComparer.Ordinal).ToArray(),
        "unrelated" => ["unrelated-shop-customer"],
        "duplicate-buyer" => [offer.SecondPartyId, offer.SecondPartyId],
        _ => throw new ArgumentException("Unknown checkpoint damage.", nameof(damage)),
    };

    private static void AssertHealthyRoundtrip(WorldRuntimeState state, byte[] healthyBytes)
    {
        Assert.Equal(healthyBytes, WorldRuntimeCodec.Encode(state));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(healthyBytes),
            _ => new ShopChoice("safe_idle"));
        Assert.Equal(state.ShopTrades, restored.ShopTrades);
        Assert.Equal(healthyBytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    private static async Task<(WorldRuntimeState State, string Seller)> PendingExchangeAsync()
    {
        var (state, buyer, seller, _) = ShopTradeTests.CreateShopState();
        using var offering = WorldRuntime.Restore(state, actor => actor == buyer
            ? new ShopChoice("shop_buy:") : new ShopChoice("safe_idle"));
        for (var tick = 0; tick < 40 && offering.ShopTrades.Count == 0; tick++)
            Assert.True((await offering.AdvanceOneTickAsync()).Advanced);
        var trade = Assert.Single(offering.ShopTrades);
        Assert.Equal(DirectBarterState.Open, offering.Society.Inventory.GetOffer(trade.OfferId).State);
        return (offering.ExportState(), seller);
    }

}
