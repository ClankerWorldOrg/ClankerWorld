using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IInventoryRuntimeWorld
{
    long WorldTick { get; }
    HashSet<string> PerishableKinds { get; }
    IReadOnlyDictionary<string, int> FoodSpoilageRates { get; }
    string ProductionOutputLotId(string jobId, int outputIndex);
    string ProductionVesselUnitId(string outputLotId, int unitIndex);
    InventoryCheckpoint ApplyToolWorkToInventory(InventoryCheckpoint inventory, string actor, long tick, IReadOnlyList<ToolWorkPlan> plans);
    string OrchardReplantingReservationId(string lotId);
}
