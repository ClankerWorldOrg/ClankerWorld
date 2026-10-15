using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionObservationsWorld CognitionObservationsWorld => new CognitionObservationsAdapter(this);

    private sealed class CognitionObservationsAdapter(WorldRuntime runtime) : ICognitionObservationsWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public long WorldTick => runtime.WorldTick;
        public IReadOnlyList<TownRuntimeState> Towns => runtime.towns;
        public ReadOnlyCollection<AgentMarriage> Marriages => runtime.marriages;
        public WorldKnowledgeState Knowledge => runtime.knowledge;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public bool jevEnabled => runtime.jevEnabled;
        public CognitionSystem Host => runtime.cognitionSystem;
        public OrderStore Orders => runtime.orderStore;
        public AgentStore PhysicalAgents => runtime.agentStore;
        public IReadOnlySet<string> PendingModelAgentIds() => runtime.society.PendingModelAgentIds();
        public bool IsOrderDecisionObservationCurrent(AgentObservation observation) => runtime.IsOrderDecisionObservationCurrent(observation);
        public bool IsUndeliveredFinishedOrderDecision(AgentObservation observation) => runtime.IsUndeliveredFinishedOrderDecision(observation);
        public void CloseOrdersNotUnderstood(string agentId) => runtime.CloseOrdersNotUnderstood(agentId);
        public OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public bool HasNewObserverGuidanceFor(string agentId) => runtime.HasNewObserverGuidanceFor(agentId);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);
        public bool IsConversationBusy(string agentId) => runtime.IsConversationBusy(agentId);
        public FarmFieldState? FarmWorkFor(string workerId) => runtime.FarmWorkFor(workerId);
        public bool ShouldDispatchConversationChoice(string agentId) => runtime.ShouldDispatchConversationChoice(agentId);
        public CognitionCandidate? GuardianPlacementCandidate(string actor) => runtime.GuardianPlacementCandidate(actor);
        public bool IsProductionOrderProject(OwnerQueuedInstruction instruction, AgentProject? project) => WorldRuntime.IsProductionOrderProject(instruction, project);
        public bool IsConstructionOrderProject(OwnerQueuedInstruction instruction, AgentProject? project) => WorldRuntime.IsConstructionOrderProject(instruction, project);
        public void SetProject(string agentId, AgentProject project) => runtime.SetProject(agentId, project);
        public bool IsProtectiveProject(AgentProject? project) => runtime.IsProtectiveProject(project);
        public string? ConversationChoiceContextFor(string agentId) => runtime.ConversationChoiceContextFor(agentId);
        public List<CognitionCandidate> CreateCandidates(string agentId, AgentState state, bool restrictForOrder = true) => runtime.CreateCandidates(agentId, state, restrictForOrder);
        public string? DestinationNameForModel(string? id) => runtime.DestinationNameForModel(id);
        public bool PauseExpiredUnfillableHouseholdRecipeProject(string agentId) => runtime.PauseExpiredUnfillableHouseholdRecipeProject(agentId);
        public bool NeedsCognition(string agentId, CognitionIntention? current, List<CognitionCandidate> candidates, string? conversationChoiceContext) => runtime.NeedsCognition(agentId, current, candidates, conversationChoiceContext);
        public CognitionObserverGuidance[] ObserverGuidanceFor(string agentId) => runtime.ObserverGuidanceFor(agentId);
        public CognitionKnowledgeFact[] KnownMapFactsForCognition(string agentId) => runtime.KnownMapFactsForCognition(agentId);
        public string? GuardianPlacementModelNote(string actor) => runtime.GuardianPlacementModelNote(actor);
        public string EquipmentNote(string actor) => runtime.EquipmentNote(actor);
        public string? ContinuityNote(string actor) => runtime.ContinuityNote(actor);
        public string DepartureNote(string actor) => runtime.DepartureNote(actor);
        public string? CivicNote(string actor) => runtime.CivicNote(actor);
        public string? MedicalCareNoteCore(string actor) => runtime.MedicalCareNoteCore(actor);
        public string? TownMembershipNote(string actor) => runtime.TownMembershipNote(actor);
        public string? ToolMakingRequestNoteCore(string actor) => runtime.ToolMakingRequestNoteCore(actor);
        public CognitionParentIdentity[]? InitialChildFamilyBackground(string id, AgentState physical) => runtime.InitialChildFamilyBackground(id, physical);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool EnqueueCognition(SocietyCognitionScheduleEntry entry) => runtime.society.EnqueueCognition(entry);
        public int PriorityFor(AgentState state) => runtime.PriorityFor(state);
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public string DecisionContext(AgentState state, List<CognitionCandidate> candidates, string? conversationChoiceContext = null) => runtime.DecisionContext(state, candidates, conversationChoiceContext);
        public SocietyCognitionSchedulerState CaptureCognition() => runtime.society.Capture().Cognition;
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
