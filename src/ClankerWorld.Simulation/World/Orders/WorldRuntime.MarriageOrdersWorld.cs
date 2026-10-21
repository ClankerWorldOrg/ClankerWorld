namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IMarriageOrdersWorld MarriageOrdersWorld => new MarriageOrdersAdapter(this);

    private sealed class MarriageOrdersAdapter(WorldRuntime runtime) : IMarriageOrdersWorld
    {
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public string? CurrentMarriageOrderPartner(string actor) => runtime.CurrentMarriageOrderPartner(actor);
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.AgentMarriage> Marriages => runtime.marriages;
        public string? MarriageOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.MarriageOrderBlocker(instruction);
        public OrderStore Orders => runtime.orderStore;
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public string? UnacceptedMarriageOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.UnacceptedMarriageOrderBlocker(instruction);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public global::ClankerWorld.Simulation.Cognition.AgentConversation? ConversationForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.ConversationForOrder(instruction);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.Cognition.AgentConversation> Conversations => runtime.conversations;
    }
}
