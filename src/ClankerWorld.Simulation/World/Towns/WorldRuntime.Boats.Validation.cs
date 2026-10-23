namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static bool IsSavedBoatPassenger(BoatTransportState transport, string actor, GridPoint position) =>
        TransportSystem.IsSavedBoatPassenger(transport, actor, position);

    private static void ValidateBoatTransport(WorldRuntimeState state)
    {
        TransportSystem.ValidateBoatTransport(state);
        ValidateBoatOrderBindings(state);
    }
}
