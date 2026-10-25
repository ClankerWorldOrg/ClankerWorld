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

    internal AnimalState AdvanceWildAnimalCare(IAnimalWorld world, AnimalState animal, long tick)
    {
        if (AnimalRules.HasCare(animal, tick)) return animal;
        var definition = AnimalRules.Definition(animal.Species);
        if (animal.WildFedUntilTick <= tick)
        {
            var occupied = WildAnimalRouteOccupants(world, animal);
            var forage = WildAnimalForageSources(world.Map, world.Environment.Ecology, animal)
                .FirstOrDefault(source => world.Map.IsReachableOnFoot(animal.Position, source.Position) &&
                    world.SharedUnoccupiedRoute(animal.Position, occupied, source.Position, 1).Count > 0);
            if (forage is null) return animal;
            if (world.Map.FootDistance(animal.Position, forage.Position) > 1)
                return MoveWildAnimalToward(world, animal, forage.Position, tick, 1);
            var harvested = EcologyRules.Harvest(world.Environment.Ecology.GetResource(forage.Id), definition.DailyFeed);
            if (!harvested.IsValid || harvested.Resource is not { } remaining) return animal;
            world.ReplaceEcologyResource(forage.Id, remaining);
            world.SyncEcologyResourceStates();
            SetAnimal(animal = animal with { WildFedUntilTick = tick + AnimalDayTicks(world) });
        }
        if (animal.WildWaterUntilTick <= tick)
        {
            var occupied = WildAnimalRouteOccupants(world, animal);
            var shore = world.FreshWaterShorePositions().Where(point => !State.Animals.Any(other => other.Id != animal.Id &&
                    other.DiedTick is null && other.Position == point) && !world.Agents.Values.Any(person => person.Position == point) &&
                    world.Map.FootDistance(animal.Position, point) <= 12 &&
                    world.Map.IsReachableOnFoot(animal.Position, point)).OrderBy(point => world.Map.FootDistance(animal.Position, point))
                .ThenBy(point => point.Y).ThenBy(point => point.X)
                .Where(point => world.SharedUnoccupiedRoute(animal.Position, occupied, point, 0).Count > 0)
                .Cast<GridPoint?>().FirstOrDefault();
            if (shore is null) return animal;
            if (animal.Position != shore.Value) return MoveWildAnimalToward(world, animal, shore.Value, tick, 0);
            SetAnimal(animal = animal with { WildWaterUntilTick = tick + AnimalDayTicks(world) });
        }
        if (animal.WildFedUntilTick > tick && animal.WildWaterUntilTick > tick)
            SetAnimal(animal = animal with { CareUntilTick = Math.Min(animal.WildFedUntilTick, animal.WildWaterUntilTick) });
        return animal;
    }

    internal static IOrderedEnumerable<MapResource> WildAnimalForageSources(WorldMap map, EcologyState ecology, AnimalState animal)
    {
        var feed = AnimalRules.Definition(animal.Species).DailyFeed;
        return map.Resources.Where(source => source.NaturalObjectKind == "wild_greens" &&
                map.FootDistance(animal.Position, source.Position) <= 12 &&
                ecology.TryGetResource(source.Id, out var resource) && resource.Quantity >= feed)
            .OrderBy(source => map.FootDistance(animal.Position, source.Position)).ThenBy(source => source.Id, StringComparer.Ordinal);
    }

    internal AnimalState MoveWildAnimalToward(IAnimalWorld world, AnimalState animal, GridPoint destination, long tick, int range)
    {
        if (tick % 2 != 0 || animal.TamingWork is not null) return animal;
        var route = world.SharedUnoccupiedRoute(animal.Position, WildAnimalRouteOccupants(world, animal), destination, range);
        if (route.Count > 1) MoveAnimal(world, animal, route[1]);
        return Animal(animal.Id)!;
    }

    internal static HashSet<GridPoint> WildAnimalRouteOccupants(IAnimalWorld world, AnimalState animal) =>
        GroundOccupancy.Combine(world.Agents.Values.Select(person => person.Position), world.Ground.AnimalPositions(animal.Id));

    private void Commit(AnimalWorldState next)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalState(next, enforcePopulationCap: false);
        State = next;
    }

    internal static void ValidateLocalState(AnimalWorldState world, bool enforcePopulationCap = true)
    {
        if (world is null || world.Animals is null || world.Offers is null || world.SupplyTrips is null ||
            world.Animals.Any(animal => animal is null) || world.Offers.Any(offer => offer is null) || world.SupplyTrips.Any(trip => trip is null))
            throw new InvalidDataException("The current checkpoint requires complete animal state.");
        if (!world.Seeded && (world.Animals.Count != 0 || world.Offers.Count != 0 || world.SupplyTrips.Count != 0))
            throw new InvalidDataException("An unseeded animal world cannot contain animal history or custody.");
        static void Unique(IEnumerable<string?> source, bool shortIdentity)
        {
            var values = source.ToArray();
            if (values.Any(value => !(shortIdentity ? IdentityRules.Reference(value, DerivedId.MaximumLength) : IdentityRules.Reference(value))) ||
                values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                throw new InvalidDataException("Animal identities and permissions must be unique, short identifiers.");
        }
        Unique(world.Animals.Select(animal => animal.Id), true);
        Unique(world.Offers.Select(offer => (string?)offer.Id), true);
        Unique(world.Offers.Select(offer => offer.AnimalId), true);
        Unique(world.SupplyTrips.Select(trip => trip.ActorId), false);
        Unique(world.Animals.Select(animal => animal.RiderId).OfType<string>(), false);
        Unique(world.Animals.Select(animal => animal.LeaderId).OfType<string>(), false);
        foreach (var animal in world.Animals)
        {
            if (!BoundedText.IsValid(animal.Name, 512) || !IdentityRules.Reference(animal.HerdId, DerivedId.MaximumLength) || !AnimalRules.Species.Any(species => species.Id == animal.Species) ||
                animal.Sex is not ("female" or "male") || animal.CarePermissions is null || animal.RidingPermissions is null)
                throw new InvalidDataException("An animal has invalid species, sex, name or permissions.");
            Unique(animal.CarePermissions, false);
            Unique(animal.RidingPermissions, false);
            if (animal.CareUntilTick < 0 || animal.BreedingReadyTick < 0 || animal.WildFedUntilTick < 0 || animal.WildWaterUntilTick < 0 ||
                animal.ProductProgressTicks < 0 || animal.DiedTick is { } death && (death < 0 || animal.Pregnancy is not null || animal.RiderId is not null ||
                    animal.LeaderId is not null || animal.ReadyProductLotId is not null || animal.SaddleLotId is not null))
                throw new InvalidDataException("An animal has invalid age, care, product progress or physical state.");
            if (animal.Species != "horse" && (animal.RidingPermissions.Count != 0 || animal.RiderId is not null || animal.SaddleLotId is not null))
                throw new InvalidDataException("Animal permissions must name known people and only horses may be ridden.");
            if ((animal.HouseholdId is null) != (animal.YardId is null) || animal.HouseholdId is null &&
                (animal.SaddleLotId is not null || animal.ReadyProductLotId is not null || animal.RiderId is not null || animal.LeaderId is not null ||
                    animal.CarePermissions.Count != 0 || animal.RidingPermissions.Count != 0))
                throw new InvalidDataException("An owned animal needs its household's actual animal yard.");
            if (animal.Pregnancy is { } pregnancy && (!IdentityRules.Reference(pregnancy.FatherId, DerivedId.MaximumLength) || animal.Sex != "female" || pregnancy.StartedTick < 0 || pregnancy.ProgressTicks < 0 ||
                world.Animals.FirstOrDefault(father => father.Id == pregnancy.FatherId) is { } father && (father.Species != animal.Species || father.Sex != "male")))
                throw new InvalidDataException("An animal pregnancy has invalid parents or progress.");
            if (animal.TamingWork is { } tame && (animal.HouseholdId is not null || tame.WorkTicks is < 1 or >= AnimalRules.TamingWorkTicks))
                throw new InvalidDataException("An animal taming task has invalid work or actor.");
            if (animal.RiderId is not null && animal.LeaderId is not null || (animal.LeaderId is null) != (animal.LeadDestination is null))
                throw new InvalidDataException("An animal cannot be ridden and led together and needs a legal lead destination.");
            if ((animal.SaddleLotId is null) != (animal.SaddleReservationId is null))
                throw new InvalidDataException("A fitted saddle requires its real, held household lot.");
            if (animal.RiderId is not null && animal.SaddleLotId is null)
                throw new InvalidDataException("A ridden adult horse needs a fitted saddle.");
            if ((animal.ReadyProductLotId is null) != (animal.ReadyProductReservationId is null) ||
                animal.ReadyProductLotId is not null && !AnimalRules.HasProduct(animal))
                throw new InvalidDataException("An animal product needs its actual reserved physical lot.");
        }
        // Birth inserts the child before clearing its mother's reserved place.
        // Bound the completed advance and saves, not that intermediate record.
        if (enforcePopulationCap)
            foreach (var herd in world.Animals.Where(animal => animal.DiedTick is null && animal.HouseholdId is null).GroupBy(animal => animal.HerdId))
                if (herd.Sum(animal => 1 + (animal.Pregnancy is null ? 0 : 1)) > AnimalRules.PopulationCap)
                    throw new InvalidDataException("A wild herd exceeds eight animals and reserved offspring places.");
        foreach (var offer in world.Offers)
            if (offer.OfferedTick < 0 || (offer.PaymentLotId is null) != (offer.PaymentKind is null) || offer.PaymentQuantity != (offer.PaymentLotId is null ? 0 : 1))
                throw new InvalidDataException("An animal trade offer has invalid parties, receiving space or exact payment.");
        foreach (var trip in world.SupplyTrips)
            if ((trip.AnimalId is null) != (trip.Action is null) || trip.AnimalId is not null && trip.Action is not ("care" or "saddle"))
                throw new InvalidDataException("An animal supply trip needs its real carried goods and household yard.");
    }
}
