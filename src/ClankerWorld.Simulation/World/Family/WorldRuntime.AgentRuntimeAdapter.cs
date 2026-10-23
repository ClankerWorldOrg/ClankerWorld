using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private abstract class AgentRuntimeAdapter(WorldRuntime runtime)
    {
        protected WorldRuntime Runtime { get; } = runtime;

        public SocietyCheckpoint Society => Runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, AgentState> Agents => Runtime.agents;
        public bool NeedsUrgentWarmth(AgentState person) => Runtime.NeedsUrgentWarmth(person);
        public long WorldTick => Runtime.WorldTick;
        public AgentStore PhysicalAgents => Runtime.agentStore;
        public void MarkCheckpointCurrent() => Runtime.checkpointSchemaVersion = StateSchemaVersion;
        public IReadOnlyList<TownRuntimeState> Towns => Runtime.towns;
        public DeclarativeWorldContentState Content => Runtime.worldContent;
        public CognitionSystem Host => Runtime.cognitionSystem;
        public ActionCheck CheckActionPlan(ActionPlan plan) => Runtime.CheckActionPlan(plan);
        public void CancelProviderCall(global::System.Threading.CancellationTokenSource cancellation, bool underRuntimeGate) => Runtime.CancelProviderCall(cancellation, underRuntimeGate);
        public bool ReadyForBriefInteraction(string actor) => Runtime.ReadyForBriefInteraction(actor);
        public void FinishModelWork(string actorId, string requestId, global::ClankerWorld.Simulation.Cognition.ModelWorkStage stage, long? admittedTick = null) => Runtime.FinishModelWork(actorId, requestId, stage, admittedTick);
        public IReadOnlySet<string>? WithheldActions => Runtime.withheldActions;
        public List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateCandidates(string agentId, AgentState state, bool restrictForOrder = true) => Runtime.CreateCandidates(agentId, state, restrictForOrder);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? OrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => Runtime.OrderCandidateFor(instruction, person);
        public bool ShouldInterruptOrder(AgentState person, OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? orderCandidate, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? urgentCandidate) => Runtime.ShouldInterruptOrder(person, instruction, orderCandidate, urgentCandidate);
        public OrderStore Orders => Runtime.orderStore;
        public SocietyWorldCapture Capture() => Runtime.society.Capture();
        public bool HasSurvivalState => Runtime.survivalState is not null;
        public ActorCapabilityState? CapabilitiesFor(string actor) => Runtime.CapabilitiesFor(actor);
        public IReadOnlyList<PlacedBuilding> Buildings => Runtime.worldSimulation.Buildings;
        public ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) => Runtime.Reach(actor, target, purpose, range, load, swimming);
        public int FreeCarryCapacity(string actor) => Runtime.FreeCarryCapacity(actor);
        public WorldMap Map => Runtime.map;
        public bool CanReach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) => Runtime.CanReach(actor, target, purpose, range, load, swimming);
        public GoodsMatch? RecheckGoods(GoodsRequest request, string lot) => Runtime.RecheckGoods(request, lot);
        public GridPoint HouseholdStockPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => Runtime.HouseholdStockPosition(lot);
        public global::System.TimeProvider ModelTimeProvider => Runtime.modelTimeProvider;
        public GridPoint SharedStoragePosition => Runtime.SharedStoragePosition;
        public bool IsChild(string actor) => global::ClankerWorld.Simulation.World.AgentQueries.IsChild(Runtime.agentWorld, actor);
        public string HouseholdFor(string actor) => Runtime.HouseholdFor(actor);
        public void MarkCurrentSchema() => Runtime.checkpointSchemaVersion = StateSchemaVersion;
        public ReadOnlyCollection<AgentMarriage> Marriages => Runtime.marriages;
        public void MarkModelReplyAdmitted(string requestId) => Runtime.admittedModelReplies.Add(requestId);
        public FamilyTurnState GuardianPlacementActions => Runtime.guardianPlacementActions;
        public void RefuseChoice(ActionPlan plan, ActionRefusal refusal) => Runtime.RefuseChoice(plan, refusal);
        public Dictionary<string, string>? AttemptedActions => Runtime.attemptedActions;
        public bool TalkOrderHasConversation(OwnerQueuedInstruction instruction) => Runtime.TalkOrderHasConversation(instruction);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, OrderStatus status, string? reason, bool waitForDecision = false) => Runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string OrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) => Runtime.OrderBlockedReason(instruction, person);
        public bool ApplyNonLandRemedyAction(TownRuntimeState town, string actor, string candidateId, ActionPlan? plan = null) => Runtime.ApplyNonLandRemedyAction(town, actor, candidateId, plan);
        public bool IsInstructionComplete(OwnerQueuedInstruction instruction) => Runtime.IsInstructionComplete(instruction);
        public bool CanContinueEquipmentRepair(string actor) => Runtime.CanContinueEquipmentRepair(actor);
        public bool CanContinueProject(AgentState state, bool waitingForModel = false) => Runtime.CanContinueProject(state, waitingForModel);
        public void RecordModelAttempt(string id, string status, string? acceptedCandidateId = null, string? setupBlocker = null) => Runtime.RecordModelAttempt(id, status, acceptedCandidateId, setupBlocker);
        public bool IsUndeliveredFinishedOrderDecision(global::ClankerWorld.Simulation.Cognition.AgentObservation observation) => Runtime.IsUndeliveredFinishedOrderDecision(observation);
        public bool ShouldDispatchConversationChoice(string agentId) => Runtime.ShouldDispatchConversationChoice(agentId);
        public string? ConversationChoiceContextFor(string agentId) => Runtime.ConversationChoiceContextFor(agentId);
        public bool TryFindRecipeSite(RecipeDefinition recipe, out string siteId, out GridPoint position, string? actorId = null) => Runtime.TryFindRecipeSite(recipe, out siteId, out position, actorId);
    }
}
