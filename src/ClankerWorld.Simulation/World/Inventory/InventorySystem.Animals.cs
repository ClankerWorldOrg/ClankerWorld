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
}
