namespace ClankerWorld.Simulation.World;

internal interface ICognitionModelOutcomesWorld
{
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    long WorldTick { get; }
    global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    void RecordModelAttempt(string id, string status, string? acceptedCandidateId = null, string? setupBlocker = null);
    void MarkCheckpointCurrent();
}
