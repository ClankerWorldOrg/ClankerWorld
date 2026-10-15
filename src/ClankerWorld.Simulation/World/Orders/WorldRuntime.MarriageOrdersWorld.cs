using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IMarriageOrdersWorld MarriageOrdersWorld => new MarriageOrdersAdapter(this);

    private sealed class MarriageOrdersAdapter(WorldRuntime runtime) : IMarriageOrdersWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public string MarriageAdultBlocker => WorldRuntime.MarriageAdultBlocker;
        public string MarriagePartnerBlocker => WorldRuntime.MarriagePartnerBlocker;
        public string MarriageChangedPartnerBlocker => WorldRuntime.MarriageChangedPartnerBlocker;
        public ReadOnlyCollection<AgentMarriage> Marriages => runtime.marriages;
        public string MarriageEligibilityBlocker => WorldRuntime.MarriageEligibilityBlocker;
        public OrderStore Orders => runtime.orderStore;
        public ReadOnlyCollection<AgentConversation> Conversations => runtime.conversations;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
    }
}
