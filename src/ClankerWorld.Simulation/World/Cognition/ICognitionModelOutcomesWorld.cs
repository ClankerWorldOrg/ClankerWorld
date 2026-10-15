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

internal interface ICognitionModelOutcomesWorld
{
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    long WorldTick { get; }
    AgentStore PhysicalAgents { get; }
    void AppendEvent(string kind, string detail, GridPoint? eventPosition = null);
    void RecordModelAttempt(string id, string status, string? acceptedCandidateId = null, string? setupBlocker = null);
    void MarkCheckpointCurrent();
}
