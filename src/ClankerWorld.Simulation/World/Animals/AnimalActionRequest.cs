namespace ClankerWorld.Simulation.World;

public enum AnimalActionFamily { Work, Product, Travel, Supply, Trip, Agreement }

public sealed record AnimalActionRequest(AnimalActionFamily Family, string CandidateId)
{
    public string Encode() => CandidateId;

    public static AnimalActionRequest? Decode(string candidate)
    {
        if (candidate == "drink_milk" || StartsWithAny(candidate, "animal:collect:", "animal:collect_hide:"))
            return new(AnimalActionFamily.Product, candidate);
        if (StartsWithAny(candidate, "animal:care:", "animal:tame:", "animal:saddle:"))
            return new(AnimalActionFamily.Work, candidate);
        if (StartsWithAny(candidate, "animal:mount:", "animal:dismount:", "animal:lead_home:"))
            return new(AnimalActionFamily.Travel, candidate);
        if (candidate.StartsWith("animal:supply:", StringComparison.Ordinal)) return new(AnimalActionFamily.Supply, candidate);
        if (candidate.StartsWith("animal:supply_trip:", StringComparison.Ordinal)) return new(AnimalActionFamily.Trip, candidate);
        if (candidate.StartsWith("animal:agreement:", StringComparison.Ordinal)) return new(AnimalActionFamily.Agreement, candidate);
        return null;
    }

    private static bool StartsWithAny(string value, params string[] prefixes) =>
        prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal));
}
