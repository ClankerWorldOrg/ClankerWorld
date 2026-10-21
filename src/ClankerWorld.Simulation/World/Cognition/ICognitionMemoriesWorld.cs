namespace ClankerWorld.Simulation.World;

internal interface ICognitionMemoriesWorld
{
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    long WorldTick { get; }
    CognitionSystem Host { get; }
    void MarkCheckpointCurrent();
    void RecordMemorySummaryState(global::ClankerWorld.Simulation.Society.SocietyCheckpoint next);
    void RecordMemoryCompactionState(global::ClankerWorld.Simulation.Society.SocietyCheckpoint next);
}
