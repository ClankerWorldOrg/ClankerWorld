using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

using static ClankerWorld.Simulation.World.WorldRuntime.AnimalSharedRules;

namespace ClankerWorld.Simulation.World;

internal sealed partial class AnimalSystem
{
    internal sealed record AnimalSupplyChoice(InventoryLot Lot, PlacedBuilding Yard, int Quantity,
        string? AnimalId = null, string? Action = null)
    {
        public string Id => AnimalSupplyId(Lot.Id, Yard.InstanceId, AnimalId, Action);
    }

    internal IEnumerable<AnimalSupplyChoice> AnimalSupplyChoices(IAnimalWorld world, string actor, bool ignoreActiveTrip = false)
    {
        if (!world.IsAdult(actor) || !ignoreActiveTrip && State.SupplyTrips.Any(trip => trip.ActorId == actor)) yield break;
        var yards = world.Simulation.Buildings.Where(yard => yard.HouseholdId is not null && world.Content.Buildings.Any(definition =>
            definition.CanonicalId == yard.DefinitionId && definition.Tags.Contains(AnimalContent.YardTag)) && MaySupplyAnimalYard(world, actor, yard));
        foreach (var yard in yards.OrderBy(yard => yard.InstanceId, StringComparer.Ordinal))
            foreach (var choice in AnimalSupplyChoicesAtYard(world, actor, yard.HouseholdId!, yard)) yield return choice;
    }

    internal bool MaySupplyAnimalYard(IAnimalWorld world, string actor, PlacedBuilding yard) => world.IsAdult(actor) &&
        (yard.HouseholdId == world.HouseholdFor(actor) || State.Animals.Any(animal => animal.YardId == yard.InstanceId && MayCareForAnimal(world, actor, animal)));

    internal IEnumerable<AnimalSupplyChoice> AnimalSupplyChoicesAtYard(IAnimalWorld world, string actor, string household, PlacedBuilding yard)
    {
        var inventory = world.Society.Inventory;
        var animals = State.Animals.Where(animal => animal.DiedTick is null && animal.YardId == yard.InstanceId && MayCareForAnimal(world, actor, animal)).ToArray();
        var kinds = new GoodsKinds(["grain", "wild_greens", "cultivated_greens", "water_jug", "saddle"]);
        var request = new GoodsRequest(GoodsUse.Collect, actor, new GoodsOwners([actor, household]), kinds);
        var sources = world.FindGoods(request).Matches.Select(match => match.Lot).Where(lot => lot.ContainerLotId is null &&
            (lot.OwnerId == actor && PersonalEquipmentRules.IsCarried(lot, actor) || lot.OwnerId == household &&
                (world.HouseholdFor(actor) == household || lot.StorageBuildingId == yard.InstanceId || PersonalEquipmentRules.IsCarried(lot, actor))) &&
            (lot.StorageBuildingId is null || world.CanRemoveWorkstationStock(inventory, lot, 1))).ToArray();
        foreach (var animal in animals.OrderBy(animal => animal.Id, StringComparer.Ordinal))
        {
            var actions = new List<string>();
            if (!AnimalRules.HasCare(animal, world.WorldTick)) actions.Add("care");
            if (animal.Species == "horse" && animal.SaddleLotId is null && AnimalHouseholdMember(world, actor, animal)) actions.Add("saddle");
            foreach (var action in actions)
                foreach (var root in sources.Where(lot => !PersonalEquipmentRules.IsCarried(lot, actor)))
                {
                    var wanted = AnimalSupplyDemand(world, actor, yard, root, animal, action, animals);
                    if (wanted <= 0) continue;
                    var capacity = world.PickupCarryCapacity(actor, root, animal.Position);
                    var quantity = Math.Min(wanted, Math.Min(world.AvailableLotQuantity(root), capacity));
                    if (quantity <= 0 || !world.VesselFits(root, capacity) ||
                        AnimalRules.IsFeed(root.ItemKind) && !AnimalFeedMayBeSpent(world, actor, animal, root, quantity)) continue;
                    yield return new(root, yard, quantity, animal.Id, action);
                }
        }
        if (animals.Length == 0) yield break;
        foreach (var root in sources.Where(lot => lot.StorageBuildingId != yard.InstanceId))
        {
            var wanted = AnimalSupplyDemand(world, actor, yard, root, null, null, animals);
            var quantity = Math.Min(wanted, Math.Min(world.AvailableLotQuantity(root), world.DestinationRoom(yard.InstanceId)));
            var carried = PersonalEquipmentRules.IsCarried(root, actor);
            var capacity = carried ? world.FreeCarryCapacity(actor) : world.PickupCarryCapacity(actor, root, yard.Position);
            if (carried && world.Reach(actor, yard.Position, ReachPurpose.AnimalSupply, 0, 0, SwimmingPolicy.WhenCapable).Count == 0) continue;
            if (!carried) quantity = Math.Min(quantity, capacity);
            if (quantity <= 0 || !world.VesselFits(root, world.DestinationRoom(yard.InstanceId)) ||
                !carried && !world.VesselFits(root, capacity) ||
                AnimalRules.IsFeed(root.ItemKind) && !AnimalFeedMayBeSpent(world, actor, animals[0], root, quantity)) continue;
            yield return new(root, yard, quantity);
        }
    }

