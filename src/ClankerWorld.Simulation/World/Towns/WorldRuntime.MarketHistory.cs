using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void RetireMarketHistory() =>
        MarketSystem.RetireMarketHistory(MarketAdministrationWorld);
}

/// <summary>Retains current trading authority and a small recent history; inventory and events keep physical outcomes.</summary>
public static class MarketHistory
{
    public const int RecentRecordLimit = 32;

    public sealed record Retention(TownMarketState Market, IReadOnlySet<string> RetiredOfferIds);

    public static Retention Retain(TownMarketState market, InventoryCheckpoint inventory) =>
        MarketSystem.Retain(market, inventory);
}
