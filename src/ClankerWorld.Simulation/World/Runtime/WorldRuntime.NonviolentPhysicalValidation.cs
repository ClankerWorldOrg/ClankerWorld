using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static void ValidateNonviolentPhysicalState(WorldMap map, SocietyCheckpoint society, IReadOnlyList<TownRuntimeState> towns, IReadOnlyList<TownLandTitleRecord> titles, IReadOnlyList<HouseholdLandUseRight> rights) => CivicSystem.ValidateNonviolentPhysicalState(map, society, towns, titles, rights);
    private static void ValidatePhysicalAllegation(TownViolationAllegation allegation, Dictionary<string, TownConductRecord> records) => CivicSystem.ValidatePhysicalAllegation(allegation, records);
    private static void ValidatePhysicalAcquisitions(string conductId, string actor, long tick, IReadOnlyList<string> evidenceIds, Dictionary<string, TownConductAcquisition> acquisitions) => CivicSystem.ValidatePhysicalAcquisitions(conductId, actor, tick, evidenceIds, acquisitions);
}
