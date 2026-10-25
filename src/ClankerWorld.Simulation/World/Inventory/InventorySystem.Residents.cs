using ClankerWorld.Simulation.Kernel;
using static ClankerWorld.Simulation.World.WorldRuntime.AnimalSharedRules;

namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command PickUpAnimalSupply(string operation, InventoryLot source, AnimalSupplyActionPlan plan) => new PreparedCommand(current => InventoryAuthority.Relocate(current, operation, source.Id, source.OwnerId,
                    plan.Quantity, carrierId: plan.Actor, holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));

    internal static Command HoldAnimalSupplyLoad(InventoryLot source, AnimalSupplyActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.HoldStock(inventory, source.Id,
                InventoryStore.CreateInventoryHold(InventoryHoldPurpose.SupplyTrip, plan.Actor)));

    internal static Command CompleteAnimalTripDelivery(InventoryLot lot, AnimalTripActionPlan plan, IAnimalWorld world) => new PreparedCommand(current => lot.OwnerId == plan.HouseholdId ? InventoryAuthority.Relocate(current,
                IdentityRules.PrefixedReference("animal-deliver-", $"{world.WorldTick}-{world.NextEventId}"), lot.Id, lot.OwnerId, plan.Quantity, storageBuildingId: plan.Trip.YardId, holdAccess: InventoryHoldRules.SupplyTripFor(plan.Actor)) :
                InventoryAuthority.Transfer(current, IdentityRules.PrefixedReference("animal-donate-", $"{world.WorldTick}-{world.NextEventId}"), lot.OwnerId, plan.HouseholdId!,
                    lot.Id, plan.Quantity, "animal_yard_supplied", destinationStorageBuildingId: plan.Trip.YardId, holdAccess: InventoryHoldRules.SupplyTripFor(plan.Actor)));

    internal static Command FinishAnimalSupplyTrip(InventoryLot lot, InventoryHold hold) => new PreparedCommand(inventory => InventoryAuthority.ReleaseStock(inventory, lot.Id, hold));

    internal static Command ConsumeAnimalInputs(List<(InventoryLot Lot, int Quantity)> inputs, string actor, IAnimalWorld world, string purpose) => new PreparedCommand(inventory =>
            {
                var next = inventory;
                for (var index = 0; index < inputs.Count; index++)
                {
                    var (lot, quantity) = inputs[index];
                    var id = AnimalInputReservationId(actor, world.WorldTick, world.NextEventId, purpose, index);
                    next = InventoryAuthority.ConsumeReservation(InventoryAuthority.Reserve(next, id, lot.OwnerId, lot.Id,
                        quantity, purpose, world.WorldTick, holdAccess: InventoryHoldRules.SupplyTripFor(actor)), id);
                }
                return next;
            });

    internal static Command CompleteAnimalProductCollection(IAnimalWorld world, InventoryLot product, string actor) => new PreparedCommand(current => InventoryAuthority.Relocate(current, IdentityRules.PrefixedReference("animal-collect-", $"{world.WorldTick}-{world.NextEventId}"),
                product.Id, product.OwnerId, product.Quantity, carrierId: actor, holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command CompleteAnimalHideCollection(string actor, AnimalState animal, IAnimalWorld world, InventoryLot hide) => new PreparedCommand(inventory => InventoryAuthority.Relocate(InventoryAuthority.Transfer(inventory,
                AnimalHideTransferId(actor, animal.Id, world.WorldTick, false), animal.Id, actor,
                hide.Id, 1, "wild-animal-hide", destinationGroundPosition: hide.GroundPosition, holdAccess: InventoryHoldRules.BorrowedBy(actor)),
                AnimalHideTransferId(actor, animal.Id, world.WorldTick, true), hide.Id, actor, 1, carrierId: actor, holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command ReleaseDeadAnimalSaddle(Identity saddleHeld) => new PreparedCommand(inventory => InventoryAuthority.ReleaseReservation(inventory, saddleHeld, "animal_died"));

    internal static Command CreateAnimalHide(AnimalState animal, long tick) => new PreparedCommand(inventory => InventoryAuthority.AddLot(inventory,
                            AnimalHideId(animal.Id), "hide", animal.HouseholdId ?? animal.Id, 1, tick,
                            groundPosition: new(animal.Position.X, animal.Position.Y)));

    internal static Command CreateReservedAnimalProduct(string productId, AnimalSpecies definition, AnimalState animal, long tick, string reservationId) => new PreparedCommand(inventory => InventoryAuthority.Reserve(InventoryAuthority.AddLot(inventory,
                            productId, definition.Product!, animal.HouseholdId!, definition.ProductQuantity, tick,
                            groundPosition: new(animal.Position.X, animal.Position.Y)), reservationId,
                            animal.HouseholdId!, productId, definition.ProductQuantity, "animal-product:" + animal.Id, long.MaxValue));

    internal static Command ReleaseAnimalProductClaim(Identity reservationId) => new PreparedCommand(inventory => InventoryAuthority.ReleaseReservation(inventory, reservationId, "animal_product_released"));

    internal static Command DiscardOldAnimalProduct(InventoryLot lot) => new PreparedCommand(inventory => InventoryAuthority.Discard(inventory, lot.OwnerId, lot.Id, lot.Quantity));

    internal static Command CompleteAnimalSaddling(string operation, InventoryLot saddle, AnimalState animal, string actor, string reservationId, string unitId) => new PreparedCommand(inventory => InventoryAuthority.Reserve(InventoryAuthority.Relocate(inventory, operation,
                saddle.Id, saddle.OwnerId, 1, groundPosition: new(animal.Position.X, animal.Position.Y), holdAccess: InventoryHoldRules.SupplyTripFor(actor)), reservationId,
                saddle.OwnerId, unitId, 1, "animal-saddle:" + animal.Id, long.MaxValue, holdAccess: InventoryHoldRules.SupplyTripFor(actor)));

    internal static Command CompleteAnimalDismount(PlannedAnimalDismount plan, IAnimalWorld world, PlannedAnimalDrop cargo) => new PreparedCommand(current => InventoryAuthority.Relocate(current,
                    AnimalCargoDismountId(plan.Actor, world.WorldTick, world.NextEventId, cargo.StockId),
                    cargo.StockId, cargo.OwnerId, cargo.Quantity, groundPosition: new(plan.Position.X, plan.Position.Y),
                    holdAccess: InventoryHoldRules.SupplyTripFor(plan.Actor)));

    internal static Command MoveSaddleWithAnimal(string saddle, Identity held, AnimalState animal, GridPoint next, IAnimalWorld world) => new PreparedCommand(inventory => InventoryAuthority.MoveAnimalSaddle(inventory, saddle, held, animal.Id,
                    new(animal.Position.X, animal.Position.Y), new(next.X, next.Y), world.Map.WrapsEastWest ? world.Map.Width : 0));

    internal static Command MoveProductWithAnimal(string product, Identity productHeld, AnimalState animal, GridPoint next, IAnimalWorld world) => new PreparedCommand(inventory => InventoryAuthority.MoveAnimalProduct(inventory, product, productHeld, animal.Id,
                    new(animal.Position.X, animal.Position.Y), new(next.X, next.Y), world.Map.WrapsEastWest ? world.Map.Width : 0));

    internal static Command PayForAnimalTransfer(AnimalTradeOffer offer, string actor, AnimalState animal, string payment) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory, DerivedId.Create(offer.Id + "-payment"), actor,
                            animal.HouseholdId!, payment, offer.PaymentQuantity, "animal_sale_payment",
                            destinationGroundPosition: new(animal.Position.X, animal.Position.Y)));

    internal static Command ReleaseTransferredSaddle(Identity saddleHeld) => new PreparedCommand(inventory => InventoryAuthority.ReleaseReservation(inventory, saddleHeld, "animal_transferred"));
    internal static Command ConsumeMedicalDose(IInventoryFamilyWorld commandContext, string id, InventoryLot supply, string caregiver, string patient, string kind) => new PreparedCommand(inventory =>
                InventoryAuthority.ConsumeReservation(InventoryAuthority.Reserve(inventory, id, supply.OwnerId, supply.Id, 1,
                    commandContext.MedicalDosePurpose(caregiver, patient, kind, supply.OwnerId, commandContext.WorldTick), commandContext.WorldTick, holdAccess: InventoryHoldRules.BorrowedBy(caregiver)), id));

    internal static Command CloseMedicalDoseReceipt(Identity reservation, string purpose) => new PreparedCommand(inventory =>
            {
                var receipt = inventory.GetReservation(reservation);
                if (receipt.State != InventoryReservationState.Completed || receipt.Purpose != purpose)
                    throw new InvalidDataException("Only the original consumed medical dose can be closed.");
                return InventoryStore.PrepareReservationLedger(inventory, inventory.Reservations.Select(item => item.Id == receipt.Id ? InventoryStore.PrepareReservationPurpose(item, purpose + ":closed") : item).ToArray());
            });

    internal static Command DeliverChildLoad(IInventoryFamilyWorld commandContext, ChildActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                IdentityRules.PrefixedReference("child-delivery:", $"{commandContext.WorldTick}:{plan.Actor}"), plan.Actor, plan.HouseholdId!, plan.StockId!, plan.Quantity, "child_household_delivery", plan.HouseId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId)));

    internal static Command CollectChildLoad(IInventoryFamilyWorld commandContext, ChildActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                IdentityRules.PrefixedReference("child-pickup:", $"{commandContext.WorldTick}:{plan.Actor}"), plan.HouseholdId!, plan.Actor, plan.StockId!, plan.Quantity, "child_household_pickup", destinationDeliveryBuildingId: plan.HouseId, holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));

    internal static Command GatherChildLoad(IInventoryFamilyWorld commandContext, OrderSystem.FoodHarvestEffect food, ChildActionPlan plan) => new PreparedCommand(inventory => commandContext.MarkBuildingMaterialDelivery(inventory, food.LotId, plan.HouseId!));

    internal static Command ShareChildFood(IInventoryFamilyWorld commandContext, ChildActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                DerivedId.Create($"child-help:{commandContext.WorldTick}:{plan.Actor}"), plan.Actor, commandContext.HouseholdFor(plan.Actor), plan.StockId!, plan.Quantity, "child_household_help", plan.HouseId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId)));
    internal static Command MovePersonalGoods(PersonalGoodsActionPlan plan, string moveId) => new PreparedCommand(inventory =>
                plan.Choice.Kind == PersonalGoodsKind.Collect
                    ? InventoryAuthority.Relocate(inventory, moveId, plan.Choice.LotId, plan.OwnerId, plan.Quantity, plan.Actor,
                        holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor))
                    : InventoryAuthority.Relocate(inventory, moveId, plan.Choice.LotId, plan.OwnerId, plan.Quantity,
                        storageBuildingId: plan.HouseId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId)));

    internal static Command SetReservationDeadline(IReadOnlyList<Identity> ids, long deadline) => new PreparedCommand(inventory => InventoryAuthority.SetReservationDeadline(inventory, ids, deadline));

    internal static Command ReleaseUnavailablePausedInputs(IReadOnlyList<Identity> ids) => new PreparedCommand(inventory =>
            {
                foreach (var id in ids)
                    if (inventory.Reservations.SingleOrDefault(item => item.Id == id) is { State: InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed })
                        inventory = InventoryAuthority.ReleaseReservation(inventory, id, "paused_work_materials_unavailable");
                return inventory;
            });

    internal static Command CommitDepartureAllowance(InventoryCheckpoint inventory) => new PreparedCommand(_ => inventory);

    internal static Command ReleaseDepartingFarmInputs(WorldProductionJob job) => new PreparedCommand(inventory =>
                    {
                        foreach (var id in job.InputReservationIds)
                            if (inventory.Reservations.SingleOrDefault(item => item.Id == id) is { State: InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed })
                                inventory = InventoryAuthority.ReleaseReservation(inventory, id, "personal_work_stopped_on_leaving");
                        return inventory;
                    });

    internal static Command HoldDepartingProjectInputs(Identity[] heldReservations) => new PreparedCommand(inventory => InventoryAuthority.HoldReservations(inventory, heldReservations));

    internal static Command RestoreDeliveryOwnership(IInventoryCommunityWorld commandContext, string transition, string actor, InventoryLot lot, string householdId) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                    IdentityRules.PrefixedReference($"{transition}-delivery:", $"{actor}:{commandContext.WorldTick}:{lot.Id}"), actor, householdId, lot.Id, lot.Quantity,
                    "delivery_ownership_restored", holdAccess: InventoryHoldRules.DeliveryFor(actor, lot.DeliveryBuildingId)));

    internal static Command RestoreDeliveryCustody(IInventoryCommunityWorld commandContext, string transition, string actor, InventoryLot lot, string householdId, string carrier) => new PreparedCommand(inventory => InventoryAuthority.Relocate(inventory,
                    IdentityRules.PrefixedReference($"{transition}-custody:", $"{actor}:{commandContext.WorldTick}:{lot.Id}"), lot.Id, householdId, lot.Quantity, carrier, holdAccess: InventoryHoldRules.BorrowedBy(actor)));
    internal static Command ApplyTownSalvagePlan(ITownWorld world, TownStockActionPlan plan) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory,
                WorldRuntime.CivicSharedRules.TownSalvageOperationId(world.WorldTick, plan.Actor, plan.CandidateId),
                plan.Choice.TownId, plan.Actor, plan.Choice.StockId, plan.Quantity,
                "town_stock_salvaged", holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));

    internal static Command ApplyCheckedNonLandWork(string operation, string actor, TownRemedyTerm term, InventoryLot lot, int remaining) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory, operation, actor,
                        term.BeneficiaryId!, lot.Id, remaining, "voluntary_restitution", holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command DeliverNonLandTownGoods(string operation, string actor, PlacedBuilding warehouse, InventoryLot lot, int quantity) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory, operation, actor, warehouse.TownId!,
                lot.Id, quantity, "town_resources_stored", warehouse.InstanceId, holdAccess: InventoryHoldRules.DeliveryFor(actor, warehouse.InstanceId)));

    internal static Command AcceptFirstTownLayout(InventoryCheckpoint starterInventory) => new PreparedCommand(_ => starterInventory);

    internal static Command CollectLandSalePayment(ITownWorld world, string actor, TownLandTransferRequest request, PlannedEquipmentPickup pickup, LandSaleCollectionActionPlan? plan) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory, IdentityRules.PrefixedReference("land-sale-collect:", $"{world.WorldTick}:{actor}:{request.Id}"),
                pickup.OwnerId, actor, pickup.StockId, plan!.Quantity, "land_sale_payment_collected", holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command SettleLandSale(InventoryCheckpoint inventory) => new PreparedCommand(_ => inventory);
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
