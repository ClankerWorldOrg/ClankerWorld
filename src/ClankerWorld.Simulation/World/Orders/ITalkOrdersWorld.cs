using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface ITalkOrdersWorld
{
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    ReadOnlyCollection<AgentConversation> Conversations { get; }
    int ResourceInteractionRange { get; }
    ReadOnlyCollection<AgentMarriage> Marriages { get; }
    OrderStore Orders { get; }
    bool IsConversationOrder(string action);
    bool IsLinkedTalkOrder(OwnerQueuedInstruction instruction);
    ClankerWorld.Simulation.Cognition.AgentConversation? ConversationFor(string agentId);
    string? CurrentMarriageOrderPartner(string actor);
    string? MarriageOrderBlocker(OwnerQueuedInstruction instruction);
    ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    bool HasExplicitConversationProvider(string agentId);
    bool HasConversationAllowance(string agentId);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange);
    System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0);
    string? UnacceptedMarriageOrderBlocker(OwnerQueuedInstruction instruction);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    ClankerWorld.Simulation.Cognition.AgentConversation? ConversationForOrder(OwnerQueuedInstruction instruction);
    void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    bool ProposeConversation(string initiatorId, string inviteeId);
    bool IsSurvivalCandidate(string actor, string candidateId);
    bool IsMarketFoodCandidate(string actor, string candidateId);
    bool ApplyMarketCandidate(string actor, AgentState state, string id, ActionPlan? originalPlan = null);
    void ApplyCandidate(string agentId, AgentState state, string candidateId, bool reportIdle);
    System.Collections.Generic.List<ClankerWorld.Simulation.Cognition.CognitionCandidate> ConversationCandidates(string agentId);
    bool ApplyConversationCandidate(string agentId, string candidateId);
    void RefreshMarriageOrderEligibilityStatus(OwnerQueuedInstruction instruction);
    bool IsActiveOrder(string status);
    string? MarriageOrderOutcome(AgentConversation conversation, IEnumerable<AgentMarriage> marriages);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    string TalkOrderEffectId(OwnerQueuedInstruction instruction);
}
