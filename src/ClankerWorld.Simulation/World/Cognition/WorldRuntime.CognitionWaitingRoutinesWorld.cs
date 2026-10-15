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
    private ICognitionWaitingRoutinesWorld CognitionWaitingRoutinesWorld => new CognitionWaitingRoutinesAdapter(this);

    private sealed class CognitionWaitingRoutinesAdapter(WorldRuntime runtime) : ICognitionWaitingRoutinesWorld
    {
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public FamilyTurnState FamilyTurn => runtime.guardianPlacementActions;
        public IReadOnlySet<string>? WithheldActions => runtime.withheldActions;
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public string GatherBlacksmithInputPrefix => WorldRuntime.GatherBlacksmithInputPrefix;
        public string GatherBuildingMaterialPrefix => WorldRuntime.GatherBuildingMaterialPrefix;
        public string RareMiningPrefix => WorldRuntime.RareMiningPrefix;
        public string SupplyWorkstationPrefix => WorldRuntime.SupplyWorkstationPrefix;
        public string ReturnEmptyVesselPrefix => WorldRuntime.ReturnEmptyVesselPrefix;
        public string FillWaterJugPrefix => WorldRuntime.FillWaterJugPrefix;
        public string PullCartPrefix => WorldRuntime.PullCartPrefix;
        public bool HasGuardianPlacementTask(string actor) => runtime.HasGuardianPlacementTask(actor);
        public OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public bool TryApplyWaitingRoutine(string id, AgentState state) => runtime.TryApplyWaitingRoutine(id, state);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);
        public List<CognitionCandidate> CreateCandidates(string agentId, AgentState state, bool restrictForOrder = true) => runtime.CreateCandidates(agentId, state, restrictForOrder);
        public bool IsWaitingSurvivalCandidate(string id) => WorldRuntime.IsWaitingSurvivalCandidate(id);
        public void ApplyCandidate(string agentId, AgentState state, string candidateId, bool reportIdle) => runtime.ApplyCandidate(agentId, state, candidateId, reportIdle);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public IEnumerable<AgentState> ChildrenNeedingCare(string actor) => runtime.ChildrenNeedingCare(actor);
        public IEnumerable<string> IllDependentsNeedingCare(string adult) => runtime.IllDependentsNeedingCare(adult);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public void CareForChild(string actor, string childId, ActionPlan? originalPlan = null) => runtime.CareForChild(actor, childId, originalPlan);
        public void TendToIllDependent(string adult, string dependent, ActionPlan? originalPlan = null) => runtime.TendToIllDependent(adult, dependent, originalPlan);
        public bool IsTownProjectCandidate(string id) => WorldRuntime.IsTownProjectCandidate(id);
        public AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor) => runtime.KnowledgeWritingFor(actor);
        public string ExplorationGoalCandidateId(ExplorationGoal goal) => WorldRuntime.ExplorationGoalCandidateId(goal);

    }
}
