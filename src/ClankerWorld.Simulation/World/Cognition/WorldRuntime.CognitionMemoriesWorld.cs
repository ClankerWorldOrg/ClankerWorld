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
    private ICognitionMemoriesWorld CognitionMemoriesWorld => new CognitionMemoriesAdapter(this);

    private sealed class CognitionMemoriesAdapter(WorldRuntime runtime) : ICognitionMemoriesWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public long WorldTick => runtime.WorldTick;
        public CognitionSystem Host => runtime.cognitionSystem;
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public void RecordMemorySummaryState(SocietyCheckpoint next) => runtime.society.Apply(_ => new SocietyOperationResult(next));
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
        public void RecordMemoryCompactionState(SocietyCheckpoint next) => runtime.society.Apply(_ => new SocietyOperationResult(next));
    }
}
