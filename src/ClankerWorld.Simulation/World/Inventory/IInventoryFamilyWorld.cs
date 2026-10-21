using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Society;
namespace ClankerWorld.Simulation.World;

internal interface IInventoryFamilyWorld
{
    string MedicalDosePurpose(string caregiver, string patient, string kind, string owner, long startedTick);
    long WorldTick { get; }
    InventoryCheckpoint MarkBuildingMaterialDelivery(InventoryCheckpoint inventory, string lotId, string? deliveryBuildingId);
    string HouseholdFor(string actor);
}
