using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    /// <summary>Accept or replace the starter footprint before any founder is placed.</summary>
    public FirstTownLayout AcceptFirstTownLayout(GridPoint roughSite)
    {
        gate.Wait();
        try { return civicSystem.AcceptFirstTownLayout(civicWorld, roughSite); }
        finally { gate.Release(); }
    }

    private static InventoryCheckpoint PrepareFirstTownStock(InventoryCheckpoint source) => CivicSystem.PrepareFirstTownStock(source);
}
