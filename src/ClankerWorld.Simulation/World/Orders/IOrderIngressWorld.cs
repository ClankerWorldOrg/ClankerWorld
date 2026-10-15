using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IOrderIngressWorld
{
    SocietyCheckpoint Society { get; }
    OrderStore Orders { get; }
    long WorldTick { get; }
    long NextEventId { get; }
    bool Matches(OwnerQueuedInstruction existing, OwnerInstructionRequest request);
    bool Matches(OwnerOrderCancellation existing, OwnerOrderCancelRequest request);
    OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    OrderTask? ParseOrderTask(string text, string actor);
    void ReplacePendingOrders(string actor);
    void AppendEvent(string kind, string detail);
    string ToWireValue(OwnerInstructionKind kind);
    void CloseOrdersNotUnderstood(string actor);
    bool IsActiveOrder(string status);
    void CancelActiveOrder(OwnerQueuedInstruction instruction, string cause, string? reason);
    void MarkCheckpointCurrent();
}
