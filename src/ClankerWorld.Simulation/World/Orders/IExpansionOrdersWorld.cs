using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IExpansionOrdersWorld
{
    WorldContentSimulationState Simulation { get; }
    DeclarativeWorldContentState Content { get; }
    OrderStore Orders { get; }
    long WorldTick { get; }
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    bool IsAdult(string actor);
    string ExpansionOwner(PlacedBuilding building);
    string HouseholdFor(string actor);
    string? TownForResident(string residentId);
    bool ExpansionJobMatchesOrder(BuildingExpansionJob job, OwnerQueuedInstruction instruction);
    System.Collections.Generic.IEnumerable<(ClankerWorld.Simulation.World.GridPoint Position, ClankerWorld.Simulation.World.BuildingFootprintRevision Footprint)> ExpansionShapes(PlacedBuilding building);
    bool CanFitExpansion(PlacedBuilding building, GridPoint position, BuildingFootprintRevision footprint, out string failure, string? ownJobId = null, bool requireLandRights = true);
    bool ReadyForBriefInteraction(string actor);
    bool MayExpandBuilding(string actor, PlacedBuilding building, out string failure);
    System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0);
    bool CanAcquireProjectInputs(IReadOnlyList<ContentQuantity> inputs, string? ownerId = null, string? residentId = null, Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>? reachableToolCache = null, Lazy<PlacedBuilding[]>? accessibleWarehouses = null);
    bool CanAcquireExpansionMaterials(string actor, PlacedBuilding building, IReadOnlyList<ContentQuantity> costs);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    void AppendEvent(string kind, string detail, GridPoint? eventPosition = null);
    void ApplyBuildingExpansionCandidate(string actor, AgentState state, string buildingId, OwnerQueuedInstruction? orderInstruction = null, ActionPlan? originalPlan = null);
    ClankerWorld.Simulation.Kernel.InventoryGroundPosition ExpansionGroundPosition(PlacedBuilding building);
    bool IsActiveOrder(string status);
    bool IsConversationBusy(string agentId);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    string ExpansionOrderReceipt(string jobId);
    void ExtendOrderReservations(IReadOnlyList<string> reservations, long deadline);
    void HoldOrderReservations(IReadOnlyList<string> reservations);
    void ReleaseExpansionOrderInputs(BuildingExpansionJob job);
    void ResumeExpansion(string job, long deadline);
    void PauseExpansion(string job, long tick);
    void CancelExpansions(IReadOnlySet<string> jobs, string reason);
    void MarkCheckpointCurrent();
}
