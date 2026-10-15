using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command PickUpAnimalSupply(AnimalSupplyActionPlan plan, InventoryLot source, string operation) => new PreparedCommand(current => InventoryAuthority.Relocate(current, operation, source.Id, source.OwnerId, plan.Quantity, carrierId: plan.Actor, holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));

    internal static Command HoldAnimalSupplyLoad(AnimalSupplyActionPlan plan, InventoryLot source) => new PreparedCommand(inventory => InventoryAuthority.HoldStock(inventory, source.Id, InventoryStore.CreateInventoryHold(InventoryHoldPurpose.SupplyTrip, plan.Actor)));

    internal static Command CompleteAnimalTripDelivery(IAnimalWorld world, AnimalTripActionPlan plan, InventoryLot lot) => new PreparedCommand(current => lot.OwnerId == plan.HouseholdId ? InventoryAuthority.Relocate(current, "animal-deliver-" + world.WorldTick + "-" + world.NextEventId, lot.Id, lot.OwnerId, plan.Quantity, storageBuildingId: plan.Trip.YardId, holdAccess: InventoryHoldRules.SupplyTripFor(plan.Actor)) : InventoryAuthority.Transfer(current, "animal-donate-" + world.WorldTick + "-" + world.NextEventId, lot.OwnerId, plan.HouseholdId!, lot.Id, plan.Quantity, "animal_yard_supplied", destinationStorageBuildingId: plan.Trip.YardId, holdAccess: InventoryHoldRules.SupplyTripFor(plan.Actor)));

    internal static Command FinishAnimalSupplyTrip(InventoryHold hold, InventoryLot lot) => new PreparedCommand(inventory => InventoryAuthority.ReleaseStock(inventory, lot.Id, hold));

    internal static Command PayForAnimalTransfer(string actor, AnimalState animal, AnimalTradeOffer offer, string payment) => new PreparedCommand(inventory => InventoryAuthority.Transfer(inventory, DerivedId.Create(offer.Id + "-payment"), actor, animal.HouseholdId!, payment, offer.PaymentQuantity, "animal_sale_payment", destinationGroundPosition: new(animal.Position.X, animal.Position.Y)));

    internal static Command ReleaseTransferredSaddle(string saddleHeld) => new PreparedCommand(inventory => InventoryAuthority.ReleaseReservation(inventory, saddleHeld, "animal_transferred"));

    internal static Command ReleaseDeadAnimalSaddle(string saddleHeld) => new PreparedCommand(inventory => InventoryAuthority.ReleaseReservation(inventory, saddleHeld, "animal_died"));

    internal static Command CreateAnimalHide(long tick, AnimalState animal) => new PreparedCommand(inventory => InventoryAuthority.AddLot(inventory, WorldRuntime.AnimalSharedRules.AnimalHideId(animal.Id), "hide", animal.HouseholdId ?? animal.Id, 1, tick, groundPosition: new(animal.Position.X, animal.Position.Y)));

    internal static Command CreateReservedAnimalProduct(long tick, AnimalState animal, AnimalSpecies definition, string productId, string reservationId) => new PreparedCommand(inventory => InventoryAuthority.Reserve(InventoryAuthority.AddLot(inventory, productId, definition.Product!, animal.HouseholdId!, definition.ProductQuantity, tick, groundPosition: new(animal.Position.X, animal.Position.Y)), reservationId, animal.HouseholdId!, productId, definition.ProductQuantity, "animal-product:" + animal.Id, long.MaxValue));

    internal static Command ReleaseAnimalProductClaim(string reservationId) => new PreparedCommand(inventory => InventoryAuthority.ReleaseReservation(inventory, reservationId, "animal_product_released"));

    internal static Command DiscardOldAnimalProduct(InventoryLot lot) => new PreparedCommand(inventory => InventoryAuthority.Discard(inventory, lot.OwnerId, lot.Id, lot.Quantity));

    internal static Command CompleteAnimalSaddling(string actor, AnimalState animal, InventoryLot saddle, string operation, string unitId, string reservationId) => new PreparedCommand(inventory => InventoryAuthority.Reserve(InventoryAuthority.Relocate(inventory, operation, saddle.Id, saddle.OwnerId, 1, groundPosition: new(animal.Position.X, animal.Position.Y), holdAccess: InventoryHoldRules.SupplyTripFor(actor)), reservationId, saddle.OwnerId, unitId, 1, "animal-saddle:" + animal.Id, long.MaxValue, holdAccess: InventoryHoldRules.SupplyTripFor(actor)));

    internal static Command CompleteAnimalDismount(IAnimalWorld world, PlannedAnimalDismount plan, PlannedAnimalDrop cargo) => new PreparedCommand(current => InventoryAuthority.Relocate(current, WorldRuntime.AnimalSharedRules.AnimalCargoDismountId(plan.Actor, world.WorldTick, world.NextEventId, cargo.StockId), cargo.StockId, cargo.OwnerId, cargo.Quantity, groundPosition: new(plan.Position.X, plan.Position.Y), holdAccess: InventoryHoldRules.SupplyTripFor(plan.Actor)));

    internal static Command MoveSaddleWithAnimal(IAnimalWorld world, AnimalState animal, GridPoint next, string saddle, string held) => new PreparedCommand(inventory => InventoryAuthority.MoveAnimalSaddle(inventory, saddle, held, animal.Id, new(animal.Position.X, animal.Position.Y), new(next.X, next.Y), world.Map.WrapsEastWest ? world.Map.Width : 0));

    internal static Command MoveProductWithAnimal(IAnimalWorld world, AnimalState animal, GridPoint next, string product, string productHeld) => new PreparedCommand(inventory => InventoryAuthority.MoveAnimalProduct(inventory, product, productHeld, animal.Id, new(animal.Position.X, animal.Position.Y), new(next.X, next.Y), world.Map.WrapsEastWest ? world.Map.Width : 0));

    internal static Command ConsumeAnimalInputs(IAnimalWorld world, string actor, string purpose, List<(InventoryLot Lot, int Quantity)> inputs) => new PreparedCommand(inventory =>
{
    var next = inventory;
    for (var index = 0; index < inputs.Count; index++)
    {
        var (lot, quantity) = inputs[index];
        var id = WorldRuntime.AnimalSharedRules.AnimalInputReservationId(actor, world.WorldTick, world.NextEventId, purpose, index);
        next = InventoryAuthority.ConsumeReservation(InventoryAuthority.Reserve(next, id, lot.OwnerId, lot.Id, quantity, purpose, world.WorldTick, holdAccess: InventoryHoldRules.SupplyTripFor(actor)), id);
    }

    return next;
});

    internal static Command CompleteAnimalProductCollection(IAnimalWorld world, string actor, InventoryLot product) => new PreparedCommand(current => InventoryAuthority.Relocate(current, "animal-collect-" + world.WorldTick + "-" + world.NextEventId, product.Id, product.OwnerId, product.Quantity, carrierId: actor, holdAccess: InventoryHoldRules.BorrowedBy(actor)));

    internal static Command CompleteAnimalHideCollection(IAnimalWorld world, string actor, AnimalState animal, InventoryLot hide) => new PreparedCommand(inventory => InventoryAuthority.Relocate(InventoryAuthority.Transfer(inventory, WorldRuntime.AnimalSharedRules.AnimalHideTransferId(actor, animal.Id, world.WorldTick, false), animal.Id, actor, hide.Id, 1, "wild-animal-hide", destinationGroundPosition: hide.GroundPosition, holdAccess: InventoryHoldRules.BorrowedBy(actor)), WorldRuntime.AnimalSharedRules.AnimalHideTransferId(actor, animal.Id, world.WorldTick, true), hide.Id, actor, 1, carrierId: actor, holdAccess: InventoryHoldRules.BorrowedBy(actor)));
}
