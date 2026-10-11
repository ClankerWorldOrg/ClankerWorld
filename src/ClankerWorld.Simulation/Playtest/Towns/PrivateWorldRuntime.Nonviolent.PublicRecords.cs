using ClankerWorld.Simulation.Harness;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    /// <summary>Only exact public records this actor actually learned, relevant to the alleged plot.</summary>
    private static TownCaseEvidence[] NonviolentKnownPublicRecords(TownRuntimeState town, IReadOnlyList<HouseholdLandUseRight> currentRights, IReadOnlyList<TownLandTitleRecord> titles, string actor, GridPoint position, int revision, long tick) => CivicSystem.NonviolentKnownPublicRecords(town, currentRights, titles, actor, position, revision, tick);
}
