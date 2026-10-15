using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IProductionOrdersWorld ProductionOrdersWorld => new ProductionOrdersAdapter(this);

    private sealed class ProductionOrdersAdapter(WorldRuntime runtime) : IProductionOrdersWorld
    {
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public WorldContentSimulationState Simulation => runtime.worldSimulation;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public OrderStore Orders => runtime.orderStore;
        public long WorldTick => runtime.WorldTick;
        public AgentStore PhysicalAgents => runtime.agentStore;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public bool TryFindRecipeSite(RecipeDefinition recipe, out string siteId, out GridPoint position, string? actorId = null) => runtime.TryFindRecipeSite(recipe, out siteId, out position, actorId);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public bool CanResumeHeldInputs(string actor, string buildingId, IReadOnlyList<string> reservationIds) => runtime.CanResumeHeldInputs(actor, buildingId, reservationIds);
        public bool TryResumeKnife(string actor, string jobId, out string? knifeId) => runtime.TryResumeKnife(actor, jobId, out knifeId);
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public bool IsProductionOrderProject(OwnerQueuedInstruction instruction, AgentProject? project) => WorldRuntime.IsProductionOrderProject(instruction, project);
        public void SetProject(string agentId, AgentProject project) => runtime.SetProject(agentId, project);
        public void ContinueProject(string agentId, AgentState state) => runtime.ContinueProject(agentId, state);
        public bool MayFollowOwnerOrder(string actor, OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool IsConversationBusy(string agentId) => runtime.IsConversationBusy(agentId);
        public ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public string ProductionOrderReceipt(string jobId) => WorldRuntime.ProductionOrderReceipt(jobId);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public void ExtendOrderReservations(IReadOnlyList<string> reservations, long deadline) => runtime.RunInventoryCommand(InventorySystem.OrdersExtendOrderReservations(reservations, deadline));
        public void HoldOrderReservations(IReadOnlyList<string> reservations) => runtime.RunInventoryCommand(InventorySystem.OrdersHoldOrderReservations(reservations));
        public void ReleaseProductionOrderInputs(WorldProductionJob job) => runtime.RunInventoryCommand(InventorySystem.ReleaseProductionOrderInputs(job));
        public void ResumeProductionJob(string job, long deadline, string? knife) => runtime.productionSystem.ResumeJob(job, deadline, knife);
        public void PauseProductionJob(string job, long tick) => runtime.productionSystem.PauseJob(job, tick);
        public void CancelProductionJobs(IReadOnlySet<string> jobs) => runtime.productionSystem.CancelJobs(jobs);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
        public bool IsHouseholdBuildingTag(string tag) => WorldRuntime.IsHouseholdBuildingTag(tag);
    }
}
