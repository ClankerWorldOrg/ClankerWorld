using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface ICivicWorld
{
    long WorldTick { get; }
    bool HasSurvivalState { get; }
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    bool IsEdibleFood(string kind);
    int AvailableLotQuantity(InventoryLot lot);
    bool NeedsUrgentWarmth(AgentState person);
    void AppendEvent(WorldEventPayload payload);
}
