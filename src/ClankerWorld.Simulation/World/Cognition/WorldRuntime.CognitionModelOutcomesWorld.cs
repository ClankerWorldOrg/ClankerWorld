namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionModelOutcomesWorld CognitionModelOutcomesWorld => new CognitionModelOutcomesAdapter(this);

    private sealed class CognitionModelOutcomesAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionModelOutcomesWorld
    {
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
    }
}
