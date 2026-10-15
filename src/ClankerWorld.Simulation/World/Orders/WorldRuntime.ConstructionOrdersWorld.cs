using TownLayoutBasis = ClankerWorld.Simulation.World.WorldRuntime.TownLayoutBasis;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IConstructionOrdersWorld ConstructionOrdersWorld => new ConstructionOrdersAdapter(this);

    private sealed class ConstructionOrdersAdapter(WorldRuntime runtime) : IConstructionOrdersWorld
    {
        public WorldContentSimulationState Simulation => runtime.worldSimulation;
        public OrderStore Orders => runtime.orderStore;
        public long WorldTick => runtime.WorldTick;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public AgentStore PhysicalAgents => runtime.agentStore;
        public string ConstructionInstanceId(string instructionId) => WorldRuntime.ConstructionInstanceId(instructionId);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool IsConstructionOrderProject(OwnerQueuedInstruction instruction, AgentProject? project) => WorldRuntime.IsConstructionOrderProject(instruction, project);
        public bool OrdinaryProjectCanYieldToOrder(AgentProject project) => runtime.OrdinaryProjectCanYieldToOrder(project);
        public string? HouseholdBuildingKind(BuildingDefinition definition) => WorldRuntime.HouseholdBuildingKind(definition);
        public ClankerWorld.Simulation.World.PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag) => runtime.HouseholdBuildingWithTag(householdId, tag);
        public ClankerWorld.Simulation.World.PlacedBuilding? FarmhouseForHousehold(string householdId) => runtime.FarmhouseForHousehold(householdId);
        public ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId) => runtime.HouseForHousehold(householdId);
        public ClankerWorld.Simulation.World.TownLayoutContext CreateTownLayoutContext(string actor, GridPoint? selectedSite = null, BuildingDefinition? building = null, bool forTownProject = false, string? townProjectId = null, TownLayoutBasis? basis = null) => runtime.CreateTownLayoutContext(actor, selectedSite, building, forTownProject, townProjectId, basis);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public void SetProject(string agentId, AgentProject project) => runtime.SetProject(agentId, project);
        public void ContinueProject(string agentId, AgentState state) => runtime.ContinueProject(agentId, state);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string ConstructionOrderReceipt(string instanceId) => WorldRuntime.ConstructionOrderReceipt(instanceId);
        public void RecordConstructionReceipt(WorldConstructionReceipt receipt) => runtime.constructionSystem.Buildings.RecordConstructionReceipt(receipt);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
