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
        var water = world.FindGoods(new(GoodsUse.ConsumeAt, actor, new GoodsOwners([actor, household]),
            GoodsKinds.One(InventoryContainerRules.FreshWater))).Matches;
        var sources = world.FindGoods(request).Matches.Select(match => match.Lot).Where(lot => lot.ContainerLotId is null &&
            (lot.OwnerId == actor && PersonalEquipmentRules.IsCarried(lot, actor) || lot.OwnerId == household &&
                (world.HouseholdFor(actor) == household || lot.StorageBuildingId == yard.InstanceId || PersonalEquipmentRules.IsCarried(lot, actor))) &&
            (lot.StorageBuildingId is null || world.CanRemoveWorkstationStock(inventory, lot, 1))).ToArray();
        foreach (var animal in animals.OrderBy(animal => animal.Id, StringComparer.Ordinal))
        {
            var definition = AnimalRules.Definition(animal.Species);
            var carriedWater = AnimalSuppliesAtHand(world, actor, animal).Where(lot =>
                lot.ItemKind == InventoryContainerRules.FreshWater && lot.ContainerLotId is not null &&
                PersonalEquipmentRules.IsPhysicallyCarried(inventory, lot, actor)).Sum(world.AvailableLotQuantity);
            var actions = new List<string>();
            if (!AnimalRules.HasCare(animal, world.WorldTick)) actions.Add("care");
            if (animal.Species == "horse" && animal.SaddleLotId is null && AnimalHouseholdMember(world, actor, animal)) actions.Add("saddle");
            foreach (var action in actions)
                foreach (var root in sources.Where(lot => !PersonalEquipmentRules.IsCarried(lot, actor)))
                {
                    var wanted = action switch
                    {
                        "saddle" => root.ItemKind == "saddle" && root.OwnerId == household ? 1 : 0,
                        _ => AnimalRules.IsFeed(root.ItemKind) ? Math.Max(0, definition.DailyFeed - AnimalSuppliesAtHand(world, actor, animal)
                            .Where(lot => AnimalRules.IsFeed(lot.ItemKind) && PersonalEquipmentRules.IsPhysicallyCarried(inventory, lot, actor)).Sum(world.AvailableLotQuantity)) :
                            root.ItemKind == InventoryContainerRules.WaterJug && water.Any(content => content.Root.Id == root.Id) &&
                            carriedWater < definition.DailyWater ? 1 : 0,
                    };
                    var quantity = Math.Min(wanted, Math.Min(world.AvailableLotQuantity(root), world.FreeCarryCapacity(actor)));
                    if (quantity <= 0 || !world.VesselFits(root, world.FreeCarryCapacity(actor)) ||
                        AnimalRules.IsFeed(root.ItemKind) && !AnimalFeedMayBeSpent(world, actor, animal, root, quantity)) continue;
                    yield return new(root, yard, quantity, animal.Id, action);
                }
        }
        if (animals.Length == 0) yield break;
        var feedTarget = Math.Min(8, animals.Sum(animal => AnimalRules.Definition(animal.Species).DailyFeed *
            (1 + (animal.Pregnancy is null ? 0 : 1))) + 2);
        var feedStock = inventory.Lots.Where(lot => lot.StorageBuildingId == yard.InstanceId && AnimalRules.IsFeed(lot.ItemKind)).Sum(world.AvailableLotQuantity);
        var hasWater = water.Any(match => match.Lot.StorageBuildingId == yard.InstanceId && match.Quantity >= 2);
        foreach (var root in sources.Where(lot => lot.StorageBuildingId != yard.InstanceId))
        {
            var wanted = AnimalRules.IsFeed(root.ItemKind) ? feedTarget - feedStock :
                !hasWater && root.ItemKind == InventoryContainerRules.WaterJug && water.Any(content => content.Root.Id == root.Id) ? 1 : 0;
            var quantity = Math.Min(wanted, Math.Min(world.AvailableLotQuantity(root), world.DestinationRoom(yard.InstanceId)));
            var carried = PersonalEquipmentRules.IsCarried(root, actor);
            var capacity = carried ? world.FreeCarryCapacity(actor) : world.PickupCarryCapacity(actor, root, yard.Position);
            if (carried && world.FindUnoccupiedRoute(actor, world.Agents[actor].Position, yard.Position, 0).Count == 0) continue;
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
            candidates.Add(new(AnimalSupplyTripId(actor), trip.AnimalId is null ?
                "Carry the collected feed or jug to the household animal yard." : "Carry the collected supply to the animal and finish its care task.", 14, trip.AnimalId ?? trip.YardId));
        else foreach (var choice in AnimalSupplyChoices(world, actor).Take(12))
            candidates.Add(new(choice.Id, $"Collect physical {choice.Lot.ItemKind.Replace('_', ' ')} for " +
                (choice.AnimalId is null ? "the animal yard supply stock." : Animal(choice.AnimalId)!.Name + "."), 14,
                choice.AnimalId ?? choice.Yard.InstanceId));
    }

    internal bool ApplyAnimalSupplyCandidate(IAnimalWorld world, string actor, string id)
    {
        if (id.StartsWith("animal:supply_trip:", StringComparison.Ordinal))
        {
            if (State.SupplyTrips.FirstOrDefault(trip => trip.ActorId == actor) is not { } trip) return true;
            var inventory = world.Society.Inventory;
            var lot = inventory.Lots.FirstOrDefault(lot => lot.Id == trip.LotId && PersonalEquipmentRules.IsCarried(lot, actor));
            var yard = world.Simulation.Buildings.FirstOrDefault(building => building.InstanceId == trip.YardId && MaySupplyAnimalYard(world, actor, building));
            if (lot is null || yard is null || !world.IsAdult(actor)) { FinishAnimalSupplyTrip(actor); return true; }
            if (trip.AnimalId is { } animalId)
            {
                var animal = Animal(animalId);
                if (animal is null || !MayCareForAnimal(world, actor, animal)) { FinishAnimalSupplyTrip(actor); return true; }
                _ = ApplyAnimalCandidate(world, actor, new AnimalChoice(trip.Action!, animal.Id).Id);
                if (trip.Action == "care" && AnimalRules.HasCare(Animal(animal.Id)!, world.WorldTick) ||
                    trip.Action == "saddle" && Animal(animal.Id)!.SaddleLotId is not null) FinishAnimalSupplyTrip(actor);
                // A partial feed/water pickup needs the other ingredient before walking to the animal.
                if (trip.Action == "care" && AnimalCareInputs(world, actor, animal, AnimalRules.Definition(animal.Species).DailyFeed,
                        AnimalRules.Definition(animal.Species).DailyWater) is null) FinishAnimalSupplyTrip(actor);
                return true;
            }
            if (world.Agents[actor].Position != yard.Position) { world.MoveToward(actor, world.Agents[actor], yard.Position, "animal_yard_supply"); return true; }
            var room = world.DestinationRoom(yard.InstanceId, lot);
            var quantity = InventoryContainerRules.IsContainer(lot.ItemKind) ? world.VesselFits(lot, room) ? 1 : 0 :
                Math.Min(world.AvailableLotQuantity(lot), room);
            if (quantity <= 0) return true;
            world.ApplyInventoryTransition(current => lot.OwnerId == yard.HouseholdId ? InventoryAuthority.Relocate(current,
                "animal-deliver-" + world.WorldTick + "-" + world.NextEventId, lot.Id, lot.OwnerId, quantity, storageBuildingId: yard.InstanceId) :
                InventoryAuthority.Transfer(current, "animal-donate-" + world.WorldTick + "-" + world.NextEventId, lot.OwnerId, yard.HouseholdId!,
                    lot.Id, quantity, "animal_yard_supplied", destinationStorageBuildingId: yard.InstanceId));
            FinishAnimalSupplyTrip(actor);
            world.AppendEvent(new AnimalYardSuppliedEventPayload(actor, yard.InstanceId));
            return true;
        }
        if (!id.StartsWith("animal:supply:", StringComparison.Ordinal)) return false;
        var choice = AnimalSupplyChoices(world, actor).FirstOrDefault(choice => choice.Id == id);
        if (choice is null) return true;
        var source = choice.Lot;
        var carried = PersonalEquipmentRules.IsCarried(source, actor);
        if (!carried)
        {
            var position = world.HouseholdStockPosition(source);
            if (!world.IsWithinInteractionRange(world.Agents[actor].Position, position, HouseholdStockInteractionRange(source)))
            { world.MoveToward(actor, world.Agents[actor], position, "animal_supply_pickup", HouseholdStockInteractionRange(source)); return true; }
        }
        var pickupRequest = new GoodsRequest(GoodsUse.Collect, actor,
            GoodsOwners.One(source.OwnerId), GoodsKinds.One(source.ItemKind));
        if (world.RecheckGoods(pickupRequest, source.Id) is not { } pickup || pickup.Quantity < choice.Quantity) return true;
        // The trip follows the chosen physical load, including when it was already carried.
        if (!carried || choice.Quantity < source.Quantity)
        {
            var operation = AnimalPickupId(actor, world.WorldTick, world.NextEventId);
            world.ApplyInventoryTransition(current => InventoryAuthority.Relocate(current, operation, source.Id, source.OwnerId,
                choice.Quantity, carrierId: actor));
            if (choice.Quantity < source.Quantity) source = world.Society.Inventory.GetLot(DerivedId.Create(source.Id + "#move:" + operation));
        }
        State = State with
        {
            SupplyTrips = State.SupplyTrips.Append(new(actor, source.Id,
            choice.Yard.InstanceId, choice.AnimalId, choice.Action)).OrderBy(trip => trip.ActorId, StringComparer.Ordinal).ToArray()
        };
        return true;
    }

    internal void FinishAnimalSupplyTrip(string actor) => State = State with
    { SupplyTrips = State.SupplyTrips.Where(trip => trip.ActorId != actor).ToArray() };
}

