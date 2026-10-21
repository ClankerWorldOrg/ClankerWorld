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

    private void CompleteAnimalSaddling(IAnimalWorld world, string actor, AnimalState animal, InventoryLot saddle)
    {
        var operation = AnimalSaddleOperationId(animal.Id, world.WorldTick, world.NextEventId);
        var unitId = saddle.Quantity == 1 ? saddle.Id : DerivedId.Create(saddle.Id + "#move:" + operation);
        var reservationId = AnimalSaddleReservationId(operation);
        world.ApplyInventoryTransition(inventory => InventoryAuthority.Reserve(InventoryAuthority.Relocate(inventory, operation,
            saddle.Id, saddle.OwnerId, 1, groundPosition: new(animal.Position.X, animal.Position.Y), holdAccess: InventoryHoldRules.SupplyTripFor(actor)), reservationId,
            saddle.OwnerId, unitId, 1, "animal-saddle:" + animal.Id, long.MaxValue, holdAccess: InventoryHoldRules.SupplyTripFor(actor)));
        SetAnimal(animal with { SaddleLotId = unitId, SaddleReservationId = Identity.Parse(reservationId) });
        FinishAnimalSupplyTrip(world, actor);
        world.AppendEvent(new AnimalSaddledEventPayload(actor, animal.Id), animal.Position);
    }

    internal void EndAnimalRide(IAnimalWorld world, AnimalState animal, string reason)
    {
        if (animal.RiderId is { } rider && world.Agents.ContainsKey(rider))
            CompleteAnimalDismount(world, PlanAnimalDismount(world, rider, animal), reason);
        else if (animal.RiderId is { } missing)
        {
            SetAnimal(animal with { RiderId = null });
            world.AppendEvent(new HorseDismountedEventPayload(missing, animal.Id, reason), animal.Position);
        }
    }

    private void CompleteAnimalDismount(IAnimalWorld world, PlannedAnimalDismount plan, string reason)
    {
        var animal = Animal(plan.AnimalId)!;
        SetAnimal(animal with { RiderId = null });
        foreach (var cargo in plan.Cargo)
        {
            var inventory = world.Society.Inventory;
            if (InventoryHoldRules.Family(InventoryIndex.For(inventory), inventory.GetLot(cargo.StockId))
                .Contains(InventoryStore.CreateInventoryHold(InventoryHoldPurpose.SupplyTrip, plan.Actor)))
                FinishAnimalSupplyTrip(world, plan.Actor);
            world.ApplyInventoryTransition(current => InventoryAuthority.Relocate(current,
                AnimalCargoDismountId(plan.Actor, world.WorldTick, world.NextEventId, cargo.StockId),
                cargo.StockId, cargo.OwnerId, cargo.Quantity, groundPosition: new(plan.Position.X, plan.Position.Y),
                holdAccess: InventoryHoldRules.SupplyTripFor(plan.Actor)));
        }
        world.AppendEvent(new HorseDismountedEventPayload(plan.Actor, animal.Id, reason), plan.Position);
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

