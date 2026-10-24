namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionMemoriesWorld CognitionMemoriesWorld => new CognitionMemoriesAdapter(this);

    private sealed class CognitionMemoriesAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionMemoriesWorld
    {
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
        public void RecordMemorySummaryState(global::ClankerWorld.Simulation.Society.SocietyCheckpoint next) => Runtime.society.Apply(_ => new global::ClankerWorld.Simulation.Society.SocietyOperationResult(next));
        public void RecordMemoryCompactionState(global::ClankerWorld.Simulation.Society.SocietyCheckpoint next) => Runtime.society.Apply(_ => new global::ClankerWorld.Simulation.Society.SocietyOperationResult(next));
    }
}
