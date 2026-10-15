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

internal interface ICognitionObservationsWorld
{
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    long WorldTick { get; }
    IReadOnlyList<TownRuntimeState> Towns { get; }
    ReadOnlyCollection<AgentMarriage> Marriages { get; }
    WorldKnowledgeState Knowledge { get; }
    DeclarativeWorldContentState Content { get; }
    bool jevEnabled { get; }
    CognitionSystem Host { get; }
    OrderStore Orders { get; }
    AgentStore PhysicalAgents { get; }
    IReadOnlySet<string> PendingModelAgentIds();
    bool IsOrderDecisionObservationCurrent(AgentObservation observation);
    bool IsUndeliveredFinishedOrderDecision(AgentObservation observation);
    void CloseOrdersNotUnderstood(string agentId);
    OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    bool HasNewObserverGuidanceFor(string agentId);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    bool IsConversationBusy(string agentId);
    FarmFieldState? FarmWorkFor(string workerId);
    bool ShouldDispatchConversationChoice(string agentId);
    CognitionCandidate? GuardianPlacementCandidate(string actor);
    bool IsProductionOrderProject(OwnerQueuedInstruction instruction, AgentProject? project);
    bool IsConstructionOrderProject(OwnerQueuedInstruction instruction, AgentProject? project);
    void SetProject(string agentId, AgentProject project);
    bool IsProtectiveProject(AgentProject? project);
    string? ConversationChoiceContextFor(string agentId);
    List<CognitionCandidate> CreateCandidates(string agentId, AgentState state, bool restrictForOrder = true);
    string? DestinationNameForModel(string? id);
    bool PauseExpiredUnfillableHouseholdRecipeProject(string agentId);
    bool NeedsCognition(string agentId, CognitionIntention? current, List<CognitionCandidate> candidates, string? conversationChoiceContext);
    CognitionObserverGuidance[] ObserverGuidanceFor(string agentId);
    CognitionKnowledgeFact[] KnownMapFactsForCognition(string agentId);
    string? GuardianPlacementModelNote(string actor);
    string EquipmentNote(string actor);
    string? ContinuityNote(string actor);
    string DepartureNote(string actor);
    string? CivicNote(string actor);
    string? MedicalCareNoteCore(string actor);
    string? TownMembershipNote(string actor);
    string? ToolMakingRequestNoteCore(string actor);
    CognitionParentIdentity[]? InitialChildFamilyBackground(string id, AgentState physical);
    bool IsAdult(string actor);
    bool EnqueueCognition(SocietyCognitionScheduleEntry entry);
    int PriorityFor(AgentState state);
    void AppendEvent(string kind, string detail, GridPoint? eventPosition = null);
    string DecisionContext(AgentState state, List<CognitionCandidate> candidates, string? conversationChoiceContext = null);
    SocietyCognitionSchedulerState CaptureCognition();
    void MarkCheckpointCurrent();
}
