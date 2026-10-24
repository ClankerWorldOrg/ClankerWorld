using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

using static ClankerWorld.Simulation.World.WorldRuntime.AnimalSharedRules;

namespace ClankerWorld.Simulation.World;

internal sealed partial class AnimalSystem
{
    internal static int AnimalDayTicks(IAnimalWorld world) => world.Environment.Config.TicksPerDay;
    internal static void StageAnimalContent(IAnimalWorld world)
    {
        var packages = world.Packages;
        if (new[] { TailorVariantContent.PackageId, RestaurantVariantContent.PackageId }.Any(id =>
                !packages.Any(package => package.Manifest.PackageId == id && package.Lifecycle == ContentPackageLifecycle.Active))) return;
        world.StageBuiltInContent(AnimalContent.PackageId, RestaurantContent.PackageId, AnimalContent.Create, "animal_content_staged");
    }
    internal static string AnimalKey(string value) => DerivedId.Hash(value, hexadecimalCharacters: 24);
    internal AnimalState? Animal(string id) => State.Animals.FirstOrDefault(item => item.Id == id);
    internal void SetAnimal(AnimalState animal) => Commit(State with
    {
        Animals = State.Animals.Where(item => item.Id != animal.Id).Append(animal)
            .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
    });
    internal static PlacedBuilding? AnimalYard(IAnimalWorld world, string? household) => household is null ? null : world.HouseholdBuildingWithTag(household, AnimalContent.YardTag);
    internal static PlacedBuilding? AssignedAnimalYard(IAnimalWorld world, AnimalState animal) => world.Simulation.Buildings.FirstOrDefault(yard =>
        yard.InstanceId == animal.YardId && yard.HouseholdId == animal.HouseholdId);
    internal static int AnimalYardCapacity(IAnimalWorld world, PlacedBuilding yard)
    {
        var definition = world.Content.Buildings.Single(item => item.CanonicalId == yard.DefinitionId);
        var effective = BuildingStorageRules.EffectiveDefinition(definition, yard);
        return Math.Min(AnimalRules.PopulationCap, effective.Width * effective.Height);
    }
    internal int AnimalPlacesUsed(string household, string? yardId = null) => State.Animals
        .Where(item => item.DiedTick is null && item.HouseholdId == household && (yardId is null || item.YardId == yardId))
        .Sum(item => 1 + (item.Pregnancy is null ? 0 : 1)) + State.Offers.Where(offer =>
            offer.ReceivingHouseholdId == household && (yardId is null || offer.ReceivingYardId == yardId))
            .Sum(offer => 1 + (Animal(offer.AnimalId)?.Pregnancy is null ? 0 : 1));
    internal bool HasAnimalSpace(IAnimalWorld world, string household, PlacedBuilding yard) =>
        AnimalPlacesUsed(household) < AnimalRules.PopulationCap && AnimalPlacesUsed(household, yard.InstanceId) < AnimalYardCapacity(world, yard);
    internal static bool AnimalHouseholdMember(IAnimalWorld world, string actor, AnimalState animal) => animal.HouseholdId is not null &&
        world.IsAdult(actor) && world.HouseholdFor(actor) == animal.HouseholdId;
    internal static bool MayCareForAnimal(IAnimalWorld world, string actor, AnimalState animal) => animal.DiedTick is null && world.IsAdult(actor) &&
        (AnimalHouseholdMember(world, actor, animal) || animal.CarePermissions.Contains(actor, StringComparer.Ordinal));
    internal static bool MayRideAnimal(IAnimalWorld world, string actor, AnimalState animal) => animal.Species == "horse" &&
        animal.DiedTick is null && world.IsAdult(actor) &&
        (AnimalHouseholdMember(world, actor, animal) || animal.RidingPermissions.Contains(actor, StringComparer.Ordinal));
    internal static GridPoint[] YardTiles(IAnimalWorld world, PlacedBuilding yard) => WorldContentSimulationRules.Footprint(
        world.Content.Buildings.Single(item => item.CanonicalId == yard.DefinitionId), yard).ToArray();
    internal GridPoint? FreeAnimalYardTile(IAnimalWorld world, PlacedBuilding yard, string? animalId = null, GridPoint? near = null) => YardTiles(world, yard)
        .Where(tile => world.Map.IsPassable(tile) && (near is null || world.Map.FootDistance(tile, near.Value) <= 1) && !State.Animals.Any(item => item.Id != animalId &&
            item.DiedTick is null && item.Position == tile) && !world.Agents.Values.Any(person => person.Position == tile))
        .OrderBy(tile => tile.Y).ThenBy(tile => tile.X).Cast<GridPoint?>().FirstOrDefault();

