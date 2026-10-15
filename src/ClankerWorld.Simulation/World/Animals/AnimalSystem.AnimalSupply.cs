using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
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
                    var quantity = Math.Min(wanted, Math.Min(world.AvailableLotQuantity(root), world.FreeCarryCapacity(actor)));
                    if (quantity <= 0 || !world.VesselFits(root, world.FreeCarryCapacity(actor)) ||
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
            world.RunInventoryCommand(InventorySystem.PickUpAnimalSupply(plan, source, operation));
            if (plan.Quantity < source.Quantity) source = world.Society.Inventory.GetLot(DerivedId.Create(source.Id + "#move:" + operation));
        }
        world.RunInventoryCommand(InventorySystem.HoldAnimalSupplyLoad(plan, source));
        State = State with
        {
            SupplyTrips = State.SupplyTrips.Append(new(plan.Actor, source.Id,
            plan.Choice.YardId, plan.Choice.AnimalId, plan.Choice.Action)).OrderBy(trip => trip.ActorId, StringComparer.Ordinal).ToArray()
        };
    }

    private void CompleteAnimalTripDelivery(IAnimalWorld world, AnimalTripActionPlan plan, InventoryLot lot)
    {
        world.RunInventoryCommand(InventorySystem.CompleteAnimalTripDelivery(world, plan, lot));
        FinishAnimalSupplyTrip(world, plan.Actor);
        world.AppendEvent("animal_yard_supplied", plan.Actor + ":" + plan.Trip.YardId);
    }

    internal void FinishAnimalSupplyTrip(IAnimalWorld world, string actor)
    {
        var hold = InventoryStore.CreateInventoryHold(InventoryHoldPurpose.SupplyTrip, actor);
        foreach (var lot in InventoryIndex.For(world.Society.Inventory).Lots.Where(lot => (lot.Holds ?? []).Contains(hold)))
            world.RunInventoryCommand(InventorySystem.FinishAnimalSupplyTrip(hold, lot));
        State = State with { SupplyTrips = State.SupplyTrips.Where(trip => trip.ActorId != actor).ToArray() };
    }
}
