namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionPersonalIdentityWorld CognitionPersonalIdentityWorld => new CognitionPersonalIdentityAdapter(this);

    private sealed class CognitionPersonalIdentityAdapter(WorldRuntime runtime) : ICognitionPersonalIdentityWorld
    {
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.DeceasedAgentState> DeceasedAgents => runtime.deceasedAgents;
        public global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents => runtime.agentStore;
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
