using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    /// <summary>Only the chosen physical trip continues locally; arrival needs a fresh personal choice.</summary>
    private void ContinueTownLandSaleWalk(string actor, string candidate) => townSystem.ContinueTownLandSaleWalk(civicWorld, actor, candidate);
}
