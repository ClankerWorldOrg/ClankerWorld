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

internal interface ICognitionWaitingRoutinesWorld
{
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    FamilyTurnState FamilyTurn { get; }
    IReadOnlySet<string>? WithheldActions { get; }
    int ResourceInteractionRange { get; }
    string GatherBlacksmithInputPrefix { get; }
    string GatherBuildingMaterialPrefix { get; }
    string RareMiningPrefix { get; }
    string SupplyWorkstationPrefix { get; }
    string ReturnEmptyVesselPrefix { get; }
    string FillWaterJugPrefix { get; }
    string PullCartPrefix { get; }
    bool HasGuardianPlacementTask(string actor);
    OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    bool TryApplyWaitingRoutine(string id, AgentState state);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    List<CognitionCandidate> CreateCandidates(string agentId, AgentState state, bool restrictForOrder = true);
    bool IsWaitingSurvivalCandidate(string id);
    void ApplyCandidate(string agentId, AgentState state, string candidateId, bool reportIdle);
    bool IsAdult(string actor);
    bool ReadyForBriefInteraction(string actor);
    IEnumerable<AgentState> ChildrenNeedingCare(string actor);
    IEnumerable<string> IllDependentsNeedingCare(string adult);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange);
    void CareForChild(string actor, string childId, ActionPlan? originalPlan = null);
    void TendToIllDependent(string adult, string dependent, ActionPlan? originalPlan = null);
    bool IsTownProjectCandidate(string id);
    AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor);
    string ExplorationGoalCandidateId(ExplorationGoal goal);

}
