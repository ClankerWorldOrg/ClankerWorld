using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IOrderCapabilitiesWorld
{
    ClankerWorld.Simulation.World.ActorCapabilityState? CapabilitiesFor(string actor);
}
