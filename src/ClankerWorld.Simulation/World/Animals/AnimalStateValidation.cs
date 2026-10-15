using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static void ValidateAnimalState(WorldRuntimeState state)
    {
        var world = state.AnimalWorld;
        AnimalSystem.ValidateLocalState(world);
        var society = state.Society.Society;
        var people = society.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        void KnownAgents(IEnumerable<string> ids)
        {
            if (ids.Any(id => !people.Contains(id)))
                throw new InvalidDataException("Animal agent references must name unique, known people.");
        }
        KnownAgents(world.SupplyTrips.Select(trip => trip.ActorId));
        KnownAgents(world.Animals.Select(animal => animal.RiderId).OfType<string>());
        KnownAgents(world.Animals.Select(animal => animal.LeaderId).OfType<string>());
        var inventory = society.Inventory;
        var day = state.WorldSystems!.Config.TicksPerDay;
        var map = TravelMap(state);
        foreach (var animal in world.Animals)
        {
            var definition = AnimalRules.Definition(animal.Species);
            if (animal.BornTick > society.WorldTick || animal.BornTick < -(long)definition.AdultDays * day || !map.IsPassable(animal.Position) ||
                animal.CareUntilTick > society.WorldTick + day || animal.ProductProgressTicks >= Math.Max(1, definition.ProductDays * day) ||
                animal.DiedTick is { } death && death > society.WorldTick)
                throw new InvalidDataException("An animal has invalid age, care, product progress or physical state.");
            KnownAgents(animal.CarePermissions); KnownAgents(animal.RidingPermissions);
            if (animal.HouseholdId is { } home && (!society.Households.Any(household => household.Id == home) ||
                animal.DiedTick is null && !state.WorldSimulation!.Buildings.Any(building => building.InstanceId == animal.YardId &&
                    building.HouseholdId == home && state.WorldContent!.Buildings.Any(buildingDefinition => buildingDefinition.CanonicalId == building.DefinitionId &&
                        buildingDefinition.Tags.Contains(AnimalContent.YardTag)))))
                throw new InvalidDataException("An owned animal needs its household's actual animal yard.");
            if (animal.Pregnancy is { } pregnancy && (!AnimalRules.IsAdult(animal, society.WorldTick, day) ||
                pregnancy.StartedTick > society.WorldTick || pregnancy.ProgressTicks >= definition.GestationDays * day))
                throw new InvalidDataException("An animal pregnancy has invalid parents or progress.");
            if (animal.TamingWork is { } tame && !state.Agents.Any(person => person.AgentId == tame.ActorId))
                throw new InvalidDataException("An animal taming task has invalid work or actor.");
            if (animal.LeadDestination is { } destination && !map.IsPassable(destination))
                throw new InvalidDataException("An animal cannot be ridden and led together and needs a legal lead destination.");
            foreach (var actor in new[] { animal.RiderId, animal.LeaderId }.OfType<string>())
                if (!state.Agents.Any(person => person.AgentId == actor && person.Position == animal.Position) ||
                    state.HandcartHitches!.Any(hitch => hitch.PullerId == actor) ||
                    state.BoatTransport.Boats.Any(boat => boat.Journey?.PassengerId == actor) ||
                    world.Animals.Any(other => other.Id != animal.Id && (other.RiderId == actor || other.LeaderId == actor)))
                    throw new InvalidDataException("Animal riders and leaders must share the animal's physical position without another attachment.");
            if (animal.RiderId is not null && !AnimalRules.IsAdult(animal, society.WorldTick, day))
                throw new InvalidDataException("A ridden adult horse needs a fitted saddle.");
            if (animal.ReadyProductLotId is { } productId &&
                (!inventory.Lots.Any(lot => lot.Id == productId && lot.OwnerId == animal.HouseholdId &&
                    lot.ItemKind == definition.Product && lot.Quantity == definition.ProductQuantity &&
                    lot.GroundPosition == new InventoryGroundPosition(animal.Position.X, animal.Position.Y)) ||
                 !inventory.Reservations.Any(held => held.Id == animal.ReadyProductReservationId && held.LotId == productId &&
                    held.OwnerId == animal.HouseholdId && held.Quantity == definition.ProductQuantity && held.IsExclusive &&
                    held.State == InventoryReservationState.Reserved && held.Purpose == "animal-product:" + animal.Id)))
                throw new InvalidDataException("A ready animal product requires its real, held household lot.");
            if (animal.SaddleLotId is { } saddleId &&
                (!inventory.Lots.Any(lot => lot.Id == saddleId && lot.ItemKind == "saddle" && lot.OwnerId == animal.HouseholdId && lot.Quantity == 1 &&
                    lot.GroundPosition == new InventoryGroundPosition(animal.Position.X, animal.Position.Y)) ||
                 !inventory.Reservations.Any(held => held.Id == animal.SaddleReservationId && held.LotId == saddleId && held.OwnerId == animal.HouseholdId &&
                    held.Quantity == 1 && held.IsExclusive && held.State == InventoryReservationState.Reserved && held.Purpose == "animal-saddle:" + animal.Id)))
                throw new InvalidDataException("A fitted saddle requires its real, held household lot.");
        }
        foreach (var household in society.Households)
        {
            int Used(string? yard = null) => world.Animals.Where(animal => animal.DiedTick is null && animal.HouseholdId == household.Id &&
                (yard is null || animal.YardId == yard)).Sum(animal => 1 + (animal.Pregnancy is null ? 0 : 1)) +
                world.Offers.Where(offer => offer.ReceivingHouseholdId == household.Id && (yard is null || offer.ReceivingYardId == yard))
                    .Sum(offer => 1 + (world.Animals.FirstOrDefault(animal => animal.Id == offer.AnimalId)?.Pregnancy is null ? 0 : 1));
            if (Used() > AnimalRules.PopulationCap) throw new InvalidDataException("A household has more than eight animals and reserved offspring places.");
            foreach (var yard in state.WorldSimulation!.Buildings.Where(building => building.HouseholdId == household.Id &&
                         state.WorldContent!.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId && definition.Tags.Contains(AnimalContent.YardTag))))
            {
                var definition = BuildingStorageRules.EffectiveDefinition(state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == yard.DefinitionId), yard);
                if (Used(yard.InstanceId) > Math.Min(8, definition.Width * definition.Height))
                    throw new InvalidDataException("The animal yard has no room for its animals and reserved offspring places.");
            }
        }
        foreach (var offer in world.Offers)
            if (!world.Animals.Any(animal => animal.Id == offer.AnimalId && animal.DiedTick is null && animal.HouseholdId != offer.ReceivingHouseholdId) ||
                !society.Agents.Any(person => person.Id == offer.SellerId) || !society.Agents.Any(person => person.Id == offer.BuyerId &&
                    person.HouseholdId == offer.ReceivingHouseholdId) || !state.WorldSimulation!.Buildings.Any(yard =>
                    yard.InstanceId == offer.ReceivingYardId && yard.HouseholdId == offer.ReceivingHouseholdId) ||
                offer.OfferedTick > society.WorldTick)
                throw new InvalidDataException("An animal trade offer has invalid parties, receiving space or exact payment.");
        foreach (var trip in world.SupplyTrips)
            if (!state.Agents.Any(person => person.AgentId == trip.ActorId) ||
                !inventory.Lots.Any(lot => lot.Id == trip.LotId && PersonalEquipmentRules.IsCarried(lot, trip.ActorId)) ||
                !state.WorldSimulation!.Buildings.Any(yard => yard.InstanceId == trip.YardId && (yard.HouseholdId ==
                    society.Agents.Single(person => person.Id == trip.ActorId).HouseholdId || world.Animals.Any(animal =>
                        animal.YardId == yard.InstanceId && animal.CarePermissions.Contains(trip.ActorId)))) ||
                trip.AnimalId is not null && !world.Animals.Any(animal => animal.Id == trip.AnimalId))
                throw new InvalidDataException("An animal supply trip needs its real carried goods and household yard.");
        if ((state.Instructions ?? []).Any(instruction => instruction.Task?.TargetAnimalId is { } id &&
                !world.Animals.Any(animal => animal.Id == id)))
            throw new InvalidDataException("An animal order must retain its exact known animal identity.");
    }
}
