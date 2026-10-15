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
    private ICognitionContextWorld CognitionContextWorld => new CognitionContextAdapter(this);

    private sealed class CognitionContextAdapter(WorldRuntime runtime) : ICognitionContextWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public OrderStore Orders => runtime.orderStore;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public long WorldTick => runtime.WorldTick;
        public long CognitionReevaluationIntervalTicks => WorldRuntime.CognitionReevaluationIntervalTicks;
        public string MedicalAllowPrefix => WorldRuntime.MedicalAllowPrefix;
        public OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public bool IsFinishedOrderDecisionAwaitingReply(AgentObservation observation) => runtime.IsFinishedOrderDecisionAwaitingReply(observation);
        public string? UnderstoodTaskFor(string? candidate) => WorldRuntime.UnderstoodTaskFor(candidate);
        public bool HasNewObserverGuidanceFor(string agentId) => runtime.HasNewObserverGuidanceFor(agentId);
        public bool CanContinueEquipmentRepair(string actor) => runtime.CanContinueEquipmentRepair(actor);
        public bool CanContinueLesson(string actor, bool waitingForModel = false) => runtime.CanContinueLesson(actor, waitingForModel);
        public bool CanContinueProject(AgentState state, bool waitingForModel = false) => runtime.CanContinueProject(state, waitingForModel);
        public string DecisionContext(AgentState state, List<CognitionCandidate> candidates, string? conversationChoiceContext = null) => runtime.DecisionContext(state, candidates, conversationChoiceContext);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);

    }
}
