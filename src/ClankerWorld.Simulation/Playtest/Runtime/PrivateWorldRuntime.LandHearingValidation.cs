using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private void RefreshTownLandHearings() => civicSystem.RefreshTownLandHearings(civicWorld);
    private static void ValidateLandHearings(SeededMap map, SocietyCheckpoint checkpoint,
        IReadOnlyList<TownRuntimeState> savedTowns, IReadOnlyList<HouseholdLandUseRight> rights,
        IReadOnlyList<HouseholdLandUseRequest> requests, IReadOnlyList<TownLandTitleRecord> titles, int day) => CivicSystem.ValidateLandHearings(map, checkpoint, savedTowns, rights, requests, titles, day);
}
