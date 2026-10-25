namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static string LandHearingEvidenceId(TownLandCase item, string actor, string kind, string source) => TownSystem.LandHearingEvidenceId(item, actor, kind, source);
}
