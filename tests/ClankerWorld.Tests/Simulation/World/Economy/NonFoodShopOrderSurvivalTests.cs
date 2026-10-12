using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class ShopTradeTests
{
    // Normal shopping tests do not prove that hunger keeps a tool quote out
    // of an order's survival choices. Exercise both a new and an open quote.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UrgentFoodDoesNotExposeNonFoodShopChoicesDuringAnOrder(bool openQuote)
    {
        var (state, buyer, _, shopId) = CreateShopState();
        if (openQuote)
        {
            using var offering = WorldRuntime.Restore(state, id => id == buyer
                ? new ShopProvider("shop_buy:") : new ShopProvider("safe_idle"));
            for (var tick = 0; tick < 12 && offering.ShopTrades.Count == 0; tick++)
                Assert.True((await offering.AdvanceOneTickAsync()).Advanced);
            Assert.Single(offering.ShopTrades);
            state = offering.ExportState();
        }
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == buyer
                ? person with { FullnessBasisPoints = 1_500, LastDecisionContext = null } : person).ToArray(),
        };
        var candidate = openQuote ? "shop_continue:" + Assert.Single(state.ShopTrades!).OfferId : "shop_buy:" + shopId;
        var ordinary = new ShopProvider(candidate);
        using (var control = WorldRuntime.Restore(state, id => id == buyer ? ordinary : new ShopProvider("safe_idle")))
        {
            Assert.True((await control.AdvanceOneTickAsync()).Advanced);
            Assert.Contains(candidate, ordinary.Seen);
        }
        var ordered = new ShopProvider(candidate);
        using var world = WorldRuntime.Restore(state, id => id == buyer ? ordered : new ShopProvider("safe_idle"));
        var receipt = world.SubmitInstruction(new("nonfood-survival-order", "owner:test", buyer,
            OwnerInstructionKind.Order, "keep gathering wood"));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.DoesNotContain(candidate, ordered.Seen);
        Assert.Equal(openQuote ? 1 : 0, world.ShopTrades.Count);
        Assert.DoesNotContain(receipt.InstructionId, world.ExportState().CompletedInstructionIds!);
        world.Validate();
        _ = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()));
    }
}
