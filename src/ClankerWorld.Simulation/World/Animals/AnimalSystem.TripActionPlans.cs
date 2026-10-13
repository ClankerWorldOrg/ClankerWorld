using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class AnimalSystem
{
    private AnimalTripActionPlan? PlanAnimalTrip(IAnimalWorld world, string actor, AnimalSupplyTrip trip)
    {
        if (InventoryIndex.For(world.Society.Inventory).Find(trip.LotId) is not { } lot) return null;
        var yard = world.Simulation.Buildings.FirstOrDefault(item => item.InstanceId == trip.YardId && MaySupplyAnimalYard(world, actor, item));
        var animal = trip.AnimalId is { } id ? Animal(id) : null;
        var finished = animal is not null && (trip.Action == "care" && AnimalRules.HasCare(animal, world.WorldTick) ||
            trip.Action == "saddle" && animal.SaddleLotId is not null);
        var work = !finished && animal is not null && yard is not null && MayCareForAnimal(world, actor, animal)
            ? PlanAnimalWork(world, actor, new(trip.Action == "care" ? AnimalWorkKind.Care : AnimalWorkKind.Saddle, animal.Id)) : null;
        var step = finished || yard is null || trip.AnimalId is not null && work is null ? AnimalTripStep.Release :
            work is not null ? AnimalTripStep.Work : AnimalTripStep.Deliver;
        var room = yard is not null ? world.DestinationRoom(yard.InstanceId, lot) : 0;
        var quantity = step == AnimalTripStep.Deliver ? InventoryContainerRules.IsContainer(lot.ItemKind)
            ? world.VesselFits(lot, room) ? 1 : 0 : Math.Min(world.AvailableLotQuantity(lot), room) : 0;
        return new(actor, trip, lot.OwnerId, lot.ItemKind, quantity,
            InventoryContainerRules.IsContainer(lot.ItemKind) ? GoodsPlanFamily.Capture(world.Society.Inventory, lot) : null,
            step, yard?.HouseholdId, yard?.Position, work,
            step == AnimalTripStep.Deliver ? world.Reach(actor, yard!.Position, ReachPurpose.AnimalTrip, 0, 0, SwimmingPolicy.WhenCapable) : Array.AsReadOnly<GridPoint>([]));
    }

    internal ActionCheck CheckAnimalTripPlan(IAnimalWorld world, AnimalTripActionPlan plan)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if (State.SupplyTrips.FirstOrDefault(trip => trip.ActorId == plan.Actor) != plan.Trip)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected supply trip has changed");
        if (plan.Step == AnimalTripStep.Release) return new();
        if (world.RecheckGoods(new(GoodsUse.AnimalSupplyTrip, plan.Actor, GoodsOwners.One(plan.OwnerId), GoodsKinds.One(plan.ItemKind)), plan.Trip.LotId) is not { } load ||
            !GoodsPlanFamily.Matches(world.Society.Inventory, load.Lot, plan.Family) || load.Quantity < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected trip load is no longer available");
        var yard = world.Simulation.Buildings.FirstOrDefault(item => item.InstanceId == plan.Trip.YardId);
        if (yard is null || yard.HouseholdId != plan.HouseholdId || yard.Position != plan.Destination || !MaySupplyAnimalYard(world, plan.Actor, yard))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected animal yard is no longer available");
        if (plan.Step == AnimalTripStep.Work) return CheckAnimalWorkPlan(world, plan.Work!);
        if (plan.Quantity <= 0 || plan.Quantity > world.DestinationRoom(yard.InstanceId, load.Lot) || !world.VesselFits(load.Lot, world.DestinationRoom(yard.InstanceId, load.Lot)))
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected trip load");
        return world.Reach(plan.Actor, plan.Destination!.Value, ReachPurpose.AnimalTrip, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected animal yard is out of reach now");
    }

    private void ApplyAnimalTripPlan(IAnimalWorld world, AnimalTripActionPlan plan)
    {
        if (CheckAnimalTripPlan(world, plan).Refusal is { } refusal) { world.RefuseChoice(plan, refusal); return; }
        if (plan.Step == AnimalTripStep.Release) { FinishAnimalSupplyTrip(world, plan.Actor); return; }
        if (plan.Step == AnimalTripStep.Work)
        {
            ApplyAnimalWorkPlan(world, plan.Work!);
            var animal = Animal(plan.Trip.AnimalId!)!;
            if (plan.Trip.Action == "care" && AnimalRules.HasCare(animal, world.WorldTick) || plan.Trip.Action == "saddle" && animal.SaddleLotId is not null ||
                plan.Trip.Action == "care" && AnimalCareInputs(world, plan.Actor, animal, AnimalRules.Definition(animal.Species).DailyFeed,
                    AnimalRules.Definition(animal.Species).DailyWater) is null) FinishAnimalSupplyTrip(world, plan.Actor);
            return;
        }
        if (world.Agents[plan.Actor].Position != plan.Destination)
        { world.MoveToward(plan.Actor, world.Agents[plan.Actor], plan.Destination!.Value, "animal_yard_supply"); return; }
        CompleteAnimalTripDelivery(world, plan, world.Society.Inventory.GetLot(plan.Trip.LotId));
    }
}
