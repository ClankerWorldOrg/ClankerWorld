using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ITalkOrdersWorld TalkOrdersWorld => new TalkOrdersAdapter(this);

    private sealed class TalkOrdersAdapter(WorldRuntime runtime) : ITalkOrdersWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public ReadOnlyCollection<AgentConversation> Conversations => runtime.conversations;
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public ReadOnlyCollection<AgentMarriage> Marriages => runtime.marriages;
        public OrderStore Orders => runtime.orderStore;
        public bool IsConversationOrder(string action) => WorldRuntime.IsConversationOrder(action);
        public bool IsLinkedTalkOrder(OwnerQueuedInstruction instruction) => WorldRuntime.IsLinkedTalkOrder(instruction);
        public ClankerWorld.Simulation.Cognition.AgentConversation? ConversationFor(string agentId) => runtime.ConversationFor(agentId);
        public string? CurrentMarriageOrderPartner(string actor) => runtime.CurrentMarriageOrderPartner(actor);
        public string? MarriageOrderBlocker(OwnerQueuedInstruction instruction) => runtime.MarriageOrderBlocker(instruction);
        public ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor) => runtime.PassengerBoat(actor);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);
        public bool HasExplicitConversationProvider(string agentId) => runtime.HasExplicitConversationProvider(agentId);
        public bool HasConversationAllowance(string agentId) => runtime.HasConversationAllowance(agentId);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public string? UnacceptedMarriageOrderBlocker(OwnerQueuedInstruction instruction) => runtime.UnacceptedMarriageOrderBlocker(instruction);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public ClankerWorld.Simulation.Cognition.AgentConversation? ConversationForOrder(OwnerQueuedInstruction instruction) => runtime.ConversationForOrder(instruction);
        public void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public bool ProposeConversation(string initiatorId, string inviteeId) => runtime.ProposeConversation(initiatorId, inviteeId);
        public bool IsSurvivalCandidate(string actor, string candidateId) => runtime.IsSurvivalCandidate(actor, candidateId);
        public bool IsMarketFoodCandidate(string actor, string candidateId) => runtime.IsMarketFoodCandidate(actor, candidateId);
        public bool ApplyMarketCandidate(string actor, AgentState state, string id, ActionPlan? originalPlan = null) => runtime.ApplyMarketCandidate(actor, state, id, originalPlan);
        public void ApplyCandidate(string agentId, AgentState state, string candidateId, bool reportIdle) => runtime.ApplyCandidate(agentId, state, candidateId, reportIdle);
        public System.Collections.Generic.List<ClankerWorld.Simulation.Cognition.CognitionCandidate> ConversationCandidates(string agentId) => runtime.ConversationCandidates(agentId);
        public bool ApplyConversationCandidate(string agentId, string candidateId) => runtime.ApplyConversationCandidate(agentId, candidateId);
        public void RefreshMarriageOrderEligibilityStatus(OwnerQueuedInstruction instruction) => runtime.RefreshMarriageOrderEligibilityStatus(instruction);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public string? MarriageOrderOutcome(AgentConversation conversation, IEnumerable<AgentMarriage> marriages) => WorldRuntime.MarriageOrderOutcome(conversation, marriages);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string TalkOrderEffectId(OwnerQueuedInstruction instruction) => WorldRuntime.TalkOrderEffectId(instruction);
    }
}
