using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static void ValidateNonLandPhysicalState(WorldMap map, SocietyCheckpoint society, IReadOnlyList<TownRuntimeState> towns, IReadOnlyList<TownLandTitleRecord> titles, IReadOnlyList<HouseholdLandUseRight> rights) => TownSystem.ValidateNonLandPhysicalState(map, society, towns, titles, rights);
    private static void ValidatePhysicalAllegation(TownViolationAllegation allegation, Dictionary<string, TownConductRecord> records) => TownSystem.ValidatePhysicalAllegation(allegation, records);
    private static void ValidatePhysicalAcquisitions(string conductId, string actor, long tick, IReadOnlyList<string> evidenceIds, Dictionary<string, TownConductAcquisition> acquisitions) => TownSystem.ValidatePhysicalAcquisitions(conductId, actor, tick, evidenceIds, acquisitions);
}
