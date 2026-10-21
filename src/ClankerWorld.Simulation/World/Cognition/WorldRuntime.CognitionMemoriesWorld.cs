namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionMemoriesWorld CognitionMemoriesWorld => new CognitionMemoriesAdapter(this);

    private sealed class CognitionMemoriesAdapter(WorldRuntime runtime) : ICognitionMemoriesWorld
    {
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public long WorldTick => runtime.WorldTick;
        public CognitionSystem Host => runtime.cognitionSystem;
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
        public void RecordMemorySummaryState(global::ClankerWorld.Simulation.Society.SocietyCheckpoint next) => runtime.society.Apply(_ => new global::ClankerWorld.Simulation.Society.SocietyOperationResult(next));
        public void RecordMemoryCompactionState(global::ClankerWorld.Simulation.Society.SocietyCheckpoint next) => runtime.society.Apply(_ => new global::ClankerWorld.Simulation.Society.SocietyOperationResult(next));
    }
}
