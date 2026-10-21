using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private bool HasOpenLandHearingPlot(string townId, IEnumerable<GridPoint> tiles) => townSystem.HasOpenLandHearingPlot(civicWorld, townId, tiles);
}
