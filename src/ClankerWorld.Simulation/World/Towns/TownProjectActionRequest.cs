namespace ClankerWorld.Simulation.World;

public enum TownProjectActionFamily { Load, Work, Recovery, Donation }

public sealed record TownProjectActionRequest(TownProjectActionFamily Family, string CandidateId)
{
    public string Encode() => CandidateId;
    public static TownProjectActionRequest? Decode(string candidate)
    {
        foreach (var (prefix, family) in new[]
        {
            ("town_project_supply:", TownProjectActionFamily.Load), ("town_project_deliver:", TownProjectActionFamily.Load),
            ("town_project_work:", TownProjectActionFamily.Work), ("town_project_gather:", TownProjectActionFamily.Work),
            ("town_project_recover:", TownProjectActionFamily.Recovery), ("town_project_return:", TownProjectActionFamily.Recovery),
            ("town_project_donate:", TownProjectActionFamily.Donation),
        })
            if (candidate.StartsWith(prefix, StringComparison.Ordinal)) return new(family, candidate);
        return null;
    }
}
