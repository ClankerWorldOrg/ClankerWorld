using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface ICivicWorld
{
    ActorCapabilityState? CapabilitiesFor(string actor);
    void RefuseChoice(ActionPlan plan, ActionRefusal refusal);
    long WorldTick { get; }
    bool HasSurvivalState { get; }
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    bool IsEdibleFood(string kind);
    int AvailableLotQuantity(InventoryLot lot);
    bool NeedsUrgentWarmth(AgentState person);
    void AppendEvent(string kind, string detail);
}
