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

internal interface IInventoryRuntimeWorld
{
    string ProductionOutputLotId(string jobId, int outputIndex);
    string ProductionVesselUnitId(string outputLotId, int unitIndex);
    InventoryCheckpoint ApplyToolWorkToInventory(InventoryCheckpoint inventory, string actor, long tick, IReadOnlyList<ToolWorkPlan> plans);
    long WorldTick { get; }
    HashSet<string> PerishableKinds { get; }
    IReadOnlyDictionary<string, int> FoodSpoilageRates { get; }
    string OrchardReplantingReservationId(string lotId);
}
