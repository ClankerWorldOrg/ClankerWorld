using ClankerWorld.Simulation.Kernel;

using static ClankerWorld.Simulation.World.WorldRuntime.AnimalSharedRules;

namespace ClankerWorld.Simulation.World;

internal sealed partial class AnimalSystem
{
    private static AnimalSupplyActionPlan PlanAnimalSupply(IAnimalWorld world, string actor, AnimalSupplyChoice choice)
    {
        var source = choice.Lot;
        var carried = PersonalEquipmentRules.IsCarried(source, actor);
        var position = carried ? world.Agents[actor].Position : world.HouseholdStockPosition(source);
        var range = carried ? 0 : HouseholdStockInteractionRange(source);
        return new(actor, new(source.Id, choice.Yard.InstanceId, choice.AnimalId, choice.Action), source.OwnerId,
            source.ItemKind, choice.Quantity, position, range, carried, choice.Yard.HouseholdId!, choice.Yard.Position,
            InventoryContainerRules.IsContainer(source.ItemKind) ? GoodsPlanFamily.Capture(world.Society.Inventory, source) : null,
            world.Reach(actor, carried ? choice.Yard.Position : position, ReachPurpose.AnimalSupply, range, 0, SwimmingPolicy.WhenCapable));
    }

    internal ActionCheck CheckAnimalSupplyPlan(IAnimalWorld world, AnimalSupplyActionPlan plan)
    {
        var capabilities = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true));
        if (!capabilities.Allowed) return capabilities;
        var yard = world.Simulation.Buildings.FirstOrDefault(item => item.InstanceId == plan.Choice.YardId);
        if (yard is null || yard.HouseholdId != plan.HouseholdId || yard.Position != plan.Destination || !MaySupplyAnimalYard(world, plan.Actor, yard) ||
            State.SupplyTrips.Any(trip => trip.ActorId == plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected animal yard is no longer available");
        if (world.RecheckGoods(new(GoodsUse.Collect, plan.Actor, GoodsOwners.One(plan.OwnerId), GoodsKinds.One(plan.ItemKind)), plan.Choice.StockId) is not { } match ||
            match.Lot.ContainerLotId is not null || match.Quantity < plan.Quantity ||
            !GoodsPlanFamily.Matches(world.Society.Inventory, match.Lot, plan.Family) ||
            PersonalEquipmentRules.IsCarried(match.Lot, plan.Actor) != plan.Carried ||
            !plan.Carried && (world.HouseholdStockPosition(match.Lot) != plan.Pickup || HouseholdStockInteractionRange(match.Lot) != plan.PickupRange) ||
            !(match.Lot.OwnerId == plan.Actor || match.Lot.OwnerId == plan.HouseholdId &&
                (world.HouseholdFor(plan.Actor) == plan.HouseholdId || match.Lot.StorageBuildingId == yard.InstanceId || plan.Carried)) ||
            match.Lot.StorageBuildingId is not null && !world.CanRemoveWorkstationStock(world.Society.Inventory, match.Lot, 1))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected animal supply is no longer available");
        var animal = plan.Choice.AnimalId is { } id ? Animal(id) : null;
        var animals = State.Animals.Where(item => item.DiedTick is null && item.YardId == yard.InstanceId && MayCareForAnimal(world, plan.Actor, item)).ToArray();
        if (animals.Length == 0 || plan.Choice.AnimalId is not null && (animal is null || !animals.Contains(animal) ||
            plan.Choice.Action == "care" && AnimalRules.HasCare(animal, world.WorldTick) ||
            plan.Choice.Action == "saddle" && (animal.Species != "horse" || animal.SaddleLotId is not null || !AnimalHouseholdMember(world, plan.Actor, animal))))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected animal no longer needs that supply");
        if (AnimalSupplyDemand(world, plan.Actor, yard, match.Lot, animal, plan.Choice.Action, animals) < plan.Quantity ||
            AnimalRules.IsFeed(plan.ItemKind) && !AnimalFeedMayBeSpent(world, plan.Actor, animal ?? animals[0], match.Lot, plan.Quantity))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected supply quantity is no longer available for this task");
        var capacity = plan.Carried ? world.FreeCarryCapacity(plan.Actor) :
            world.PickupCarryCapacity(plan.Actor, match.Lot, animal?.Position ?? plan.Destination);
        if (!plan.Carried && plan.Quantity > capacity || !plan.Carried && !world.VesselFits(match.Lot, capacity) ||
            animal is null && (plan.Quantity > world.DestinationRoom(yard.InstanceId) || !world.VesselFits(match.Lot, world.DestinationRoom(yard.InstanceId))))
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected animal supply");
        return plan.Carried && animal is not null || world.Reach(plan.Actor, plan.Carried ? plan.Destination : plan.Pickup,
            ReachPurpose.AnimalSupply, plan.PickupRange, 0, SwimmingPolicy.WhenCapable).Count > 0
                ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected animal supply is out of reach now");
    }

    private static int AnimalSupplyDemand(IAnimalWorld world, string actor, PlacedBuilding yard, InventoryLot root,
        AnimalState? animal, string? action, IReadOnlyList<AnimalState> animals)
    {
        var water = world.FindGoods(new(GoodsUse.ConsumeAt, actor, new GoodsOwners([actor, yard.HouseholdId!]),
            GoodsKinds.One(InventoryContainerRules.FreshWater))).Matches;
        if (animal is not null)
        {
            if (action == "saddle") return root.ItemKind == "saddle" && root.OwnerId == animal.HouseholdId ? 1 : 0;
            var supplies = AnimalSuppliesAtHand(world, actor, animal).Where(lot => PersonalEquipmentRules.IsPhysicallyCarried(world.Society.Inventory, lot, actor)).ToArray();
            var definition = AnimalRules.Definition(animal.Species);
            return AnimalRules.IsFeed(root.ItemKind) ? Math.Max(0, definition.DailyFeed - supplies.Where(lot => AnimalRules.IsFeed(lot.ItemKind)).Sum(world.AvailableLotQuantity)) :
                root.ItemKind == InventoryContainerRules.WaterJug && water.Any(content => content.Root.Id == root.Id) &&
                supplies.Where(lot => lot.ItemKind == InventoryContainerRules.FreshWater && lot.ContainerLotId is not null).Sum(world.AvailableLotQuantity) < definition.DailyWater ? 1 : 0;
        }
        if (root.StorageBuildingId == yard.InstanceId) return 0;
        var feedTarget = Math.Min(8, animals.Sum(item => AnimalRules.Definition(item.Species).DailyFeed *
            (1 + (item.Pregnancy is null ? 0 : 1))) + 2);
        var feedStock = world.AnimalYardFeedStock(yard.InstanceId);
        return AnimalRules.IsFeed(root.ItemKind) ? (int)Math.Max(0, feedTarget - feedStock) :
            root.ItemKind == InventoryContainerRules.WaterJug && !water.Any(content => content.Lot.StorageBuildingId == yard.InstanceId && content.Quantity >= 2) &&
            water.Any(content => content.Root.Id == root.Id) ? 1 : 0;
    }

    private void ApplyAnimalSupplyPlan(IAnimalWorld world, AnimalSupplyActionPlan plan)
    {
        if (CheckAnimalSupplyPlan(world, plan).Refusal is { } refusal) { world.RefuseChoice(plan, refusal); return; }
        if (!plan.Carried && !world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, plan.Pickup, plan.PickupRange))
        { world.MoveToward(plan.Actor, world.Agents[plan.Actor], plan.Pickup, "animal_supply_pickup", plan.PickupRange); return; }
        CompleteAnimalSupplyPickup(world, plan, world.Society.Inventory.GetLot(plan.Choice.StockId));
    }
}
