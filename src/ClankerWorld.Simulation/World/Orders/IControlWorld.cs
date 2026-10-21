namespace ClankerWorld.Simulation.World;

internal interface IControlWorld
{
    void CancelRepairForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void CancelFieldWorkForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void CancelProductionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void CancelConstructionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void CancelExpansionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void CancelAnimalSupplyForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void CancelBoatTravelForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void CancelKnowledgeWritingForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction[] ObserverGuidanceInstructionsFor(string agentId);
    OrderStore Orders { get; }
    bool IsInstructionComplete(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    string ToWireValue(global::ClankerWorld.Simulation.World.OwnerInstructionKind kind);
    string? UnderstoodTaskFor(string? candidate);
    global::ClankerWorld.Simulation.World.OrderTask? ParseMarriageOrder(string text, string actor);
    global::ClankerWorld.Simulation.World.OrderTask? ParseKnowledgeReadOrder(string text);
    global::ClankerWorld.Simulation.World.OrderTask? ParseTalkOrder(string text, string actor);
    global::ClankerWorld.Simulation.World.OrderTask? ParseCartCargoOrder(string text, string actor);
    global::ClankerWorld.Simulation.World.OrderTask? ParseCartOrder(string text, string actor);
    global::ClankerWorld.Simulation.World.OrderTask? ParseAnimalOrder(string text, string actor);
    global::ClankerWorld.Simulation.World.OrderTask? ParseGuardianOrder(string text, string actor);
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
    string FoodKnowledgeKind(global::ClankerWorld.Simulation.World.MapResource resource);
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation { get; }
    global::ClankerWorld.Simulation.World.PlacedBuilding? Port(string id);
    void CancelActiveOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string cause, string? reason);
    void MarkCheckpointCurrent();
}