    internal void SeedWildAnimals(IAnimalWorld world)
    {
        if (State.Seeded || world.Geography is null) return;
        var ground = world.Ground;
        var blocked = GroundOccupancy.Combine(ground.BuildingFootprints, ground.Resources, ground.RoadsAndBridgeEntrances);
        var greens = world.Map.Resources.Where(item => item.NaturalObjectKind == "wild_greens")
            .OrderBy(item => AnimalKey(world.Seed + ":animal-herd:" + item.Id), StringComparer.Ordinal).ToArray();
        var shores = world.FreshWaterShorePositions();
        var result = new List<AnimalState>();
        var usedForage = new HashSet<string>(StringComparer.Ordinal);
        foreach (var species in AnimalRules.Species)
        {
            var source = greens.FirstOrDefault(item => !usedForage.Contains(item.Id) && shores.Any(shore => world.Map.FootDistance(item.Position, shore) <= 6) &&
                world.Map.FootNeighbors(item.Position).Count(tile => world.Map.IsBuildable(tile) && !blocked.Contains(tile) && !result.Any(animal => animal.Position == tile)) >= 2);
            if (source is null) continue;
            usedForage.Add(source.Id);
            var sites = world.Map.FootNeighbors(source.Position).Where(tile => world.Map.IsBuildable(tile) && !blocked.Contains(tile) &&
                    !result.Any(item => item.Position == tile)).OrderBy(tile => tile.Y).ThenBy(tile => tile.X).Take(2).ToArray();
            for (var index = 0; index < sites.Length; index++)
                result.Add(new("animal-wild-" + species.Id + "-" + index, species.Id + " " + (index + 1),
                    species.Id, index == 0 ? "female" : "male", -(long)species.AdultDays * AnimalDayTicks(world),
                    sites[index], "herd-" + species.Id));
        }
        Commit(new(true, result.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(), []));
        world.AppendEvent(new AnimalsArrivedEventPayload(null, result.Count));
    }

