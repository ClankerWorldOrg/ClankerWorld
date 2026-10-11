using System.Collections.ObjectModel;

namespace ClankerWorld.Simulation.Playtest;

internal sealed record TownCivicState(TownGovernanceState? Governance, TownGovernmentState? Government,
    TownNonviolentState Nonviolent)
{
    internal static TownCivicState From(TownRuntimeState town) => new(town.Governance, town.Government, town.Nonviolent);

    internal bool Matches(TownRuntimeState town) => ReferenceEquals(Governance, town.Governance) &&
        ReferenceEquals(Government, town.Government) && ReferenceEquals(Nonviolent, town.Nonviolent);

    internal TownRuntimeState ApplyTo(TownRuntimeState town) => Matches(town) ? town : town with
    {
        Governance = Governance,
        Government = Government,
        Nonviolent = Nonviolent,
    };
}

internal sealed record CivicState(SettlementCouncil? Council, ReadOnlyDictionary<string, TownCivicState> Towns)
{
    internal static CivicState Empty { get; } = new(null, new(new Dictionary<string, TownCivicState>(StringComparer.Ordinal)));
}
