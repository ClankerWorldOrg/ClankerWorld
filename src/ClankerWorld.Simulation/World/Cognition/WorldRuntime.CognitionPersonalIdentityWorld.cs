namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionPersonalIdentityWorld CognitionPersonalIdentityWorld => new CognitionPersonalIdentityAdapter(this);

    private sealed class CognitionPersonalIdentityAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionPersonalIdentityWorld
    {
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.DeceasedAgentState> DeceasedAgents => Runtime.deceasedAgents;
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
    }
}
