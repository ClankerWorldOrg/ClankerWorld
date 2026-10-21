using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IProductionOrdersWorld ProductionOrdersWorld => new ProductionOrdersAdapter(this);

    private sealed class ProductionOrdersAdapter(WorldRuntime runtime) : IProductionOrdersWorld
    {
        public global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content => runtime.worldContent;
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public global::ClankerWorld.Simulation.World.WorldProductionJob? BoundProductionJob(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.BoundProductionJob(instruction);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? ProductionOrderBuilding(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Content.RecipeDefinition recipe) => runtime.ProductionOrderBuilding(instruction, recipe);
        public bool TryFindRecipeSite(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, out string siteId, out global::ClankerWorld.Simulation.World.GridPoint position, string? actorId = null) => runtime.TryFindRecipeSite(recipe, out siteId, out position, actorId);
        public global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation => runtime.worldSimulation;
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public bool IsHouseholdBuildingTag(string tag) => WorldRuntime.IsHouseholdBuildingTag(tag);
        public OrderStore Orders => runtime.orderStore;
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person) => runtime.NeedsUrgentWarmth(person);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ProductionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ProductionOrderCandidateFor(instruction, person);
        public string ProductionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.ProductionOrderBlockedReason(instruction);
        public void PauseProductionOrderJob(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.WorldProductionJob job) => runtime.PauseProductionOrderJob(instruction, job);
        public void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public bool CanResumeHeldInputs(string actor, string buildingId, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Identity> reservationIds) => runtime.CanResumeHeldInputs(actor, buildingId, reservationIds);
        public bool TryResumeKnife(string actor, string jobId, out string? knifeId) => runtime.TryResumeKnife(actor, jobId, out knifeId);
        public long WorldTick => runtime.WorldTick;
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public bool IsProductionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project) => WorldRuntime.IsProductionOrderProject(instruction, project);
        public global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents => runtime.agentStore;
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public void SetProject(string agentId, global::ClankerWorld.Simulation.World.AgentProject project) => runtime.SetProject(agentId, project);
        public void ContinueProject(string agentId, global::ClankerWorld.Simulation.World.AgentState state) => runtime.ContinueProject(agentId, state);
        public bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public void CancelProductionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.CancelProductionForOrder(instruction);
        public bool IsConversationBusy(string agentId) => runtime.IsConversationBusy(agentId);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public string ProductionOrderReceipt(string jobId) => WorldRuntime.ProductionOrderReceipt(jobId);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
        public void ExtendOrderReservations(IReadOnlyList<Identity> reservations, long deadline) => runtime.ApplyInventoryTransition(inventory => InventoryAuthority.SetReservationDeadline(inventory, reservations, deadline));
        public void HoldOrderReservations(IReadOnlyList<Identity> reservations) => runtime.ApplyInventoryTransition(inventory => InventoryAuthority.HoldReservations(inventory, reservations));
        public void ReleaseProductionOrderInputs(WorldProductionJob job) => runtime.ApplyInventoryTransition(inventory =>
            {
                foreach (var id in job.InputReservationIds)
                    if (inventory.Reservations.FirstOrDefault(item => item.Id == id) is
                        { State: InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed })
                        inventory = InventoryAuthority.ReleaseReservation(inventory, id, "production_order_cancelled");
                return inventory;
            });
        public void ResumeProductionJob(string job, long deadline, string? knife) => runtime.productionSystem.ResumeJob(job, deadline, knife);
        public void PauseProductionJob(string job, long tick) => runtime.productionSystem.PauseJob(job, tick);
        public void CancelProductionJobs(IReadOnlySet<string> jobs) => runtime.productionSystem.CancelJobs(jobs);
    }
}
