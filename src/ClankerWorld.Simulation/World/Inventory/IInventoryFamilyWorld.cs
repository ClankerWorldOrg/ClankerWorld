using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

internal interface IInventoryFamilyWorld
{
    string MedicalDosePurpose(string caregiver, string patient, string kind, string owner, long startedTick);
    long WorldTick { get; }
    InventoryCheckpoint MarkBuildingMaterialDelivery(InventoryCheckpoint inventory, string lotId, string? deliveryBuildingId);
    string HouseholdFor(string actor);
}
