using System.Buffers;
using System.Collections.Frozen;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using FoodHarvestEffect = ClankerWorld.Simulation.World.OrderSystem.FoodHarvestEffect;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryRuntimeWorld InventoryRuntimeWorld => new InventoryRuntimeAdapter(this);
    private sealed class InventoryRuntimeAdapter(WorldRuntime runtime) : IInventoryRuntimeWorld
    {
        public string ProductionOutputLotId(string jobId, int outputIndex) => WorldRuntime.ProductionOutputLotId(jobId, outputIndex);
        public string ProductionVesselUnitId(string outputLotId, int unitIndex) => WorldRuntime.ProductionVesselUnitId(outputLotId, unitIndex);
        public InventoryCheckpoint ApplyToolWorkToInventory(InventoryCheckpoint inventory, string actor, long tick, IReadOnlyList<ToolWorkPlan> plans) => WorldRuntime.ApplyToolWorkToInventory(inventory, actor, tick, plans);
        public long WorldTick => runtime.WorldTick;
        public HashSet<string> PerishableKinds => WorldRuntime.PerishableKinds;
        public IReadOnlyDictionary<string, int> FoodSpoilageRates => WorldRuntime.FoodSpoilageRates;
        public string OrchardReplantingReservationId(string lotId) => WorldRuntime.OrchardReplantingReservationId(lotId);
    }
}
