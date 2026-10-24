namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ITalkOrdersWorld TalkOrdersWorld => new TalkOrdersAdapter(this);

    private sealed class TalkOrdersAdapter(WorldRuntime runtime) : ITalkOrdersWorld
    {
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public bool IsLinkedTalkOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => WorldRuntime.IsLinkedTalkOrder(instruction);
        public global::ClankerWorld.Simulation.Cognition.AgentConversation? ConversationFor(string agentId) => runtime.ConversationFor(agentId);
        public string? TalkOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.TalkOrderBlocker(instruction, person);
        public string? CurrentMarriageOrderPartner(string actor) => OrderSystem.CurrentMarriageOrderPartner(runtime.MarriageOrdersWorld, actor);
        public string? MarriageOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.MarriageOrderBlocker(runtime.MarriageOrdersWorld, instruction);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor) => TransportSystem.PassengerBoat(runtime.BoatWorld, actor);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person) => runtime.NeedsUrgentWarmth(person);
        public bool HasExplicitConversationProvider(string agentId) => runtime.HasExplicitConversationProvider(agentId);
        public bool HasConversationAllowance(string agentId) => runtime.HasConversationAllowance(agentId);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.Cognition.AgentConversation> Conversations => runtime.conversations;
        public bool ActiveTalkOrderUses(string conversationId) => runtime.ActiveTalkOrderUses(conversationId);
        public bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public void CompleteClosedTalkOrders() => runtime.CompleteClosedTalkOrders();
        public string? UnacceptedMarriageOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.UnacceptedMarriageOrderBlocker(runtime.MarriageOrdersWorld, instruction);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public global::ClankerWorld.Simulation.Cognition.AgentConversation? ConversationForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.ConversationForOrder(runtime.MarriageOrdersWorld, instruction);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.AgentMarriage> Marriages => runtime.marriages;
        public OrderStore Orders => runtime.orderStore;
        public void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public bool ProposeConversation(string initiatorId, string inviteeId) => runtime.ProposeConversation(initiatorId, inviteeId);
        public bool IsSurvivalCandidate(string actor, string candidateId) => runtime.IsSurvivalCandidate(actor, candidateId);
        public bool IsMarketFoodCandidate(string actor, string candidateId) => MarketSystem.IsMarketFoodCandidate(runtime.MarketStallWorld, actor, candidateId);
        public bool ApplyMarketCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string id, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => runtime.ApplyMarketCandidate(actor, state, id, originalPlan);
        public void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle) => runtime.ApplyCandidate(agentId, state, candidateId, reportIdle);
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> ConversationCandidates(string agentId) => runtime.ConversationCandidates(agentId);
        public bool ApplyConversationCandidate(string agentId, string candidateId) => runtime.ApplyConversationCandidate(agentId, candidateId);
        public void RefreshMarriageOrderEligibilityStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.RefreshMarriageOrderEligibilityStatus(runtime.MarriageOrdersWorld, instruction);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public string? MarriageOrderOutcome(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation, global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.AgentMarriage> marriages) => WorldRuntime.MarriageOrderOutcome(conversation, marriages);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string TalkOrderEffectId(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => WorldRuntime.TalkOrderEffectId(instruction);
    }
}
