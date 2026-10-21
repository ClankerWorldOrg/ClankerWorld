using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IConstructionOrdersWorld ConstructionOrdersWorld => new ConstructionOrdersAdapter(this);

    private sealed class ConstructionOrdersAdapter(WorldRuntime runtime) : IConstructionOrdersWorld
    {
        public string ConstructionInstanceId(string instructionId) => WorldRuntime.ConstructionInstanceId(instructionId);
        public global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation => runtime.worldSimulation;
        public OrderStore Orders => runtime.orderStore;
        public long WorldTick => runtime.WorldTick;
        public global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content => runtime.worldContent;
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool IsConstructionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project) => WorldRuntime.IsConstructionOrderProject(instruction, project);
        public bool OrdinaryProjectCanYieldToOrder(global::ClankerWorld.Simulation.World.AgentProject project) => runtime.OrdinaryProjectCanYieldToOrder(project);
        public string? HouseholdBuildingKind(global::ClankerWorld.Simulation.Content.BuildingDefinition definition) => WorldRuntime.HouseholdBuildingKind(definition);
        public bool TownHasRoomForSharedShop(string actor, string? kind) => runtime.TownHasRoomForSharedShop(actor, kind);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag) => runtime.HouseholdBuildingWithTag(householdId, tag);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? FarmhouseForHousehold(string householdId) => runtime.FarmhouseForHousehold(householdId);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId) => runtime.HouseForHousehold(householdId);
        public bool WorkBuildingHasInputs(string actor, string household, global::ClankerWorld.Simulation.Content.BuildingDefinition building) => runtime.WorkBuildingHasInputs(actor, household, building);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public global::ClankerWorld.Simulation.World.TownLayoutContext CreateTownLayoutContext(string actor, global::ClankerWorld.Simulation.World.GridPoint? selectedSite = null, global::ClankerWorld.Simulation.Content.BuildingDefinition? building = null) => runtime.CreateTownLayoutContext(actor, selectedSite, building);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public bool TryConstructionOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, out global::ClankerWorld.Simulation.Content.BuildingDefinition? definition, out string? household, out global::ClankerWorld.Simulation.World.GridPoint position, out string failure) => runtime.TryConstructionOrderSite(instruction, person, out definition, out household, out position, out failure);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person) => runtime.NeedsUrgentWarmth(person);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents => runtime.agentStore;
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public void SetProject(string agentId, global::ClankerWorld.Simulation.World.AgentProject project) => runtime.SetProject(agentId, project);
        public void ContinueProject(string agentId, global::ClankerWorld.Simulation.World.AgentState state) => runtime.ContinueProject(agentId, state);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public void CancelConstructionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.CancelConstructionForOrder(instruction);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string ConstructionOrderReceipt(string instanceId) => WorldRuntime.ConstructionOrderReceipt(instanceId);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
        public void RecordConstructionReceipt(WorldConstructionReceipt receipt) => runtime.constructionSystem.Buildings.RecordConstructionReceipt(receipt);
    }
}
