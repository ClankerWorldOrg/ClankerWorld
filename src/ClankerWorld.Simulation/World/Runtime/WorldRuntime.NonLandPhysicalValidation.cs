using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static void ValidateNonLandPhysicalState(WorldMap map, SocietyCheckpoint society, IReadOnlyList<TownRuntimeState> towns, IReadOnlyList<TownLandTitleRecord> titles, IReadOnlyList<HouseholdLandUseRight> rights) => TownSystem.ValidateNonLandPhysicalState(map, society, towns, titles, rights);
}
