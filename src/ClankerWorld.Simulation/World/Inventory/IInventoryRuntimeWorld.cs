using ClankerWorld.Simulation.Kernel;
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
