namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderIngressWorld OrderIngressWorld => new OrderIngressAdapter(this);

    private sealed class OrderIngressAdapter(WorldRuntime runtime) : IOrderIngressWorld
    {
        public OrderStore Orders => runtime.orderStore;
        public string InstructionRequestDigest(global::ClankerWorld.Simulation.World.OwnerInstructionRequest request) => WorldRuntime.InstructionRequestDigest(request);
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseOrderTask(string text, string actor) => runtime.ParseOrderTask(text, actor);
        public void ReplacePendingOrder(string agentId) => runtime.ReplacePendingOrder(agentId);
        public string InstructionIdFor(long sequence) => WorldRuntime.InstructionIdFor(sequence);
        public long WorldTick => runtime.WorldTick;
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public string ToWireValue(global::ClankerWorld.Simulation.World.OwnerInstructionKind kind) => WorldRuntime.ToWireValue(kind);
        public void CloseOrdersNotUnderstood(string agentId) => runtime.CloseOrdersNotUnderstood(agentId);
        public void PruneClosedOrderHistory() => runtime.PruneClosedOrderHistory();
        public bool Matches(global::ClankerWorld.Simulation.World.OwnerOrderCancellation existing, global::ClankerWorld.Simulation.World.OwnerOrderCancelRequest request) => WorldRuntime.Matches(existing, request);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public bool IsInstructionComplete(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.IsInstructionComplete(instruction);
        public void CancelActiveOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string cause, string? reason) => runtime.CancelActiveOrder(instruction, cause, reason);
        public long NextEventId => runtime.nextEventId;
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
