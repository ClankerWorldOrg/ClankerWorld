using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void RefreshTownLandHearings() => townSystem.RefreshTownLandHearings(civicWorld);
    private static void ValidateLandHearings(WorldMap map, SocietyCheckpoint checkpoint,
        IReadOnlyList<TownRuntimeState> savedTowns, IReadOnlyList<HouseholdLandUseRight> rights,
        IReadOnlyList<HouseholdLandUseRequest> requests, IReadOnlyList<TownLandTitleRecord> titles, int day) => TownSystem.ValidateLandHearings(map, checkpoint, savedTowns, rights, requests, titles, day);
}
