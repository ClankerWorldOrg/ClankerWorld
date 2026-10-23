using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

internal interface IInventoryConstructionWorld
{
    InventoryCheckpoint ReserveExpansionInputs(InventoryCheckpoint inventory, IReadOnlyList<ExpansionMaterialInput> inputs, string actor, string jobId, long completion, out IReadOnlyList<string> reservationIds);
    InventoryCheckpoint ConsumeConstructionInputs(InventoryCheckpoint inventory, IReadOnlyList<ConstructionMaterialInput> inputs, string purpose, string owner);
}
