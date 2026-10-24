using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryFamilyWorld InventoryFamilyWorld => new InventoryFamilyAdapter(this);
    private sealed class InventoryFamilyAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IInventoryFamilyWorld
    {
        public string MedicalDosePurpose(string caregiver, string patient, string kind, string owner, long startedTick) => WorldRuntime.MedicalDosePurpose(caregiver, patient, kind, owner, startedTick);
        public InventoryCheckpoint MarkBuildingMaterialDelivery(InventoryCheckpoint inventory, string lotId, string? deliveryBuildingId) => WorldRuntime.MarkBuildingMaterialDelivery(inventory, lotId, deliveryBuildingId);
    }
}
