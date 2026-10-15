using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command ConsumeMedicalDose(IInventoryFamilyWorld commandContext, string id, InventoryLot supply, string caregiver, string patient, string kind) => new PreparedCommand(inventory => InventoryAuthority.ConsumeReservation(InventoryAuthority.Reserve(inventory, id, supply.OwnerId, supply.Id, 1, commandContext.MedicalDosePurpose(caregiver, patient, kind, supply.OwnerId, commandContext.WorldTick), commandContext.WorldTick, holdAccess: InventoryHoldRules.BorrowedBy(caregiver)), id));

    internal static Command CloseMedicalDoseReceipt(string reservation, string purpose) => new PreparedCommand(inventory =>
{
    var receipt = inventory.GetReservation(reservation);
    if (receipt.State != InventoryReservationState.Completed || receipt.Purpose != purpose)
        throw new InvalidDataException("Only the original consumed medical dose can be closed.");
    return InventoryStore.PrepareReservationLedger(inventory, inventory.Reservations.Select(item => item.Id == receipt.Id ? InventoryStore.PrepareReservationPurpose(item, purpose + ":closed") : item).ToArray());
});

    internal static Command DeliverChildLoad(IInventoryFamilyWorld commandContext, ChildActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory, $"child-delivery:{commandContext.WorldTick}:{plan.Actor}", plan.Actor, plan.HouseholdId!, plan.StockId!, plan.Quantity, "child_household_delivery", plan.HouseId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId)));

    internal static Command CollectChildLoad(IInventoryFamilyWorld commandContext, ChildActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory, $"child-pickup:{commandContext.WorldTick}:{plan.Actor}", plan.HouseholdId!, plan.Actor, plan.StockId!, plan.Quantity, "child_household_pickup", destinationDeliveryBuildingId: plan.HouseId, holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));

    internal static Command GatherChildLoad(IInventoryFamilyWorld commandContext, ChildActionPlan plan, OrderSystem.FoodHarvestEffect food) => new PreparedCommand(inventory => commandContext.MarkBuildingMaterialDelivery(inventory, food.LotId, plan.HouseId!));

    internal static Command ShareChildFood(IInventoryFamilyWorld commandContext, ChildActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory, $"child-help:{commandContext.WorldTick}:{plan.Actor}", plan.Actor, commandContext.HouseholdFor(plan.Actor), plan.StockId!, plan.Quantity, "child_household_help", plan.HouseId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId)));
}
