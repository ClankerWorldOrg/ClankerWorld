namespace ClankerWorld.Simulation.World;

public enum MarketActionFamily { Occupancy, Stock, Exchange }

public sealed record MarketActionRequest(MarketActionFamily Family, string CandidateId)
{
    public string Encode() => CandidateId;
    public static MarketActionRequest? Decode(string candidate)
    {
        foreach (var (prefix, family) in new[]
        {
            ("market_borrow:", MarketActionFamily.Occupancy), ("market_leave:", MarketActionFamily.Occupancy),
            ("market_load:", MarketActionFamily.Stock), ("market_deposit:", MarketActionFamily.Stock), ("market_collect:", MarketActionFamily.Stock),
            ("market_buy:", MarketActionFamily.Exchange), ("market_continue:", MarketActionFamily.Exchange), ("market_cancel:", MarketActionFamily.Exchange),
        })
            if (candidate.StartsWith(prefix, StringComparison.Ordinal)) return new(family, candidate);
        return null;
    }
}
