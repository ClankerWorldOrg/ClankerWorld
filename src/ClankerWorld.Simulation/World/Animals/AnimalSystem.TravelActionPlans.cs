using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class AnimalSystem
{
    private bool MayMountAnimal(IAnimalWorld world, string actor, AnimalState animal) =>
        MayRideAnimal(world, actor, animal) && animal.SaddleLotId is not null && animal.RiderId is null && animal.LeaderId is null &&
        AnimalRules.HasCare(animal, world.WorldTick) && AnimalRules.IsAdult(animal, world.WorldTick, AnimalDayTicks(world)) &&
        world.AttachedHandcart(actor) is null && !State.Animals.Any(item => item.RiderId == actor || item.LeaderId == actor);

    private static PlannedAnimalDismount PlanAnimalDismount(IAnimalWorld world, string actor, AnimalState animal)
    {
        var inventory = world.Society.Inventory;
        var equipment = world.Agents[actor].Equipment;
        var capacity = PersonalEquipmentRules.Capacity(inventory, actor, equipment);
        var carried = PersonalEquipmentRules.CarriedQuantity(inventory, actor, equipment);
        var excess = carried - capacity;
        var cargo = new List<PlannedAnimalDrop>();
        if (excess > 0)
            foreach (var match in world.DismountCargo(actor))
            {
                if (excess <= 0) break;
                var vessel = InventoryContainerRules.IsContainer(match.Lot.ItemKind);
                var quantity = vessel ? 1 : Math.Min(excess, match.Quantity);
                if (quantity <= 0) continue;
                var physical = vessel ? match.MoveUnits : quantity;
                cargo.Add(new(match.Lot.Id, match.Lot.OwnerId, match.Lot.ItemKind, quantity, physical,
                    vessel ? GoodsPlanFamily.Capture(inventory, match.Lot) : null));
                excess -= physical;
            }
        return new(actor, animal.Id, world.Agents[actor].Position, capacity, carried, cargo.AsReadOnly());
    }

    private ActionCheck CheckAnimalDismount(IAnimalWorld world, PlannedAnimalDismount plan)
    {
        if (!world.Agents.TryGetValue(plan.Actor, out var person) || Animal(plan.AnimalId) is not { RiderId: var rider } animal || rider != plan.Actor ||
            animal.Position != plan.Position || person.Position != plan.Position)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected ride has changed");
        var inventory = world.Society.Inventory;
        if (PersonalEquipmentRules.Capacity(inventory, plan.Actor, person.Equipment) != plan.BaseCapacity ||
            PersonalEquipmentRules.CarriedQuantity(inventory, plan.Actor, person.Equipment) != plan.CarriedQuantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the load or carrying capacity has changed");
        foreach (var cargo in plan.Cargo)
            if (world.RecheckGoods(new(GoodsUse.DismountCargo, plan.Actor, GoodsOwners.One(cargo.OwnerId), GoodsKinds.One(cargo.ItemKind)), cargo.StockId) is not { } current ||
                current.Quantity < cargo.Quantity || !GoodsPlanFamily.Matches(inventory, current.Lot, cargo.Family))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "a selected dismount load is no longer available");
        return new();
    }

    private AnimalTravelActionPlan? PlanAnimalTravel(IAnimalWorld world, string actor, AnimalTravelChoice choice)
    {
        if (Animal(choice.AnimalId) is not { } animal) return null;
        if (choice.Kind == AnimalTravelKind.Dismount)
            return animal.RiderId == actor ? new(actor, choice, animal.Position, animal.HouseholdId, animal.YardId, null, false,
                animal.SaddleLotId, animal.SaddleReservationId, PlanAnimalDismount(world, actor, animal), Array.AsReadOnly<GridPoint>([])) : null;
        var yard = choice.Kind == AnimalTravelKind.LeadHome ? AssignedAnimalYard(world, animal) : null;
        var release = yard is not null && YardTiles(world, yard).Contains(animal.Position);
        var destination = yard is not null ? release ? yard.Position : FreeAnimalYardTile(world, yard, animal.Id) : (GridPoint?)null;
        if (choice.Kind == AnimalTravelKind.LeadHome && destination is null) return null;
        var target = world.Agents[actor].Position != animal.Position && animal.LeaderId != actor ? animal.Position : destination ?? animal.Position;
        return new(actor, choice, animal.Position, animal.HouseholdId, yard?.InstanceId, destination, release,
            animal.SaddleLotId, animal.SaddleReservationId, null,
            world.Reach(actor, target, ReachPurpose.AnimalTravel, 0, 0,
                choice.Kind == AnimalTravelKind.LeadHome && (animal.LeaderId == actor || world.Agents[actor].Position == animal.Position)
                    ? SwimmingPolicy.Never : SwimmingPolicy.WhenCapable));
    }

    internal ActionCheck CheckAnimalTravelPlan(IAnimalWorld world, AnimalTravelActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ActionCapabilities);
        if (!capability.Allowed) return capability;
        var animal = Animal(plan.Choice.AnimalId);
        if (animal is null || animal.DiedTick is not null || animal.Position != plan.Position || animal.HouseholdId != plan.HouseholdId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected animal has changed");
        if (plan.Choice.Kind == AnimalTravelKind.Dismount) return CheckAnimalDismount(world, plan.Dismount!);
        if (plan.Choice.Kind == AnimalTravelKind.Mount && (!MayMountAnimal(world, plan.Actor, animal) || animal.SaddleLotId != plan.SaddleStockId || animal.SaddleReservationId != plan.SaddleReservationId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected horse is no longer available for riding");
        if (plan.Choice.Kind == AnimalTravelKind.LeadHome)
        {
            var yard = AssignedAnimalYard(world, animal);
            if (!AnimalHouseholdMember(world, plan.Actor, animal) || yard is null || yard.InstanceId != plan.YardId || !MayLeadAnimal(world, plan.Actor, animal) ||
                animal.RiderId is not null || animal.LeaderId is not null && animal.LeaderId != plan.Actor ||
                YardTiles(world, yard).Contains(animal.Position) != plan.ReleaseLead || plan.Destination is not { } destination || !YardTiles(world, yard).Contains(destination))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected animal yard is no longer available");
            if (!plan.ReleaseLead && (!world.Map.IsPassable(destination) || State.Animals.Any(other => other.Id != animal.Id && other.DiedTick is null && other.Position == destination) ||
                world.Agents.Values.Any(person => person.Position == destination)))
                return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected yard place is occupied now");
        }
        if (plan.ReleaseLead) return new();
        var target = world.Agents[plan.Actor].Position != plan.Position && animal.LeaderId != plan.Actor ? plan.Position : plan.Destination ?? plan.Position;
        return world.Reach(plan.Actor, target, ReachPurpose.AnimalTravel, 0, 0,
            plan.Choice.Kind == AnimalTravelKind.LeadHome && (animal.LeaderId == plan.Actor || world.Agents[plan.Actor].Position == animal.Position)
                ? SwimmingPolicy.Never : SwimmingPolicy.WhenCapable).Count > 0
                ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected animal or yard is out of reach now");
    }

    private void ApplyAnimalTravelPlan(IAnimalWorld world, AnimalTravelActionPlan plan)
    {
        if (CheckAnimalTravelPlan(world, plan).Refusal is { } refusal) { world.RefuseChoice(plan, refusal); return; }
        ApplyCheckedAnimalTravel(world, plan);
    }

    internal void ApplyCheckedAnimalTravel(IAnimalWorld world, AnimalTravelActionPlan plan)
    {
        if (plan.Dismount is { } dismount) { CompleteAnimalDismount(world, dismount, "dismounted"); return; }
        var animal = Animal(plan.Choice.AnimalId)!;
        if (world.Agents[plan.Actor].Position != plan.Position && animal.LeaderId != plan.Actor)
        { world.MoveToward(plan.Actor, world.Agents[plan.Actor], plan.Position, "animal_" + plan.Choice.Action); return; }
        if (plan.Choice.Kind == AnimalTravelKind.LeadHome) CompleteAnimalLead(world, plan, animal);
        else
        {
            SetAnimal(animal with { RiderId = plan.Actor });
            world.AppendEvent("horse_mounted", plan.Actor + ":" + animal.Id, plan.Position);
        }
    }
}
