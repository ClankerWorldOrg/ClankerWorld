using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderCapabilitiesWorld OrderCapabilitiesWorld => new OrderCapabilitiesAdapter(this);

    private sealed class OrderCapabilitiesAdapter(WorldRuntime runtime) : IOrderCapabilitiesWorld
    {
        public ClankerWorld.Simulation.World.ActorCapabilityState? CapabilitiesFor(string actor) => runtime.CapabilitiesFor(actor);
    }
}
