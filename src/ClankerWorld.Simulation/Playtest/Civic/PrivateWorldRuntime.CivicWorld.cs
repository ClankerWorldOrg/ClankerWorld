using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private sealed partial class CivicWorldAdapter(PrivateWorldRuntime runtime) : ICivicTownWorld
    {
        public long WorldTick => runtime.WorldTick;
        public bool HasSurvivalState => runtime.survivalState is not null;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, PlaytestInhabitantState> Inhabitants => runtime.inhabitants;
        public bool IsEdibleFood(string kind) => PrivateWorldRuntime.IsEdibleFood(kind);
        public int AvailableLotQuantity(InventoryLot lot) => runtime.AvailableLotQuantity(lot);
        public bool NeedsUrgentWarmth(PlaytestInhabitantState person) => runtime.NeedsUrgentWarmth(person);
        public void AppendEvent(string kind, string detail) => runtime.AppendEvent(kind, detail);
    }
}
