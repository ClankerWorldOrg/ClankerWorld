using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
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

    internal AnimalState? RidingAnimal(IAnimalWorld world, string actor) => State.Animals.FirstOrDefault(animal => animal.RiderId == actor &&
        MayRideAnimal(world, actor, animal) && AnimalRules.HasCare(animal, world.WorldTick) && animal.SaddleLotId is not null);
    internal int HorseCargoCapacity(IAnimalWorld world, string actor) => RidingAnimal(world, actor) is null ? 0 : AnimalRules.RidingCargo;

    private void CompleteAnimalSaddling(IAnimalWorld world, string actor, AnimalState animal, InventoryLot saddle)
    {
        var operation = AnimalSaddleOperationId(animal.Id, world.WorldTick, world.NextEventId);
        var unitId = saddle.Quantity == 1 ? saddle.Id : DerivedId.Create(saddle.Id + "#move:" + operation);
        var reservationId = AnimalSaddleReservationId(operation);
        world.RunInventoryCommand(InventorySystem.CompleteAnimalSaddling(operation, saddle, animal, actor, reservationId, unitId));
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
            world.RunInventoryCommand(InventorySystem.CompleteAnimalDismount(plan, world, cargo));
        }
        world.AppendEvent(new HorseDismountedEventPayload(plan.Actor, animal.Id, reason), plan.Position);
    }

    internal void MoveAnimal(IAnimalWorld world, AnimalState animal, GridPoint next)
    {
        if (!world.Map.CanFootStep(animal.Position, next)) throw new InvalidOperationException("Animals use legal foot steps.");
        if (animal.SaddleLotId is { } saddle && animal.SaddleReservationId is { } held)
            world.RunInventoryCommand(InventorySystem.MoveSaddleWithAnimal(saddle, held, animal, next, world));
        if (animal.ReadyProductLotId is { } product && animal.ReadyProductReservationId is { } productHeld)
            world.RunInventoryCommand(InventorySystem.MoveProductWithAnimal(product, productHeld, animal, next, world));
        SetAnimal(animal with { Position = next });
    }

    internal void MoveAttachedAnimal(IAnimalWorld world, string actor, GridPoint from, GridPoint to)
    {
        var animal = State.Animals.FirstOrDefault(item => item.RiderId == actor || item.LeaderId == actor);
        if (animal is null) return;
        if (animal.Position != from) throw new InvalidOperationException("The rider or leader must be beside the animal.");
        MoveAnimal(world, animal, to);
    }

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
            world.AppendEvent(new HorseMountedEventPayload(plan.Actor, animal.Id), plan.Position);
        }
    }

    internal sealed record AnimalAgreement(string Action, string AnimalId, string OtherId, string? LotId = null)
    {
        public string Id => AnimalAgreementId(Action, AnimalId, OtherId, LotId);
    }

    internal IEnumerable<AnimalAgreement> AnimalAgreements(IAnimalWorld world, string actor)
    {
        if (!world.IsAdult(actor)) yield break;
        foreach (var animal in State.Animals.Where(animal => AnimalHouseholdMember(world, actor, animal)))
        {
            var permissionRecipients = 0;
            var tradeRecipients = 0;
            foreach (var recipient in world.Agents.Values.Where(person => animal.DiedTick is null && AnimalRecipientIsNear(world, actor, animal, person.AgentId))
                         .OrderBy(person => person.AgentId, StringComparer.Ordinal))
            {
                if (permissionRecipients++ < 8)
                {
                    yield return new(animal.CarePermissions.Contains(recipient.AgentId) ? "revoke_care" : "grant_care", animal.Id, recipient.AgentId);
                    if (animal.Species == "horse") yield return new(animal.RidingPermissions.Contains(recipient.AgentId) ? "revoke_ride" : "grant_ride", animal.Id, recipient.AgentId);
                }
                if (world.HouseholdFor(recipient.AgentId) is not { } home || AnimalYard(world, home) is not { } yard ||
                    !MayOfferAnimalTrade(world, actor, animal, recipient.AgentId, home, yard)) continue;
                if (tradeRecipients++ >= 8) continue;
                yield return new("offer_gift", animal.Id, recipient.AgentId);
                foreach (var lot in world.AnimalPayments(recipient.AgentId).Take(4).Select(match => match.Lot))
                    yield return new("offer_sale", animal.Id, recipient.AgentId, lot.Id);
            }
            // Revocation stays available after the permitted adult moves away.
            foreach (var permission in animal.CarePermissions) yield return new("revoke_care", animal.Id, permission);
            foreach (var permission in animal.RidingPermissions) yield return new("revoke_ride", animal.Id, permission);
        }
        foreach (var offer in State.Offers.Where(offer => offer.BuyerId == actor && ValidAnimalOffer(world, offer)))
        {
            yield return new("accept", offer.AnimalId, offer.Id);
            yield return new("decline", offer.AnimalId, offer.Id);
        }
    }

    internal bool HasAnimalTransferSpace(IAnimalWorld world, string household, PlacedBuilding yard, AnimalState animal, string? ignoringOffer = null)
    {
        var reserved = ignoringOffer is null ? 0 : 1 + (animal.Pregnancy is null ? 0 : 1);
        var required = 1 + (animal.Pregnancy is null ? 0 : 1);
        return AnimalPlacesUsed(household) - reserved + required <= AnimalRules.PopulationCap &&
            AnimalPlacesUsed(household, yard.InstanceId) - reserved + required <= AnimalYardCapacity(world, yard);
    }

    internal bool ValidAnimalOffer(IAnimalWorld world, AnimalTradeOffer offer)
    {
        var animal = Animal(offer.AnimalId);
        var yard = world.Simulation.Buildings.FirstOrDefault(building => building.InstanceId == offer.ReceivingYardId &&
            building.HouseholdId == offer.ReceivingHouseholdId && world.Content.Buildings.Any(definition =>
                definition.CanonicalId == building.DefinitionId && definition.Tags.Contains(AnimalContent.YardTag)));
        if (animal is null || animal.DiedTick is not null || animal.RiderId is not null || animal.LeaderId is not null ||
            animal.ReadyProductLotId is not null || yard is null || !AnimalHouseholdMember(world, offer.SellerId, animal) ||
            !world.IsAdult(offer.BuyerId) || world.HouseholdFor(offer.BuyerId) != offer.ReceivingHouseholdId ||
            animal.HouseholdId == offer.ReceivingHouseholdId || world.WorldTick - offer.OfferedTick >= AnimalDayTicks(world) ||
            !HasAnimalTransferSpace(world, offer.ReceivingHouseholdId, yard, animal, offer.Id) ||
            !world.IsWithinInteractionRange(world.Agents[offer.SellerId].Position, animal.Position, ResourceInteractionRange) ||
            !world.IsWithinInteractionRange(world.Agents[offer.BuyerId].Position, animal.Position, ResourceInteractionRange)) return false;
        return offer.PaymentLotId is null || world.RecheckGoods(new(GoodsUse.AnimalPayment, offer.BuyerId,
            GoodsOwners.One(offer.BuyerId), GoodsKinds.One(offer.PaymentKind!)), offer.PaymentLotId) is { Quantity: var quantity } && quantity >= offer.PaymentQuantity;
    }

    internal void AddAnimalPermissionAndTradeCandidates(IAnimalWorld world, List<CognitionCandidate> candidates, string actor)
    {
        foreach (var plan in AnimalAgreements(world, actor).DistinctBy(item => item.Id).Select(agreement => PlanAnimalAgreement(world, actor, agreement))
            .Where(plan => plan is not null && CheckAnimalAgreementPlan(world, plan).Allowed).Cast<AnimalAgreementActionPlan>().Take(48))
        {
            var agreement = new AnimalAgreement(plan.Choice.Action, plan.Choice.AnimalId, plan.Choice.OtherId, plan.Choice.StockId);
            var animal = Animal(agreement.AnimalId)!;
            var name = world.Society.Agents.FirstOrDefault(person => person.Id == agreement.OtherId)?.Name ?? agreement.OtherId;
            var offer = State.Offers.FirstOrDefault(offer => offer.Id == agreement.OtherId);
            var description = agreement.Action switch
            {
                "grant_care" => $"Allow {name} to care for {animal.Name} using its animal-yard supplies and collect its household products.",
                "grant_ride" => $"Allow {name} to ride {animal.Name}; this grants no ownership or stock access.",
                "revoke_care" => $"Revoke {name}'s care permission for {animal.Name}.",
                "revoke_ride" => $"Revoke {name}'s riding permission for {animal.Name}.",
                "offer_gift" => $"Offer the specific animal {animal.Name} as a gift to {name}'s household; they must accept.",
                "offer_sale" => $"Offer {animal.Name} to {name}'s household for one {world.Society.Inventory.GetLot(agreement.LotId!).ItemKind.Replace('_', ' ')} from their specific carried lot; they must accept.",
                "accept" => $"Accept {animal.Name} for your household and lead it home, " +
                    (offer!.PaymentKind is null ? "as an offered gift." : $"paying exactly {offer.PaymentQuantity} {offer.PaymentKind.Replace('_', ' ')} from the named carried lot."),
                _ => $"Decline the offer for {animal.Name}.",
            };
            candidates.Add(new(agreement.Id, description, 190, animal.Id) { ActionPlan = plan });
        }
    }

    internal bool TryPlanAnimalConsentDecision(IAnimalWorld world, SocietyCognitionDispatchResult decision, out AnimalAgreementActionPlan? plan)
    {
        plan = null;
        var selected = decision.Admission.Intention;
        if (selected is null || !selected.CandidateId.StartsWith("animal:agreement:", StringComparison.Ordinal)) return false;
        if (!decision.Admission.Accepted || decision.Admission.FellBack || selected.Provider != DecisionProviderKind.LargeLanguageModel ||
            selected.AgentId != decision.AgentId || world.PendingInstructionFor(decision.AgentId)?.Kind == OwnerInstructionKind.Order) return true;
        var actor = decision.AgentId;
        plan = decision.ActionPlan as AnimalAgreementActionPlan;
        if (plan is null && AnimalAgreements(world, actor).FirstOrDefault(item => item.Id == selected.CandidateId) is { } current)
            plan = PlanAnimalAgreement(world, actor, current);
        return true;
    }

    internal void CompleteAnimalAgreement(IAnimalWorld world, AnimalAgreementActionPlan plan)
    {
        var actor = plan.Actor;
        var agreement = new AnimalAgreement(plan.Choice.Action, plan.Choice.AnimalId, plan.Choice.OtherId, plan.Choice.StockId);
        var animal = Animal(agreement.AnimalId)!;
        if (agreement.Action.StartsWith("grant_", StringComparison.Ordinal) || agreement.Action.StartsWith("revoke_", StringComparison.Ordinal))
        {
            var riding = agreement.Action.EndsWith("ride", StringComparison.Ordinal);
            var grant = agreement.Action.StartsWith("grant_", StringComparison.Ordinal);
            var permissions = (riding ? animal.RidingPermissions : animal.CarePermissions).Where(id => id != agreement.OtherId)
                .Concat(grant ? [agreement.OtherId] : Array.Empty<string>()).Order(StringComparer.Ordinal).ToArray();
            SetAnimal(riding ? animal with { RidingPermissions = permissions } : animal with { CarePermissions = permissions });
            if (plan.RiderDismount is { } dismount) CompleteAnimalDismount(world, dismount, "permission_revoked");
            world.AppendEvent(new AnimalPermissionChangedEventPayload(actor, animal.Id, agreement.Action, agreement.OtherId));
        }
        else if (agreement.Action is "offer_sale" or "offer_gift")
        {
            var home = plan.ReceivingHouseholdId!;
            var payment = agreement.LotId is null ? null : world.Society.Inventory.GetLot(agreement.LotId);
            var offer = new AnimalTradeOffer(Identity.Parse(AnimalOfferId(actor, animal.Id, world.WorldTick, world.NextEventId)),
                animal.Id, actor, agreement.OtherId, home, plan.ReceivingYardId!, payment?.ItemKind, payment is null ? 0 : 1, world.WorldTick)
            { PaymentLotId = payment?.Id };
            Commit(State with { Offers = State.Offers.Append(offer).OrderBy(offer => offer.Id.Value, StringComparer.Ordinal).ToArray() });
            world.AppendEvent(new AnimalTradeOfferedEventPayload(actor, offer.Id, animal.Id, (payment?.Id ?? "gift")));
        }
        else
        {
            var offer = plan.Offer!;
            if (agreement.Action == "accept")
            {
                if (offer.PaymentLotId is { } payment)
                    world.RunInventoryCommand(InventorySystem.PayForAnimalTransfer(offer, actor, animal, payment));
                if (animal.SaddleReservationId is { } saddleHeld)
                    world.RunInventoryCommand(InventorySystem.ReleaseTransferredSaddle(saddleHeld));
                SetAnimal(animal with
                {
                    HouseholdId = offer.ReceivingHouseholdId,
                    YardId = offer.ReceivingYardId,
                    HerdId = AnimalHerdId(offer.ReceivingHouseholdId),
                    CarePermissions = [],
                    RidingPermissions = [],
                    SaddleLotId = null,
                    SaddleReservationId = null,
                    TamingWork = null,
                    LeaderId = plan.LeadAfterTransfer ? actor : null,
                    LeadDestination = plan.LeadAfterTransfer ?
                        plan.ReceivingYardPosition : null
                });
                world.AppendEvent(new AnimalTransferredEventPayload(actor, offer.Id, animal.Id, offer.ReceivingHouseholdId), animal.Position);
            }
            Commit(State with { Offers = State.Offers.Where(item => item.Id != offer.Id).ToArray() });
        }
    }

    private AnimalAgreementActionPlan? PlanAnimalAgreement(IAnimalWorld world, string actor, AnimalAgreement agreement)
    {
        if (Animal(agreement.AnimalId) is not { HouseholdId: { } household } animal) return null;
        var kind = agreement.Action switch
        {
            "grant_care" => AnimalAgreementKind.GrantCare,
            "revoke_care" => AnimalAgreementKind.RevokeCare,
            "grant_ride" => AnimalAgreementKind.GrantRide,
            "revoke_ride" => AnimalAgreementKind.RevokeRide,
            "offer_gift" => AnimalAgreementKind.OfferGift,
            "offer_sale" => AnimalAgreementKind.OfferSale,
            "accept" => AnimalAgreementKind.Accept,
            _ => AnimalAgreementKind.Decline,
        };
        var offer = kind is AnimalAgreementKind.Accept or AnimalAgreementKind.Decline ? State.Offers.FirstOrDefault(item => item.Id == agreement.OtherId) : null;
        var receiving = offer?.ReceivingHouseholdId ?? (kind is AnimalAgreementKind.OfferGift or AnimalAgreementKind.OfferSale ? world.HouseholdFor(agreement.OtherId) : null);
        var yard = offer is not null ? world.Simulation.Buildings.FirstOrDefault(item => item.InstanceId == offer.ReceivingYardId) :
            receiving is not null ? AnimalYard(world, receiving) : null;
        var stockId = offer?.PaymentLotId ?? agreement.LotId;
        var owner = offer?.BuyerId ?? agreement.OtherId;
        var payment = stockId is not null ? world.AnimalPayments(owner).FirstOrDefault(item => item.Lot.Id == stockId)?.Lot : null;
        if (stockId is not null && payment is null) return null;
        return new(actor, new(kind, agreement.AnimalId, agreement.OtherId, agreement.LotId), household, receiving, yard?.InstanceId, yard?.Position,
            stockId is null ? null : owner, payment?.ItemKind, offer?.PaymentQuantity ?? (payment is null ? 0 : 1), offer,
            kind == AnimalAgreementKind.RevokeRide && animal.RiderId == agreement.OtherId
                ? PlanAnimalDismount(world, agreement.OtherId, animal) : null,
            kind == AnimalAgreementKind.Accept && world.Agents[actor].Position == animal.Position && MayLeadAnimal(world, actor, animal));
    }

    private static bool AnimalRecipientIsNear(IAnimalWorld world, string actor, AnimalState animal, string recipient) =>
        world.Agents.TryGetValue(recipient, out var person) && recipient != actor && world.IsAdult(recipient) &&
        world.HouseholdFor(recipient) != animal.HouseholdId && world.IsWithinInteractionRange(world.Agents[actor].Position,
            person.Position, WorldRuntime.AnimalSharedRules.ResourceInteractionRange);

    private bool MayOfferAnimalTrade(IAnimalWorld world, string actor, AnimalState animal, string recipient, string household, PlacedBuilding yard) =>
        animal.DiedTick is null && AnimalRecipientIsNear(world, actor, animal, recipient) && animal.RiderId is null && animal.LeaderId is null &&
        animal.ReadyProductLotId is null && !State.Offers.Any(item => item.AnimalId == animal.Id) &&
        HasAnimalTransferSpace(world, household, yard, animal) &&
        world.IsWithinInteractionRange(world.Agents[actor].Position, animal.Position, WorldRuntime.AnimalSharedRules.ResourceInteractionRange) &&
        world.IsWithinInteractionRange(world.Agents[recipient].Position, animal.Position, WorldRuntime.AnimalSharedRules.ResourceInteractionRange);

    internal ActionCheck CheckAnimalAgreementPlan(IAnimalWorld world, AnimalAgreementActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ActionCapabilities);
        if (!capability.Allowed) return capability;
        if (world.PendingInstructionFor(plan.Actor)?.Kind == OwnerInstructionKind.Order)
            return ActionCheck.Refuse(ActionRefusalReason.ActiveOrder, "their current order comes first");
        var animal = Animal(plan.Choice.AnimalId);
        if (animal is null || animal.HouseholdId != plan.HouseholdId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected animal has changed ownership");
        var kind = plan.Choice.Kind;
        if (kind is AnimalAgreementKind.Accept or AnimalAgreementKind.Decline)
        {
            if (plan.Offer is not { } offer || State.Offers.FirstOrDefault(item => item.Id == plan.Choice.OtherId) != offer ||
                offer.BuyerId != plan.Actor || !world.Agents.ContainsKey(offer.SellerId) || !ValidAnimalOffer(world, offer))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected animal offer is no longer available");
        }
        else
        {
            if (!AnimalHouseholdMember(world, plan.Actor, animal))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected animal is no longer theirs to offer or permit");
            if (kind is AnimalAgreementKind.GrantCare or AnimalAgreementKind.GrantRide or AnimalAgreementKind.RevokeCare or AnimalAgreementKind.RevokeRide)
            {
                var riding = kind is AnimalAgreementKind.GrantRide or AnimalAgreementKind.RevokeRide;
                var grant = kind is AnimalAgreementKind.GrantCare or AnimalAgreementKind.GrantRide;
                var permitted = (riding ? animal.RidingPermissions : animal.CarePermissions).Contains(plan.Choice.OtherId, StringComparer.Ordinal);
                if (riding && animal.Species != "horse" || permitted == grant || grant &&
                    (animal.DiedTick is not null || !AnimalRecipientIsNear(world, plan.Actor, animal, plan.Choice.OtherId)))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected permission or recipient has changed");
                if (kind == AnimalAgreementKind.RevokeRide && (animal.RiderId == plan.Choice.OtherId ? animal.RiderId : null) != plan.RiderDismount?.Actor)
                    return ActionCheck.Refuse(ActionRefusalReason.Mounted, "the selected rider has changed");
                return plan.RiderDismount is { } dismount ? CheckAnimalDismount(world, dismount) : new();
            }
            var targetYard = world.Simulation.Buildings.FirstOrDefault(item => item.InstanceId == plan.ReceivingYardId);
            if (targetYard is null || !MayOfferAnimalTrade(world, plan.Actor, animal, plan.Choice.OtherId, plan.ReceivingHouseholdId!, targetYard))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected animal or recipient is no longer available for this offer");
        }
        if (kind == AnimalAgreementKind.Accept && (world.Agents[plan.Actor].Position == animal.Position && MayLeadAnimal(world, plan.Actor, animal)) != plan.LeadAfterTransfer)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the planned leading preparation has changed");
        var receivingActor = plan.Offer?.BuyerId ?? plan.Choice.OtherId;
        var yard = world.Simulation.Buildings.FirstOrDefault(item => item.InstanceId == plan.ReceivingYardId);
        if (yard is null || yard.HouseholdId != plan.ReceivingHouseholdId || yard.Position != plan.ReceivingYardPosition ||
            world.HouseholdFor(receivingActor) != plan.ReceivingHouseholdId ||
            !HasAnimalTransferSpace(world, plan.ReceivingHouseholdId!, yard, animal, plan.Offer?.Id))
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected receiving yard is no longer available");
        var stockId = plan.Offer?.PaymentLotId ?? plan.Choice.StockId;
        if (stockId is not null && (plan.PaymentOwnerId is null || plan.PaymentKind is null ||
            world.RecheckGoods(new(GoodsUse.AnimalPayment, plan.PaymentOwnerId, GoodsOwners.One(plan.PaymentOwnerId), GoodsKinds.One(plan.PaymentKind)), stockId) is not { Quantity: var quantity } || quantity < plan.PaymentQuantity))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected payment lot is no longer available");
        return new();
    }
}
