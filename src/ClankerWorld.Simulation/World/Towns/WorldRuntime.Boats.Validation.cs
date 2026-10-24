namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private static void ValidateBoatTransport(WorldRuntimeState state)
    {
        TransportSystem.ValidateBoatTransport(state);
        ValidateBoatOrderBindings(state);
    }
}
