namespace ClankerWorld.Simulation.World;

internal sealed partial class AnimalSystem
{
    internal static ActorCapabilityPolicy ActionCapabilities =>
        new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);
    internal static ActorCapabilityPolicy ProductCapabilities(AnimalProductKind kind) =>
        new(kind == AnimalProductKind.Milk ? ActionLifeStages.NonInfants : ActionLifeStages.Adults,
            AllowBoat: kind == AnimalProductKind.Milk, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    internal TPlan? PlanAnimalRequest<TPlan>(IAnimalWorld world, string actor, AnimalActionRequest request) where TPlan : ActionPlan
    {
        ActionPlan? plan;
        if (request.Family == AnimalActionFamily.Agreement)
            plan = AnimalAgreements(world, actor).FirstOrDefault(item => item.Id == request.CandidateId) is { } agreement
                ? PlanAnimalAgreement(world, actor, agreement) : null;
        else if (request.CandidateId == "drink_milk") plan = PlanAnimalProduct(world, actor, new(AnimalProductKind.Milk));
        else if (request.Family == AnimalActionFamily.Trip)
            plan = State.SupplyTrips.FirstOrDefault(item => item.ActorId == actor) is { } trip ? PlanAnimalTrip(world, actor, trip) : null;
        else if (request.Family == AnimalActionFamily.Supply)
            plan = AnimalSupplyChoices(world, actor).FirstOrDefault(item => item.Id == request.CandidateId) is { } supply
                ? PlanAnimalSupply(world, actor, supply) : null;
        else if (AnimalChoices(world, actor).FirstOrDefault(item => item.Id == request.CandidateId) is { } choice)
            plan = request.Family switch
            {
                AnimalActionFamily.Product => PlanAnimalProduct(world, actor, new(choice.Action == "collect" ? AnimalProductKind.Product : AnimalProductKind.Hide, choice.AnimalId)),
                AnimalActionFamily.Work => PlanAnimalWork(world, actor, new(choice.Action == "care" ? AnimalWorkKind.Care : choice.Action == "tame" ? AnimalWorkKind.Tame : AnimalWorkKind.Saddle, choice.AnimalId)),
                _ => PlanAnimalTravel(world, actor, new(choice.Action == "mount" ? AnimalTravelKind.Mount : choice.Action == "dismount" ? AnimalTravelKind.Dismount : AnimalTravelKind.LeadHome, choice.AnimalId)),
            };
        else plan = null;
        return plan as TPlan;
    }

    internal bool FinishMissingAnimalTrip(IAnimalWorld world, string actor)
    {
        if (State.SupplyTrips.Any(trip => trip.ActorId == actor)) FinishAnimalSupplyTrip(world, actor);
        return true;
    }
}
