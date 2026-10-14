using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

using static ClankerWorld.Simulation.World.WorldRuntime.AnimalSharedRules;

namespace ClankerWorld.Simulation.World;

internal sealed partial class AnimalSystem
{
    internal AnimalState? RidingAnimal(IAnimalWorld world, string actor) => State.Animals.FirstOrDefault(animal => animal.RiderId == actor &&
        MayRideAnimal(world, actor, animal) && AnimalRules.HasCare(animal, world.WorldTick) && animal.SaddleLotId is not null);
    internal int HorseCargoCapacity(IAnimalWorld world, string actor) => RidingAnimal(world, actor) is null ? 0 : AnimalRules.RidingCargo;

    internal void SaddleAnimal(IAnimalWorld world, string actor, AnimalState animal)
    {
        if (!AnimalHouseholdMember(world, actor, animal) || animal.Species != "horse" || animal.SaddleLotId is not null ||
            world.Agents[actor].Position != animal.Position) return;
        var saddle = AnimalSuppliesAtHand(world, actor, animal).FirstOrDefault(lot => lot.ItemKind == "saddle" &&
            lot.OwnerId == animal.HouseholdId && lot.ContainerLotId is null);
        if (saddle is null) return;
        var operation = AnimalSaddleOperationId(animal.Id, world.WorldTick, world.NextEventId);
        var unitId = saddle.Quantity == 1 ? saddle.Id : DerivedId.Create(saddle.Id + "#move:" + operation);
        var reservationId = AnimalSaddleReservationId(operation);
        world.ApplyInventoryTransition(inventory => InventoryAuthority.Reserve(InventoryAuthority.Relocate(inventory, operation,
            saddle.Id, saddle.OwnerId, 1, groundPosition: new(animal.Position.X, animal.Position.Y), holdAccess: InventoryHoldRules.SupplyTripFor(actor)), reservationId,
            saddle.OwnerId, unitId, 1, "animal-saddle:" + animal.Id, long.MaxValue, holdAccess: InventoryHoldRules.SupplyTripFor(actor)));
        SetAnimal(animal with { SaddleLotId = unitId, SaddleReservationId = Identity.Parse(reservationId) });
        FinishAnimalSupplyTrip(world, actor);
        world.AppendEvent("animal_saddled", actor + ":" + animal.Id, animal.Position);
    }

    internal void EndAnimalRide(IAnimalWorld world, AnimalState animal, string reason)
    {
        if (animal.RiderId is not { } rider) return;
        SetAnimal(animal with { RiderId = null });
        if (world.Agents.TryGetValue(rider, out var person))
        {
            // Put down the excess at the rider's actual position. Held job inputs stay held;
            // losing a mount never cancels a reservation or changes property ownership.
            foreach (var lot in world.Society.Inventory.Lots.Where(lot => lot.ContainerLotId is null &&
                         PersonalEquipmentRules.IsCarried(lot, rider) && lot.DeliveryBuildingId is null &&
                         !PersonalEquipmentRules.IsSelected(person.Equipment, lot.Id)).OrderBy(lot => lot.Id, StringComparer.Ordinal).ToArray())
            {
                var inventory = world.Society.Inventory;
                var excess = PersonalEquipmentRules.CarriedQuantity(inventory, rider, person.Equipment) -
                    PersonalEquipmentRules.Capacity(inventory, rider, person.Equipment);
                if (excess <= 0) break;
                if (InventoryContainerRules.IsContainer(lot.ItemKind) && HasActiveContainerReservation(inventory, lot.Id)) continue;
                var quantity = InventoryContainerRules.IsContainer(lot.ItemKind) ? 1 : Math.Min(excess, world.PhysicalUnreservedQuantity(lot));
                if (quantity <= 0) continue;
                if (InventoryHoldRules.Family(InventoryIndex.For(inventory), lot)
                    .Contains(new InventoryHold(InventoryHoldPurpose.SupplyTrip, rider)))
                    FinishAnimalSupplyTrip(world, rider);
                world.ApplyInventoryTransition(current => InventoryAuthority.Relocate(current,
                    AnimalCargoDismountId(rider, world.WorldTick, world.NextEventId, lot.Id),
                    lot.Id, lot.OwnerId, quantity, groundPosition: new(person.Position.X, person.Position.Y),
                    holdAccess: InventoryHoldRules.SupplyTripFor(rider)));
            }
        }
        world.AppendEvent("horse_dismounted", rider + ":" + animal.Id + ":" + reason, animal.Position);
    }

    internal void MoveAnimal(IAnimalWorld world, AnimalState animal, GridPoint next)
    {
        if (!world.Map.CanFootStep(animal.Position, next)) throw new InvalidOperationException("Animals use legal foot steps.");
        if (animal.SaddleLotId is { } saddle && animal.SaddleReservationId is { } held)
            world.ApplyInventoryTransition(inventory => InventoryAuthority.MoveAnimalSaddle(inventory, saddle, held, animal.Id,
                new(animal.Position.X, animal.Position.Y), new(next.X, next.Y), world.Map.WrapsEastWest ? world.Map.Width : 0));
        if (animal.ReadyProductLotId is { } product && animal.ReadyProductReservationId is { } productHeld)
            world.ApplyInventoryTransition(inventory => InventoryAuthority.MoveAnimalProduct(inventory, product, productHeld, animal.Id,
                new(animal.Position.X, animal.Position.Y), new(next.X, next.Y), world.Map.WrapsEastWest ? world.Map.Width : 0));
        SetAnimal(animal with { Position = next });
    }

    internal void MoveAttachedAnimal(IAnimalWorld world, string actor, GridPoint from, GridPoint to)
    {
        var animal = State.Animals.FirstOrDefault(item => item.RiderId == actor || item.LeaderId == actor);
        if (animal is null) return;
        if (animal.Position != from) throw new InvalidOperationException("The rider or leader must be beside the animal.");
        MoveAnimal(world, animal, to);
    }
}

