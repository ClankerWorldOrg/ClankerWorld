namespace ClankerWorld.Simulation.World;

internal interface IOrderIngressWorld
{
    OrderStore Orders { get; }
    string InstructionRequestDigest(global::ClankerWorld.Simulation.World.OwnerInstructionRequest request);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    global::ClankerWorld.Simulation.World.OrderTask? ParseOrderTask(string text, string actor);
    void ReplacePendingOrder(string agentId);
    string InstructionIdFor(long sequence);
    long WorldTick { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    string ToWireValue(global::ClankerWorld.Simulation.World.OwnerInstructionKind kind);
    void CloseOrdersNotUnderstood(string agentId);
    void PruneClosedOrderHistory();
    bool Matches(global::ClankerWorld.Simulation.World.OwnerOrderCancellation existing, global::ClankerWorld.Simulation.World.OwnerOrderCancelRequest request);
    bool IsActiveOrder(string status);
    bool IsInstructionComplete(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void CancelActiveOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string cause, string? reason);
    long NextEventId { get; }
    void MarkCheckpointCurrent();
}