    internal void AdvanceAnimals(IAnimalWorld world, long tick)
    {
        SeedWildAnimals(world);
        foreach (var original in State.Animals.ToArray())
        {
            var animal = Animal(original.Id)!;
            var definition = AnimalRules.Definition(animal.Species);
            if (animal.DiedTick is not null) continue;
            if (tick - animal.BornTick >= (long)definition.LifespanDays * AnimalDayTicks(world))
            {
                EndAnimalRide(world, animal, "old_age");
                if (animal.SaddleReservationId is { } saddleHeld)
                    world.RunInventoryCommand(InventorySystem.ReleaseDeadAnimalSaddle(saddleHeld));
                animal = Animal(animal.Id)!;
                ClearAnimalProduct(world, animal, discard: true);
                if (animal.Species != "chicken")
                    world.RunInventoryCommand(InventorySystem.CreateAnimalHide(animal, tick));
                SetAnimal(animal with
                {
                    DiedTick = tick,
                    Pregnancy = null,
                    RiderId = null,
                    LeaderId = null,
                    SaddleLotId = null,
                    SaddleReservationId = null,
                    TamingWork = null,
                    LeadDestination = null,
                    ReadyProductLotId = null,
                    ReadyProductReservationId = null,
                    ProductProgressTicks = 0
                });
                world.AppendEvent(new AnimalDiedEventPayload(null, animal.Id), animal.Position);
                continue;
            }
            if (animal.HouseholdId is null) animal = AdvanceWildAnimalCare(world, animal, tick);
            if (animal.TamingWork is { } taming && (!world.IsAdult(taming.ActorId) ||
                    world.Agents[taming.ActorId].Position != animal.Position))
                SetAnimal(animal = animal with { TamingWork = null });
            if (animal.RiderId is { } rider && (!MayRideAnimal(world, rider, animal) || !AnimalRules.HasCare(animal, tick) ||
                    !world.Agents.TryGetValue(rider, out var person) || person.Position != animal.Position || world.PassengerBoat(rider) is not null))
                EndAnimalRide(world, animal, "care_or_permission_lost");
            animal = Animal(animal.Id)!;
            if (animal.LeaderId is { } leader && (!world.IsAdult(leader) || world.HouseholdFor(leader) != animal.HouseholdId ||
                    !world.Agents.TryGetValue(leader, out var leadingPerson) || leadingPerson.Position != animal.Position))
                SetAnimal(animal = animal with { LeaderId = null, LeadDestination = null });
            if (animal.ReadyProductLotId is { } lotId && world.Society.Inventory.Lots.FirstOrDefault(lot => lot.Id == lotId)
                    is not { FreshnessBasisPoints: > 0, ConditionBasisPoints: > 0 })
            {
                ClearAnimalProduct(world, animal, discard: true);
                SetAnimal(animal = animal with { ReadyProductLotId = null, ReadyProductReservationId = null });
            }
            if (!AnimalRules.HasCare(animal, tick - 1)) { WanderAnimal(world, animal, tick); continue; }
            if (animal.HouseholdId is not null && AnimalRules.IsAdult(animal, tick, AnimalDayTicks(world)) &&
                AnimalRules.HasProduct(animal) && animal.ReadyProductLotId is null)
            {
                var progress = animal.ProductProgressTicks + 1;
                if (progress >= definition.ProductDays * AnimalDayTicks(world))
                {
                    var productId = AnimalProductId(animal.Id, tick);
                    var reservationId = AnimalProductReservationId(productId);
                    world.RunInventoryCommand(InventorySystem.CreateReservedAnimalProduct(productId, definition, animal, tick, reservationId));
                    animal = animal with { ProductProgressTicks = 0, ReadyProductLotId = productId, ReadyProductReservationId = Identity.Parse(reservationId) };
                    world.AppendEvent(new AnimalProductReadyEventPayload(null, animal.Id, definition.Product), animal.Position);
                }
                else animal = animal with { ProductProgressTicks = progress };
                SetAnimal(animal);
            }
            if (animal.Pregnancy is { } pregnancy)
            {
                var progress = pregnancy.ProgressTicks + 1;
                if (progress >= definition.GestationDays * AnimalDayTicks(world))
                {
                    var yard = animal.YardId is null ? null : world.Simulation.Buildings.FirstOrDefault(item => item.InstanceId == animal.YardId);
                    var position = yard is null ? world.Map.FootNeighbors(animal.Position).Where(tile => world.Map.IsBuildable(tile) &&
                        !world.Agents.Values.Any(person => person.Position == tile) &&
                        !State.Animals.Any(item => item.DiedTick is null && item.Position == tile)).Cast<GridPoint?>().FirstOrDefault()
                        : FreeAnimalYardTile(world, yard, near: animal.Position);
                    if (position is null) continue;
                    var childId = AnimalBornId(animal.Id, pregnancy.StartedTick);
                    SetAnimal(new(childId, definition.Id + " " + childId[^4..], definition.Id,
                        AnimalKey(childId)[0] % 2 == 0 ? "female" : "male", tick, position.Value, animal.HerdId,
                        animal.HouseholdId, animal.YardId));
                    animal = animal with { Pregnancy = null, BreedingReadyTick = tick + (long)AnimalRules.RecoveryDays * AnimalDayTicks(world) };
                    world.AppendEvent(new AnimalBornEventPayload(null, childId, animal.Id), position);
                }
                else animal = animal with { Pregnancy = pregnancy with { ProgressTicks = progress } };
                SetAnimal(animal);
            }
            else if (animal.Sex == "female" && AnimalRules.IsAdult(animal, tick, AnimalDayTicks(world)) && tick >= animal.BreedingReadyTick &&
                     !State.Offers.Any(offer => offer.AnimalId == animal.Id) && CanStartAnimalPregnancy(world, animal, tick))
            {
                var male = State.Animals.FirstOrDefault(item => item.Species == animal.Species && item.Sex == "male" &&
                    item.DiedTick is null && item.HouseholdId == animal.HouseholdId && item.YardId == animal.YardId &&
                    item.HerdId == animal.HerdId && item.RiderId is null && item.LeaderId is null &&
                    (animal.HouseholdId is null ? world.Map.FootDistance(animal.Position, item.Position) <= 1 :
                        AssignedAnimalYard(world, animal) is { } sharedYard && YardTiles(world, sharedYard).Contains(item.Position)) &&
                    AnimalRules.IsAdult(item, tick, AnimalDayTicks(world)) && AnimalRules.HasCare(item, tick));
                if (male is not null)
                {
                    SetAnimal(animal = animal with { Pregnancy = new(male.Id, 0, tick) });
                    world.AppendEvent(new AnimalBreedingStartedEventPayload(null, animal.Id, male.Id), animal.Position);
                }
            }
            WanderAnimal(world, animal, tick);
        }
        ReconcileAnimalCustody(world);
        if (SystemInvariantChecks.Enabled) ValidateLocalState(State);
    }

