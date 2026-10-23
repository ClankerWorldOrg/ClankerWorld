using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryFamilyWorld InventoryFamilyWorld => new InventoryFamilyAdapter(this);
    private sealed class InventoryFamilyAdapter(WorldRuntime runtime) : IInventoryFamilyWorld
    {
        public string MedicalDosePurpose(string caregiver, string patient, string kind, string owner, long startedTick) => WorldRuntime.MedicalDosePurpose(caregiver, patient, kind, owner, startedTick);
        public long WorldTick => runtime.WorldTick;
        public InventoryCheckpoint MarkBuildingMaterialDelivery(InventoryCheckpoint inventory, string lotId, string? deliveryBuildingId) => WorldRuntime.MarkBuildingMaterialDelivery(inventory, lotId, deliveryBuildingId);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
    }
}
