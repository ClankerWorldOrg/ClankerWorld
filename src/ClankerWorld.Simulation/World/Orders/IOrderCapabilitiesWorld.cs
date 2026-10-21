namespace ClankerWorld.Simulation.World;

internal interface IOrderCapabilitiesWorld
{
    global::ClankerWorld.Simulation.World.ActorCapabilityState? CapabilitiesFor(string actor);
}
