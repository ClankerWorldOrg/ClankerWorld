using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static bool MayFollowOwnerOrder(IOrderCapabilitiesWorld world, string actor, OrderTask order) =>
        ActorCapabilities.Check(world.CapabilitiesFor(actor), order.Action, OwnerOrderCapabilities.For(order.Action)).Allowed;
}
