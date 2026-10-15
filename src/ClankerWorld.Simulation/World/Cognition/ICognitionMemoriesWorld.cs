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

internal interface ICognitionMemoriesWorld
{
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    CognitionSystem Host { get; }
    void AppendEvent(string kind, string detail, GridPoint? eventPosition = null);
    void RecordMemorySummaryState(SocietyCheckpoint next);
    void MarkCheckpointCurrent();
    void RecordMemoryCompactionState(SocietyCheckpoint next);
}
