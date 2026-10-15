namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private bool MayFollowOwnerOrder(string actor, OrderTask order) =>
        OrderSystem.MayFollowOwnerOrder(OrderCapabilitiesWorld, actor, order);
}
