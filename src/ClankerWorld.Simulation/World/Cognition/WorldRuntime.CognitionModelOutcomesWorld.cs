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
    private ICognitionModelOutcomesWorld CognitionModelOutcomesWorld => new CognitionModelOutcomesAdapter(this);

    private sealed class CognitionModelOutcomesAdapter(WorldRuntime runtime) : ICognitionModelOutcomesWorld
    {
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public long WorldTick => runtime.WorldTick;
        public AgentStore PhysicalAgents => runtime.agentStore;
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public void RecordModelAttempt(string id, string status, string? acceptedCandidateId = null, string? setupBlocker = null) => runtime.RecordModelAttempt(id, status, acceptedCandidateId, setupBlocker);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
