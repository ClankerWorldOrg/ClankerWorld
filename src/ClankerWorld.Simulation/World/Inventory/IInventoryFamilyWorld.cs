using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IInventoryFamilyWorld
{
    long WorldTick { get; }
    string MedicalDosePurpose(string caregiver, string patient, string kind, string owner, long startedTick);
    InventoryCheckpoint MarkBuildingMaterialDelivery(InventoryCheckpoint inventory, string lotId, string? deliveryBuildingId);
    string HouseholdFor(string actor);
}
