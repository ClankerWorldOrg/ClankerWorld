using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderIngressWorld OrderIngressWorld => new OrderIngressAdapter(this);

    private sealed class OrderIngressAdapter(WorldRuntime runtime) : IOrderIngressWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public OrderStore Orders => runtime.orderStore;
        public long WorldTick => runtime.WorldTick;
        public long NextEventId => runtime.nextEventId;
        public bool Matches(OwnerQueuedInstruction existing, OwnerInstructionRequest request) => WorldRuntime.Matches(existing, request);
        public bool Matches(OwnerOrderCancellation existing, OwnerOrderCancelRequest request) => WorldRuntime.Matches(existing, request);
        public OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public OrderTask? ParseOrderTask(string text, string actor) => runtime.ParseOrderTask(text, actor);
        public void ReplacePendingOrders(string actor) => runtime.ReplacePendingOrders(actor);
        public void AppendEvent(string kind, string detail) => runtime.AppendEvent(kind, detail);
        public string ToWireValue(OwnerInstructionKind kind) => WorldRuntime.ToWireValue(kind);
        public void CloseOrdersNotUnderstood(string actor) => runtime.CloseOrdersNotUnderstood(actor);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public void CancelActiveOrder(OwnerQueuedInstruction instruction, string cause, string? reason) => runtime.CancelActiveOrder(instruction, cause, reason);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
