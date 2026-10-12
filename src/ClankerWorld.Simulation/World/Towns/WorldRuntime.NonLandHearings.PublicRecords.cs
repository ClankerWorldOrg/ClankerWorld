namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    /// <summary>Only exact public records this actor actually learned, relevant to the alleged plot.</summary>
    private static TownCaseEvidence[] NonLandKnownPublicRecords(TownRuntimeState town, IReadOnlyList<HouseholdLandUseRight> currentRights, IReadOnlyList<TownLandTitleRecord> titles, string actor, GridPoint position, int revision, long tick) => TownSystem.NonLandKnownPublicRecords(town, currentRights, titles, actor, position, revision, tick);
}
