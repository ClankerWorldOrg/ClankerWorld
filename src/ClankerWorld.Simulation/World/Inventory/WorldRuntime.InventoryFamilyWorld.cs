using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryFamilyWorld InventoryFamilyWorld => new InventoryFamilyAdapter(this);
    private sealed class InventoryFamilyAdapter(WorldRuntime runtime) : IInventoryFamilyWorld
    {
        public long WorldTick => runtime.WorldTick;
        public string MedicalDosePurpose(string caregiver, string patient, string kind, string owner, long startedTick) => WorldRuntime.MedicalDosePurpose(caregiver, patient, kind, owner, startedTick);
        public InventoryCheckpoint MarkBuildingMaterialDelivery(InventoryCheckpoint inventory, string lotId, string? deliveryBuildingId) => WorldRuntime.MarkBuildingMaterialDelivery(inventory, lotId, deliveryBuildingId);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
    }
}
