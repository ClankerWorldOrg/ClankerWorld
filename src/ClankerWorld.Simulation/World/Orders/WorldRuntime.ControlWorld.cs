using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IControlWorld ControlWorld => new ControlAdapter(this);

    private sealed class ControlAdapter(WorldRuntime runtime) : IControlWorld
    {
        public OrderStore Orders => runtime.orderStore;
        public WorldMap Map => runtime.map;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public WorldContentSimulationState Simulation => runtime.worldSimulation;
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public void CancelRepairForOrder(OwnerQueuedInstruction instruction) => runtime.CancelRepairForOrder(instruction);
        public void CancelFieldWorkForOrder(OwnerQueuedInstruction instruction) => runtime.CancelFieldWorkForOrder(instruction);
        public void CancelProductionForOrder(OwnerQueuedInstruction instruction) => runtime.CancelProductionForOrder(instruction);
        public void CancelConstructionForOrder(OwnerQueuedInstruction instruction) => runtime.CancelConstructionForOrder(instruction);
        public void CancelExpansionForOrder(OwnerQueuedInstruction instruction) => runtime.CancelExpansionForOrder(instruction);
        public void CancelAnimalSupplyForOrder(OwnerQueuedInstruction instruction) => runtime.CancelAnimalSupplyForOrder(instruction);
        public void CancelBoatTravelForOrder(OwnerQueuedInstruction instruction) => runtime.CancelBoatTravelForOrder(instruction);
        public void CancelKnowledgeWritingForOrder(OwnerQueuedInstruction instruction) => runtime.CancelKnowledgeWritingForOrder(instruction);
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public string ToWireValue(OwnerInstructionKind kind) => WorldRuntime.ToWireValue(kind);
        public string? UnderstoodTaskFor(string? candidate) => WorldRuntime.UnderstoodTaskFor(candidate);
        public ClankerWorld.Simulation.World.OrderTask? ParseMarriageOrder(string text, string actor) => runtime.ParseMarriageOrder(text, actor);
        public ClankerWorld.Simulation.World.OrderTask? ParseKnowledgeReadOrder(string text) => runtime.ParseKnowledgeReadOrder(text);
        public ClankerWorld.Simulation.World.OrderTask? ParseTalkOrder(string text, string actor) => runtime.ParseTalkOrder(text, actor);
        public ClankerWorld.Simulation.World.OrderTask? ParseCartCargoOrder(string text, string actor) => runtime.ParseCartCargoOrder(text, actor);
        public ClankerWorld.Simulation.World.OrderTask? ParseCartOrder(string text, string actor) => runtime.ParseCartOrder(text, actor);
        public ClankerWorld.Simulation.World.OrderTask? ParseAnimalOrder(string text, string actor) => runtime.ParseAnimalOrder(text, actor);
        public ClankerWorld.Simulation.World.OrderTask? ParseGuardianOrder(string text, string actor) => runtime.ParseGuardianOrder(text, actor);
        public ClankerWorld.Simulation.World.PlacedBuilding? Port(string id) => runtime.Port(id);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
        public string FoodKnowledgeKind(MapResource resource) => WorldRuntime.FoodKnowledgeKind(resource);
    }
}
