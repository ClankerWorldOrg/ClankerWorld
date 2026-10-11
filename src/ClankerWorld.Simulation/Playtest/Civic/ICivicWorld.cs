using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Playtest;

internal interface ICivicWorld
{
    long WorldTick { get; }
    bool HasSurvivalState { get; }
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, PlaytestInhabitantState> Inhabitants { get; }
    bool IsEdibleFood(string kind);
    int AvailableLotQuantity(InventoryLot lot);
    bool NeedsUrgentWarmth(PlaytestInhabitantState person);
    void AppendEvent(string kind, string detail);
}
