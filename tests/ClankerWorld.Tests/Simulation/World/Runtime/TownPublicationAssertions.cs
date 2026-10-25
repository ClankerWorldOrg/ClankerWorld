using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

internal static class TownPublicationAssertions
{
    internal static void Reject(Action<TownSystem> mutation)
    {
        var system = new TownSystem(CivicState.Empty);
        var before = system.State;

        Assert.Throws<InvalidDataException>(() => mutation(system));
        Assert.Same(before, system.State);
    }
}
