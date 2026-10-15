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

internal interface ICognitionContextWorld
{
    SocietyCheckpoint Society { get; }
    OrderStore Orders { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    long WorldTick { get; }
    long CognitionReevaluationIntervalTicks { get; }
    string MedicalAllowPrefix { get; }
    OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    bool IsFinishedOrderDecisionAwaitingReply(AgentObservation observation);
    string? UnderstoodTaskFor(string? candidate);
    bool HasNewObserverGuidanceFor(string agentId);
    bool CanContinueEquipmentRepair(string actor);
    bool CanContinueLesson(string actor, bool waitingForModel = false);
    bool CanContinueProject(AgentState state, bool waitingForModel = false);
    string DecisionContext(AgentState state, List<CognitionCandidate> candidates, string? conversationChoiceContext = null);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);

}
