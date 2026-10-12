using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private sealed partial class CivicWorldAdapter(WorldRuntime runtime) : ICivicTownWorld
    {
        public long WorldTick => runtime.WorldTick;
        public bool HasSurvivalState => runtime.survivalState is not null;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, InhabitantState> Inhabitants => runtime.inhabitants;
        public bool IsEdibleFood(string kind) => WorldRuntime.IsEdibleFood(kind);
        public int AvailableLotQuantity(InventoryLot lot) => runtime.AvailableLotQuantity(lot);
        public bool NeedsUrgentWarmth(InhabitantState person) => runtime.NeedsUrgentWarmth(person);
        public void AppendEvent(string kind, string detail) => runtime.AppendEvent(kind, detail);
    }
}
