using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    /// <summary>Accept or replace the starter footprint before any founder is placed.</summary>
    public FirstTownLayout AcceptFirstTownLayout(GridPoint roughSite)
    {
        gate.Wait();
        try { return townSystem.AcceptFirstTownLayout(civicWorld, roughSite); }
        finally { gate.Release(); }
    }

    private static InventoryCheckpoint PrepareFirstTownStock(InventoryCheckpoint source) => TownSystem.PrepareFirstTownStock(source);
}
