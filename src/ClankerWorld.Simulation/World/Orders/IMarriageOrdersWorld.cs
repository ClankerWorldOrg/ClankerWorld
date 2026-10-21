namespace ClankerWorld.Simulation.World;

internal interface IMarriageOrdersWorld
{
    bool IsAdult(string actor);
    string? CurrentMarriageOrderPartner(string actor);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.AgentMarriage> Marriages { get; }
    string? MarriageOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    OrderStore Orders { get; }
    bool IsActiveOrder(string status);
    string? UnacceptedMarriageOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    global::ClankerWorld.Simulation.Cognition.AgentConversation? ConversationForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.Cognition.AgentConversation> Conversations { get; }
}
