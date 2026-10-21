using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command ReleaseTownProjectClaims(HashSet<string> claims) => new PreparedCommand(inventory =>
                {
                    foreach (var reservation in inventory.Reservations.Where(item => claims.Contains(item.Id) &&
                                 item.State is InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed).ToArray())
                        inventory = InventoryAuthority.ReleaseReservation(inventory, reservation.Id, "town_project_material_released");
                    return inventory;
                });

    internal static Command ApplyCheckedTownProjectDonation(IInventoryTownsWorld commandContext, string operation, string actor, WorldRuntime.TownProjectChoice choice, TownProjectDelivery delivery) => new PreparedCommand(inventory => commandContext.ReserveTownProjectDelivery(InventoryAuthority.Transfer(inventory,
                operation, actor, choice.Town.Id, choice.Lot!.Id, choice.Quantity, "town_project_donated",
                destinationGroundPosition: new(TownProjectRules.WorkSite(choice.Project!.Plan).X, TownProjectRules.WorkSite(choice.Project!.Plan).Y), holdAccess: InventoryHoldRules.BorrowedBy(actor)), choice.Town.Id, choice.Project, delivery));

    internal static Command PickUpTownProjectLoad(string operation, InventoryLot lot, WorldRuntime.TownProjectChoice choice, string actor) => new PreparedCommand(inventory => InventoryAuthority.Relocate(inventory, operation, lot.Id,
                choice.Town.Id, choice.Quantity, carrierId: actor, holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command DeliverTownProjectLoad(IInventoryTownsWorld commandContext, TownProjectDelivery delivery, WorldRuntime.TownProjectChoice choice, string actor) => new PreparedCommand(inventory => commandContext.ReserveTownProjectDelivery(InventoryAuthority.Relocate(inventory,
                TownRecordIds.DeliveryFinal(delivery.Id), delivery.LotId, choice.Town.Id, delivery.Quantity,
                groundPosition: new(TownProjectRules.WorkSite(choice.Project!.Plan).X, TownProjectRules.WorkSite(choice.Project!.Plan).Y), holdAccess: InventoryHoldRules.BorrowedBy(actor)), choice.Town.Id, choice.Project, delivery));

    internal static Command RecoverTownProjectLoad(string operation, InventoryLot lot, WorldRuntime.TownProjectChoice choice, string actor) => new PreparedCommand(inventory => InventoryAuthority.Relocate(inventory, operation, lot.Id,
                choice.Town.Id, choice.Quantity, carrierId: actor, holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command SetDownTownProjectLoad(string operation, InventoryLot lot, WorldRuntime.TownProjectChoice choice, AgentState state, string actor) => new PreparedCommand(inventory => InventoryAuthority.Relocate(inventory,
                    operation, lot.Id, choice.Town.Id, choice.Quantity,
                    groundPosition: new(state.Position.X, state.Position.Y), holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command ReturnTownProjectLoadToWarehouse(IInventoryTownsWorld commandContext, WorldRuntime.TownProjectChoice choice, PlacedBuilding warehouse, string actor) => new PreparedCommand(inventory => InventoryAuthority.Relocate(inventory,
                IdentityRules.PrefixedReference(choice.Id + ":", $"{commandContext.WorldTick}"), choice.Lot!.Id, choice.Town.Id, choice.Quantity,
                storageBuildingId: warehouse.InstanceId, holdAccess: InventoryHoldRules.DeliveryFor(actor, warehouse.InstanceId)));

    internal static Command CompletePaidTownProject(TownProject project) => new PreparedCommand(inventory =>
            {
                foreach (var delivery in project.Deliveries.Where(item => item.ReleasedTick is null &&
                             item.DeliveredTick is not null && item.ReservationId is not null))
                    inventory = InventoryAuthority.ConsumeReservation(inventory, delivery.ReservationId!);
                return inventory;
            });

    internal static Command TransferCaseProperty(InventoryCheckpoint inventory) => new PreparedCommand(_ => inventory);
}
