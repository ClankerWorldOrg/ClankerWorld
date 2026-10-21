namespace ClankerWorld.Simulation.World;

internal interface ICognitionPersonalIdentityWorld
{
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.DeceasedAgentState> DeceasedAgents { get; }
    global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    void MarkCheckpointCurrent();
}
