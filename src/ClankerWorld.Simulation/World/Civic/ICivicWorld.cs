using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface ICivicWorld
{
    long WorldTick { get; }
    bool HasSurvivalState { get; }
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, InhabitantState> Inhabitants { get; }
    bool IsEdibleFood(string kind);
    int AvailableLotQuantity(InventoryLot lot);
    bool NeedsUrgentWarmth(InhabitantState person);
    void AppendEvent(string kind, string detail);
}