    internal void AddAnimalSupplyCandidates(IAnimalWorld world, List<CognitionCandidate> candidates, string actor)
    {
        if (State.SupplyTrips.FirstOrDefault(trip => trip.ActorId == actor) is { } trip)
        {
            if (PlanAnimalTrip(world, actor, trip) is { } plan && CheckAnimalTripPlan(world, plan).Allowed)
                candidates.Add(new(plan.CandidateId, trip.AnimalId is null ?
                    "Carry the collected feed or jug to the household animal yard." : "Carry the collected supply to the animal and finish its care task.", 14, trip.AnimalId ?? trip.YardId)
                { ActionPlan = plan });
        }
        else foreach (var plan in AnimalSupplyChoices(world, actor).Select(choice => PlanAnimalSupply(world, actor, choice))
            .Where(plan => CheckAnimalSupplyPlan(world, plan).Allowed).Take(12))
            candidates.Add(new(plan.CandidateId, $"Collect physical {plan.ItemKind.Replace('_', ' ')} for " +
                (plan.Choice.AnimalId is null ? "the animal yard supply stock." : Animal(plan.Choice.AnimalId)!.Name + "."), 14,
                plan.Choice.AnimalId ?? plan.Choice.YardId)
            { ActionPlan = plan });
    }

    internal bool ApplyAnimalSupplyCandidate(IAnimalWorld world, string actor, string id)
    {
        if (id.StartsWith("animal:supply_trip:", StringComparison.Ordinal))
        {
            if (State.SupplyTrips.FirstOrDefault(trip => trip.ActorId == actor) is not { } trip) return true;
            if (PlanAnimalTrip(world, actor, trip) is { } plan) ApplyAnimalTripPlan(world, plan);
            else FinishAnimalSupplyTrip(world, actor);
            return true;
        }
        if (!id.StartsWith("animal:supply:", StringComparison.Ordinal)) return false;
        var choice = AnimalSupplyChoices(world, actor).FirstOrDefault(choice => choice.Id == id);
        if (choice is null) return true;
        ApplyAnimalSupplyPlan(world, PlanAnimalSupply(world, actor, choice));
        return true;
    }

    private void CompleteAnimalSupplyPickup(IAnimalWorld world, AnimalSupplyActionPlan plan, InventoryLot source)
    {
        // The trip follows the selected physical load, including when it was already carried.
        if (!plan.Carried || plan.Quantity < source.Quantity)
        {
            var operation = AnimalPickupId(plan.Actor, world.WorldTick, world.NextEventId);
            world.RunInventoryCommand(InventorySystem.PickUpAnimalSupply(operation, source, plan));
            if (plan.Quantity < source.Quantity) source = world.Society.Inventory.GetLot(DerivedId.Create(source.Id + "#move:" + operation));
        }
        world.RunInventoryCommand(InventorySystem.HoldAnimalSupplyLoad(source, plan));
        Commit(State with
        {
            SupplyTrips = State.SupplyTrips.Append(new(plan.Actor, source.Id,
            plan.Choice.YardId, plan.Choice.AnimalId, plan.Choice.Action)).OrderBy(trip => trip.ActorId, StringComparer.Ordinal).ToArray()
        });
    }

    private void CompleteAnimalTripDelivery(IAnimalWorld world, AnimalTripActionPlan plan, InventoryLot lot)
    {
        world.RunInventoryCommand(InventorySystem.CompleteAnimalTripDelivery(lot, plan, world));
        FinishAnimalSupplyTrip(world, plan.Actor);
        world.AppendEvent(new AnimalYardSuppliedEventPayload(plan.Actor, plan.Trip.YardId));
    }

    internal void FinishAnimalSupplyTrip(IAnimalWorld world, string actor)
    {
        var hold = InventoryStore.CreateInventoryHold(InventoryHoldPurpose.SupplyTrip, actor);
        foreach (var lot in InventoryIndex.For(world.Society.Inventory).Lots.Where(lot => (lot.Holds ?? []).Contains(hold)))
            world.RunInventoryCommand(InventorySystem.FinishAnimalSupplyTrip(lot, hold));
        Commit(State with { SupplyTrips = State.SupplyTrips.Where(trip => trip.ActorId != actor).ToArray() });
    }

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

    internal ActionCheck CheckAnimalSupplyPlan(IAnimalWorld world, AnimalSupplyActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capabilities = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ActionCapabilities);
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
        ApplyCheckedAnimalSupply(world, plan);
    }

    internal void ApplyCheckedAnimalSupply(IAnimalWorld world, AnimalSupplyActionPlan plan)
    {
        if (!plan.Carried && !world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, plan.Pickup, plan.PickupRange))
        { world.MoveToward(plan.Actor, world.Agents[plan.Actor], plan.Pickup, "animal_supply_pickup", plan.PickupRange); return; }
        CompleteAnimalSupplyPickup(world, plan, world.Society.Inventory.GetLot(plan.Choice.StockId));
    }

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

    internal ActionCheck CheckAnimalTripPlan(IAnimalWorld world, AnimalTripActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ActionCapabilities);
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
        ApplyCheckedAnimalTrip(world, plan);
    }

    internal void ApplyCheckedAnimalTrip(IAnimalWorld world, AnimalTripActionPlan plan)
    {
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
