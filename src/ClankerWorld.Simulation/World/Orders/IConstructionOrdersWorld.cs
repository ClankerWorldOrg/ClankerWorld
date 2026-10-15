using TownLayoutBasis = ClankerWorld.Simulation.World.WorldRuntime.TownLayoutBasis;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IConstructionOrdersWorld
{
    WorldContentSimulationState Simulation { get; }
    OrderStore Orders { get; }
    long WorldTick { get; }
    DeclarativeWorldContentState Content { get; }
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    AgentStore PhysicalAgents { get; }
    string ConstructionInstanceId(string instructionId);
    bool IsAdult(string actor);
    bool IsConstructionOrderProject(OwnerQueuedInstruction instruction, AgentProject? project);
    bool OrdinaryProjectCanYieldToOrder(AgentProject project);
    string? HouseholdBuildingKind(BuildingDefinition definition);
    ClankerWorld.Simulation.World.PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag);
    ClankerWorld.Simulation.World.PlacedBuilding? FarmhouseForHousehold(string householdId);
    ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId);
    ClankerWorld.Simulation.World.TownLayoutContext CreateTownLayoutContext(string actor, GridPoint? selectedSite = null, BuildingDefinition? building = null, bool forTownProject = false, string? townProjectId = null, TownLayoutBasis? basis = null);
    bool ReadyForBriefInteraction(string actor);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    void AppendEvent(string kind, string detail, GridPoint? eventPosition = null);
    void SetProject(string agentId, AgentProject project);
    void ContinueProject(string agentId, AgentState state);
    bool IsActiveOrder(string status);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    string ConstructionOrderReceipt(string instanceId);
    void RecordConstructionReceipt(WorldConstructionReceipt receipt);
    void MarkCheckpointCurrent();
}
