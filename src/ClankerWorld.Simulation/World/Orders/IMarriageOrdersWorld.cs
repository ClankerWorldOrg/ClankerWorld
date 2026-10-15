using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IMarriageOrdersWorld
{
    SocietyCheckpoint Society { get; }
    string MarriageAdultBlocker { get; }
    string MarriagePartnerBlocker { get; }
    string MarriageChangedPartnerBlocker { get; }
    ReadOnlyCollection<AgentMarriage> Marriages { get; }
    string MarriageEligibilityBlocker { get; }
    OrderStore Orders { get; }
    ReadOnlyCollection<AgentConversation> Conversations { get; }
    bool IsAdult(string actor);
    bool IsActiveOrder(string status);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
}
