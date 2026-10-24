namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IControlWorld ControlWorld => new ControlAdapter(this);

    private sealed class ControlAdapter(WorldRuntime runtime) : IControlWorld
    {
        public void CancelRepairForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.CancelRepairForOrder(runtime.RepairOrdersWorld, instruction);
        public void CancelFieldWorkForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.CancelFieldWorkForOrder(runtime.FieldOrdersWorld, instruction);
        public void CancelProductionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.CancelProductionForOrder(instruction);
        public void CancelConstructionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.CancelConstructionForOrder(runtime.ConstructionOrdersWorld, instruction);
        public void CancelExpansionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.CancelExpansionForOrder(runtime.ExpansionOrdersWorld, instruction);
        public void CancelAnimalSupplyForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.CancelAnimalSupplyForOrder(instruction);
        public void CancelBoatTravelForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.CancelBoatTravelForOrder(runtime.BoatOrdersWorld, instruction);
        public void CancelKnowledgeWritingForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.CancelKnowledgeWritingForOrder(runtime.KnowledgeOrdersWorld, instruction);
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction[] ObserverGuidanceInstructionsFor(string agentId) => OrderSystem.ObserverGuidanceInstructionsFor(runtime.ControlWorld, agentId);
        public OrderStore Orders => runtime.orderStore;
        public bool IsInstructionComplete(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.IsInstructionComplete(instruction);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public string ToWireValue(global::ClankerWorld.Simulation.World.OwnerInstructionKind kind) => WorldRuntime.ToWireValue(kind);
        public string? UnderstoodTaskFor(string? candidate) => WorldRuntime.UnderstoodTaskFor(candidate);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseMarriageOrder(string text, string actor) => OrderSystem.ParseMarriageOrder(runtime.MarriageOrdersWorld, text, actor);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseKnowledgeReadOrder(string text) => OrderSystem.ParseKnowledgeReadOrder(runtime.KnowledgeReadOrdersWorld, text);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseTalkOrder(string text, string actor) => OrderSystem.ParseTalkOrder(runtime.TalkOrdersWorld, text, actor);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseCartCargoOrder(string text, string actor) => OrderSystem.ParseCartCargoOrder(runtime.CartCargoOrdersWorld, text, actor);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseCartOrder(string text, string actor) => runtime.ParseCartOrder(text, actor);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseAnimalOrder(string text, string actor) => runtime.ParseAnimalOrder(text, actor);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseGuardianOrder(string text, string actor) => OrderSystem.ParseGuardianOrder(runtime.GuardianOrdersWorld, text, actor);
        public global::ClankerWorld.Simulation.World.WorldMap Map => runtime.map;
        public string FoodKnowledgeKind(global::ClankerWorld.Simulation.World.MapResource resource) => WorldRuntime.FoodKnowledgeKind(resource);
        public global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content => runtime.worldContent;
        public global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation => runtime.worldSimulation;
        public global::ClankerWorld.Simulation.World.PlacedBuilding? Port(string id) => runtime.Port(id);
        public void CancelActiveOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string cause, string? reason) => OrderSystem.CancelActiveOrder(runtime.ControlWorld, instruction, cause, reason);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
