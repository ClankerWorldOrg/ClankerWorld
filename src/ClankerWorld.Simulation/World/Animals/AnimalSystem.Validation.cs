namespace ClankerWorld.Simulation.World;

internal sealed partial class AnimalSystem
{
    private void Commit(AnimalWorldState next)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalState(next);
        State = next;
    }

    internal static bool ValidAnimalName(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 512 && !value.Any(char.IsControl);
    internal static bool ValidAnimalIdentity(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= DerivedId.MaximumLength && !value.Any(char.IsControl);
    internal static bool ValidAnimalReference(string? value) => !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);

    internal static void ValidateLocalState(AnimalWorldState world)
    {
        if (world is null || world.Animals is null || world.Offers is null || world.SupplyTrips is null ||
            world.Animals.Any(animal => animal is null) || world.Offers.Any(offer => offer is null) || world.SupplyTrips.Any(trip => trip is null))
            throw new InvalidDataException("The current checkpoint requires complete animal state.");
        if (!world.Seeded && (world.Animals.Count != 0 || world.Offers.Count != 0 || world.SupplyTrips.Count != 0))
            throw new InvalidDataException("An unseeded animal world cannot contain animal history or custody.");
        static void Unique(IEnumerable<string> source, bool shortIdentity)
        {
            var values = source.ToArray();
            if (values.Any(value => !(shortIdentity ? ValidAnimalIdentity(value) : ValidAnimalReference(value))) ||
                values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                throw new InvalidDataException("Animal identities and permissions must be unique, short identifiers.");
        }
        Unique(world.Animals.Select(animal => animal.Id), true);
        Unique(world.Offers.Select(offer => offer.Id), true);
        Unique(world.Offers.Select(offer => offer.AnimalId), true);
        Unique(world.SupplyTrips.Select(trip => trip.ActorId), false);
        Unique(world.Animals.Select(animal => animal.RiderId).OfType<string>(), false);
        Unique(world.Animals.Select(animal => animal.LeaderId).OfType<string>(), false);
        foreach (var animal in world.Animals)
        {
            if (!ValidAnimalName(animal.Name) || !ValidAnimalIdentity(animal.HerdId) || !AnimalRules.Species.Any(species => species.Id == animal.Species) ||
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
            if (animal.Pregnancy is { } pregnancy && (!ValidAnimalIdentity(pregnancy.FatherId) || animal.Sex != "female" || pregnancy.StartedTick < 0 || pregnancy.ProgressTicks < 0 ||
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
