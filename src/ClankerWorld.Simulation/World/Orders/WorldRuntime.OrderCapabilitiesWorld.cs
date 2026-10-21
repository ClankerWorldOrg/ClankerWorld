namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderCapabilitiesWorld OrderCapabilitiesWorld => new OrderCapabilitiesAdapter(this);

    private sealed class OrderCapabilitiesAdapter(WorldRuntime runtime) : IOrderCapabilitiesWorld
    {
        public global::ClankerWorld.Simulation.World.ActorCapabilityState? CapabilitiesFor(string actor) => runtime.CapabilitiesFor(actor);
    }
}
