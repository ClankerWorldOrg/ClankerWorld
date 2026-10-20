namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private bool MayFollowOwnerOrder(string actor, OrderTask order) =>
        ActorCapabilities.Check(CapabilitiesFor(actor), order.Action, OwnerOrderCapabilities.For(order.Action)).Allowed;
}
