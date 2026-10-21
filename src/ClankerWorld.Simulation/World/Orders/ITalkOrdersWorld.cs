namespace ClankerWorld.Simulation.World;

internal interface ITalkOrdersWorld
{
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    bool IsLinkedTalkOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::ClankerWorld.Simulation.Cognition.AgentConversation? ConversationFor(string agentId);
    string? TalkOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string? CurrentMarriageOrderPartner(string actor);
    string? MarriageOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor);
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    bool HasExplicitConversationProvider(string agentId);
    bool HasConversationAllowance(string agentId);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.Cognition.AgentConversation> Conversations { get; }
    bool ActiveTalkOrderUses(string conversationId);
    bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange);
    int ResourceInteractionRange { get; }
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    void CompleteClosedTalkOrders();
    string? UnacceptedMarriageOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    global::ClankerWorld.Simulation.Cognition.AgentConversation? ConversationForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.AgentMarriage> Marriages { get; }
    OrderStore Orders { get; }
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    bool ProposeConversation(string initiatorId, string inviteeId);
    bool IsSurvivalCandidate(string actor, string candidateId);
    bool IsMarketFoodCandidate(string actor, string candidateId);
    bool ApplyMarketCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string id, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle);
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> ConversationCandidates(string agentId);
    bool ApplyConversationCandidate(string agentId, string candidateId);
    void RefreshMarriageOrderEligibilityStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool IsActiveOrder(string status);
    string? MarriageOrderOutcome(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation, global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.AgentMarriage> marriages);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string TalkOrderEffectId(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
}