    internal void ReconcileAnimalCustody(IAnimalWorld world)
    {
        foreach (var animal in State.Animals.ToArray())
        {
            if (animal.RiderId is { } rider && (!MayRideAnimal(world, rider, animal) || !AnimalRules.HasCare(animal, world.WorldTick) ||
                    !world.Agents.TryGetValue(rider, out var person) || person.Position != animal.Position))
                EndAnimalRide(world, animal, "care_or_permission_lost");
            if (animal.LeaderId is { } leader && (!AnimalHouseholdMember(world, leader, animal) ||
                    world.Agents[leader].Position != animal.Position))
                SetAnimal(Animal(animal.Id)! with { LeaderId = null, LeadDestination = null });
        }
        var previousTrips = State.SupplyTrips;
        Commit(State with
        {
            Offers = State.Offers.Where((offer) => ValidAnimalOffer(world, offer)).ToArray(),
            SupplyTrips = State.SupplyTrips.Where(trip => world.IsAdult(trip.ActorId) &&
                world.Simulation.Buildings.Any(yard => yard.InstanceId == trip.YardId && MaySupplyAnimalYard(world, trip.ActorId, yard)) &&
                world.Society.Inventory.Lots.Any(lot => lot.Id == trip.LotId && PersonalEquipmentRules.IsCarried(lot, trip.ActorId))).ToArray()
        });
        foreach (var trip in previousTrips.Where(trip => !State.SupplyTrips.Contains(trip)))
            FinishAnimalSupplyTrip(world, trip.ActorId);
    }

    internal bool CanStartAnimalPregnancy(IAnimalWorld world, AnimalState animal, long tick)
    {
        if (animal.RiderId is not null || animal.LeaderId is not null) return false;
        if (animal.HouseholdId is null)
            return State.Animals.Where(item => item.DiedTick is null && item.HouseholdId is null && item.HerdId == animal.HerdId)
                .Sum(item => 1 + (item.Pregnancy is null ? 0 : 1)) < AnimalRules.PopulationCap;
        var yard = AssignedAnimalYard(world, animal);
        if (yard is null || !YardTiles(world, yard).Contains(animal.Position) || !HasAnimalSpace(world, animal.HouseholdId, yard)) return false;
        var need = State.Animals.Where(item => item.HouseholdId == animal.HouseholdId && item.DiedTick is null)
            .Sum(item => AnimalRules.Definition(item.Species).DailyFeed * ((AnimalRules.HasCare(item, tick) ? 0 : 1) + (item.Pregnancy is null ? 0 : 1))) +
            AnimalRules.Definition(animal.Species).DailyFeed;
        var inventory = world.Society.Inventory;
        var index = InventoryIndex.For(inventory);
        var stock = inventory.Lots.Where(lot => lot.OwnerId == animal.HouseholdId &&
            lot.StorageBuildingId == yard.InstanceId && AnimalRules.IsFeed(lot.ItemKind) &&
            InventoryRules.HasUsableContainer(index, lot)).Sum(world.AvailableLotQuantity);
        var waterNeed = State.Animals.Where(item => item.HouseholdId == animal.HouseholdId && item.DiedTick is null)
            .Sum(item => AnimalRules.Definition(item.Species).DailyWater * ((AnimalRules.HasCare(item, tick) ? 0 : 1) + (item.Pregnancy is null ? 0 : 1))) +
            AnimalRules.Definition(animal.Species).DailyWater;
        return stock >= need && inventory.Lots.Where(lot => lot.OwnerId == animal.HouseholdId &&
            lot.StorageBuildingId == yard.InstanceId && lot.ItemKind == InventoryContainerRules.FreshWater &&
            InventoryRules.HasUsableContainer(index, lot)).Sum(world.AvailableLotQuantity) >= waterNeed;
    }

    internal static void ClearAnimalProduct(IAnimalWorld world, AnimalState animal, bool discard)
    {
        if (animal.ReadyProductReservationId is { } reservationId && world.Society.Inventory.Reservations.Any(item =>
                item.Id == reservationId && item.State is InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed))
            world.RunInventoryCommand(InventorySystem.ReleaseAnimalProductClaim(reservationId));
        if (discard && animal.ReadyProductLotId is { } lotId && world.Society.Inventory.Lots.FirstOrDefault(item => item.Id == lotId) is { } lot)
            world.RunInventoryCommand(InventorySystem.DiscardOldAnimalProduct(lot));
    }

    internal void WanderAnimal(IAnimalWorld world, AnimalState animal, long tick)
    {
        if (animal.HouseholdId is null || animal.RiderId is not null || animal.LeaderId is not null || animal.ReadyProductLotId is not null ||
            tick % 12 != 0 || AssignedAnimalYard(world, animal) is not { } yard || !YardTiles(world, yard).Contains(animal.Position)) return;
        var tiles = YardTiles(world, yard).ToHashSet();
        var next = world.Map.FootNeighbors(animal.Position).Where(tile => tiles.Contains(tile) && world.Map.CanFootStep(animal.Position, tile) &&
            !world.Agents.Values.Any(person => person.Position == tile) && !State.Animals.Any(item => item.DiedTick is null && item.Id != animal.Id && item.Position == tile))
            .OrderBy(tile => AnimalKey(animal.Id + ":" + tick + ":" + tile), StringComparer.Ordinal).Cast<GridPoint?>().FirstOrDefault();
        if (next is { } position) MoveAnimal(world, animal, position);
    }
}

