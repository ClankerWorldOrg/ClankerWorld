using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IControlWorld
{
    OrderStore Orders { get; }
    WorldMap Map { get; }
    DeclarativeWorldContentState Content { get; }
    WorldContentSimulationState Simulation { get; }
    bool IsActiveOrder(string status);
    void CancelRepairForOrder(OwnerQueuedInstruction instruction);
    void CancelFieldWorkForOrder(OwnerQueuedInstruction instruction);
    void CancelProductionForOrder(OwnerQueuedInstruction instruction);
    void CancelConstructionForOrder(OwnerQueuedInstruction instruction);
    void CancelExpansionForOrder(OwnerQueuedInstruction instruction);
    void CancelAnimalSupplyForOrder(OwnerQueuedInstruction instruction);
    void CancelBoatTravelForOrder(OwnerQueuedInstruction instruction);
    void CancelKnowledgeWritingForOrder(OwnerQueuedInstruction instruction);
    void AppendEvent(string kind, string detail, GridPoint? eventPosition = null);
    string ToWireValue(OwnerInstructionKind kind);
    string? UnderstoodTaskFor(string? candidate);
    ClankerWorld.Simulation.World.OrderTask? ParseMarriageOrder(string text, string actor);
    ClankerWorld.Simulation.World.OrderTask? ParseKnowledgeReadOrder(string text);
    ClankerWorld.Simulation.World.OrderTask? ParseTalkOrder(string text, string actor);
    ClankerWorld.Simulation.World.OrderTask? ParseCartCargoOrder(string text, string actor);
    ClankerWorld.Simulation.World.OrderTask? ParseCartOrder(string text, string actor);
    ClankerWorld.Simulation.World.OrderTask? ParseAnimalOrder(string text, string actor);
    ClankerWorld.Simulation.World.OrderTask? ParseGuardianOrder(string text, string actor);
    ClankerWorld.Simulation.World.PlacedBuilding? Port(string id);
    void MarkCheckpointCurrent();
    string FoodKnowledgeKind(MapResource resource);
}
