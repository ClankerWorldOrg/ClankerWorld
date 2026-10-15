using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryRuntimeWorld InventoryRuntimeWorld => new InventoryRuntimeAdapter(this);
    private sealed class InventoryRuntimeAdapter(WorldRuntime runtime) : IInventoryRuntimeWorld
    {
        public long WorldTick => runtime.WorldTick;
        public HashSet<string> PerishableKinds => WorldRuntime.PerishableKinds;
        public IReadOnlyDictionary<string, int> FoodSpoilageRates => WorldRuntime.FoodSpoilageRates;
        public string ProductionOutputLotId(string jobId, int outputIndex) => WorldRuntime.ProductionOutputLotId(jobId, outputIndex);
        public string ProductionVesselUnitId(string outputLotId, int unitIndex) => WorldRuntime.ProductionVesselUnitId(outputLotId, unitIndex);
        public InventoryCheckpoint ApplyToolWorkToInventory(InventoryCheckpoint inventory, string actor, long tick, IReadOnlyList<ToolWorkPlan> plans) => WorldRuntime.ApplyToolWorkToInventory(inventory, actor, tick, plans);
        public string OrchardReplantingReservationId(string lotId) => WorldRuntime.OrchardReplantingReservationId(lotId);
    }
}
