namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionModelOutcomesWorld CognitionModelOutcomesWorld => new CognitionModelOutcomesAdapter(this);

    private sealed class CognitionModelOutcomesAdapter(WorldRuntime runtime) : ICognitionModelOutcomesWorld
    {
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public long WorldTick => runtime.WorldTick;
        public global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents => runtime.agentStore;
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public void RecordModelAttempt(string id, string status, string? acceptedCandidateId = null, string? setupBlocker = null) => runtime.RecordModelAttempt(id, status, acceptedCandidateId, setupBlocker);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
