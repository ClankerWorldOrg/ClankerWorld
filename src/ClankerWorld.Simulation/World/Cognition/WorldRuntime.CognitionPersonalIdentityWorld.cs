using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionPersonalIdentityWorld CognitionPersonalIdentityWorld => new CognitionPersonalIdentityAdapter(this);

    private sealed class CognitionPersonalIdentityAdapter(WorldRuntime runtime) : ICognitionPersonalIdentityWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public IReadOnlyDictionary<string, DeceasedAgentState> Deceased => runtime.deceasedAgents;
        public AgentStore PhysicalAgents => runtime.agentStore;
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
