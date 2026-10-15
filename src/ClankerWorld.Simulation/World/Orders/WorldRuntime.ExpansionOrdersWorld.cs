using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IExpansionOrdersWorld ExpansionOrdersWorld => new ExpansionOrdersAdapter(this);

    private sealed class ExpansionOrdersAdapter(WorldRuntime runtime) : IExpansionOrdersWorld
    {
        public WorldContentSimulationState Simulation => runtime.worldSimulation;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public OrderStore Orders => runtime.orderStore;
        public long WorldTick => runtime.WorldTick;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public string ExpansionOwner(PlacedBuilding building) => WorldRuntime.ExpansionOwner(building);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public string? TownForResident(string residentId) => runtime.TownForResident(residentId);
        public bool ExpansionJobMatchesOrder(BuildingExpansionJob job, OwnerQueuedInstruction instruction) => WorldRuntime.ExpansionJobMatchesOrder(job, instruction);
        public System.Collections.Generic.IEnumerable<(ClankerWorld.Simulation.World.GridPoint Position, ClankerWorld.Simulation.World.BuildingFootprintRevision Footprint)> ExpansionShapes(PlacedBuilding building) => runtime.ExpansionShapes(building);
        public bool CanFitExpansion(PlacedBuilding building, GridPoint position, BuildingFootprintRevision footprint, out string failure, string? ownJobId = null, bool requireLandRights = true) => runtime.CanFitExpansion(building, position, footprint, out failure, ownJobId, requireLandRights);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public bool MayExpandBuilding(string actor, PlacedBuilding building, out string failure) => runtime.MayExpandBuilding(actor, building, out failure);
        public System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public bool CanAcquireProjectInputs(IReadOnlyList<ContentQuantity> inputs, string? ownerId = null, string? residentId = null, Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>? reachableToolCache = null, Lazy<PlacedBuilding[]>? accessibleWarehouses = null) => runtime.CanAcquireProjectInputs(inputs, ownerId, residentId, reachableToolCache, accessibleWarehouses);
        public bool CanAcquireExpansionMaterials(string actor, PlacedBuilding building, IReadOnlyList<ContentQuantity> costs) => runtime.CanAcquireExpansionMaterials(actor, building, costs);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public void ApplyBuildingExpansionCandidate(string actor, AgentState state, string buildingId, OwnerQueuedInstruction? orderInstruction = null, ActionPlan? originalPlan = null) => runtime.ApplyBuildingExpansionCandidate(actor, state, buildingId, orderInstruction, originalPlan);
        public ClankerWorld.Simulation.Kernel.InventoryGroundPosition ExpansionGroundPosition(PlacedBuilding building) => WorldRuntime.ExpansionGroundPosition(building);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public bool IsConversationBusy(string agentId) => runtime.IsConversationBusy(agentId);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string ExpansionOrderReceipt(string jobId) => WorldRuntime.ExpansionOrderReceipt(jobId);
        public void ExtendOrderReservations(IReadOnlyList<string> reservations, long deadline) => runtime.RunInventoryCommand(InventorySystem.ExtendOrderReservations(reservations, deadline));
        public void HoldOrderReservations(IReadOnlyList<string> reservations) => runtime.RunInventoryCommand(InventorySystem.HoldOrderReservations(reservations));
        public void ReleaseExpansionOrderInputs(BuildingExpansionJob job) => runtime.RunInventoryCommand(InventorySystem.ReleaseExpansionOrderInputs(job));
        public void ResumeExpansion(string job, long deadline) => runtime.constructionSystem.Buildings.ResumeExpansion(job, deadline);
        public void PauseExpansion(string job, long tick) => runtime.constructionSystem.Buildings.PauseExpansion(job, tick);
        public void CancelExpansions(IReadOnlySet<string> jobs, string reason) => runtime.constructionSystem.Buildings.CancelPausedExpansions(jobs, reason);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
