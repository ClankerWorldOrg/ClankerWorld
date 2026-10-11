using System.Collections.ObjectModel;

namespace ClankerWorld.Simulation.Playtest;

internal sealed record CivicState(SettlementCouncil? Council, ReadOnlyCollection<TownRuntimeState> Towns)
{
    internal FounderSetupState? FounderSetup { get; init; }
    internal ReadOnlyCollection<TownLandTitleRecord> TownLandTitles { get; init; } = Array.AsReadOnly(Array.Empty<TownLandTitleRecord>());
    internal ReadOnlyCollection<HouseholdLandUseRight> HouseholdLandUseRights { get; init; } = Array.AsReadOnly(Array.Empty<HouseholdLandUseRight>());
    internal ReadOnlyCollection<HouseholdLandUseRequest> HouseholdLandUseRequests { get; init; } = Array.AsReadOnly(Array.Empty<HouseholdLandUseRequest>());
    internal static CivicState Empty { get; } = new(null, Array.AsReadOnly(Array.Empty<TownRuntimeState>()));
}
