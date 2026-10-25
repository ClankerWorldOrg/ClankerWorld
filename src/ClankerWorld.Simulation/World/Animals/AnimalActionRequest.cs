using System.Collections.ObjectModel;

namespace ClankerWorld.Simulation.World;

public enum AnimalActionFamily { Work, Product, Travel, Supply, Trip, Agreement }

public sealed record AnimalActionRequest(AnimalActionFamily Family, string CandidateId)
{
    public string Encode() => CandidateId;

    public static AnimalActionRequest? Decode(string candidate)
    {
        if (candidate == "drink_milk" || StartsWithAny(candidate, "animal:collect:", "animal:collect_hide:"))
            return new(AnimalActionFamily.Product, candidate);
        if (StartsWithAny(candidate, "animal:care:", "animal:tame:", "animal:saddle:"))
            return new(AnimalActionFamily.Work, candidate);
        if (StartsWithAny(candidate, "animal:mount:", "animal:dismount:", "animal:lead_home:"))
            return new(AnimalActionFamily.Travel, candidate);
        if (candidate.StartsWith("animal:supply:", StringComparison.Ordinal)) return new(AnimalActionFamily.Supply, candidate);
        if (candidate.StartsWith("animal:supply_trip:", StringComparison.Ordinal)) return new(AnimalActionFamily.Trip, candidate);
        if (candidate.StartsWith("animal:agreement:", StringComparison.Ordinal)) return new(AnimalActionFamily.Agreement, candidate);
        return null;
    }

    private static bool StartsWithAny(string value, params string[] prefixes) =>
        prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal));
}

public enum AnimalAgreementKind { GrantCare, RevokeCare, GrantRide, RevokeRide, OfferGift, OfferSale, Accept, Decline }

public sealed record AnimalAgreementChoice(AnimalAgreementKind Kind, string AnimalId, string OtherId, string? StockId = null)
{
    public string Action => Kind switch
    {
        AnimalAgreementKind.GrantCare => "grant_care",
        AnimalAgreementKind.RevokeCare => "revoke_care",
        AnimalAgreementKind.GrantRide => "grant_ride",
        AnimalAgreementKind.RevokeRide => "revoke_ride",
        AnimalAgreementKind.OfferGift => "offer_gift",
        AnimalAgreementKind.OfferSale => "offer_sale",
        AnimalAgreementKind.Accept => "accept",
        _ => "decline",
    };
    public string Encode() => WorldRuntime.AnimalSharedRules.AnimalAgreementId(Action, AnimalId, OtherId, StockId);
}

public sealed record AnimalAgreementActionPlan(string ActorId, AnimalAgreementChoice Choice, string HouseholdId,
    string? ReceivingHouseholdId, string? ReceivingYardId, GridPoint? ReceivingYardPosition,
    string? PaymentOwnerId, string? PaymentKind, int PaymentQuantity, AnimalTradeOffer? Offer,
    PlannedAnimalDismount? RiderDismount, bool LeadAfterTransfer) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind switch
    {
        AnimalAgreementKind.GrantCare or AnimalAgreementKind.GrantRide => "grant animal permission",
        AnimalAgreementKind.RevokeCare or AnimalAgreementKind.RevokeRide => "revoke animal permission",
        AnimalAgreementKind.OfferGift or AnimalAgreementKind.OfferSale => "offer the animal",
        AnimalAgreementKind.Accept => "accept the animal offer",
        _ => "decline the animal offer",
    };
}

public enum AnimalProductKind { Milk, Product, Hide }

public sealed record AnimalProductChoice(AnimalProductKind Kind, string? AnimalId = null)
{
    public string Encode() => Kind == AnimalProductKind.Milk ? "drink_milk" :
        WorldRuntime.AnimalSharedRules.AnimalActionId(Kind == AnimalProductKind.Product ? "collect" : "collect_hide", AnimalId!, null);
}

public sealed record AnimalProductActionPlan(string AgentId, AnimalProductChoice Choice, string StockId,
    string OwnerId, string ItemKind, string RootId, int Quantity, GridPoint Position,
    string? ReservationId, PlannedAnimalDismount? Dismount, ReadOnlyCollection<GridPoint> Route)
    : ActionPlan(AgentId, Choice.Encode())
{
    public string? DismountAnimalId => Dismount?.AnimalId;
    public override string Activity => Choice.Kind switch
    {
        AnimalProductKind.Milk => "drink the milk",
        AnimalProductKind.Product => "collect the animal product",
        _ => "collect the hide",
    };
}

public sealed record AnimalSupplySelection(string StockId, string YardId, string? AnimalId, string? Action)
{
    public string Encode() => WorldRuntime.AnimalSharedRules.AnimalSupplyId(StockId, YardId, AnimalId, Action);
}

public sealed record AnimalSupplyActionPlan(string ActorId, AnimalSupplySelection Choice, string OwnerId,
    string ItemKind, int Quantity, GridPoint Pickup, int PickupRange, bool Carried, string HouseholdId,
    GridPoint Destination, IReadOnlyList<PlannedStockMember>? Family, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "collect supplies for the animal yard";
}

public enum AnimalTravelKind { Mount, Dismount, LeadHome }

public sealed record AnimalTravelChoice(AnimalTravelKind Kind, string AnimalId)
{
    public string Action => Kind switch { AnimalTravelKind.Mount => "mount", AnimalTravelKind.Dismount => "dismount", _ => "lead_home" };
    public string Encode() => WorldRuntime.AnimalSharedRules.AnimalActionId(Action, AnimalId, null);
}

public sealed record PlannedAnimalDrop(string StockId, string OwnerId, string ItemKind, int Quantity,
    int PhysicalQuantity, IReadOnlyList<PlannedStockMember>? Family);

public sealed record PlannedAnimalDismount(string Actor, string AnimalId, GridPoint Position,
    int BaseCapacity, int CarriedQuantity, IReadOnlyList<PlannedAnimalDrop> Cargo);

public sealed record AnimalTravelActionPlan(string ActorId, AnimalTravelChoice Choice, GridPoint Position,
    string? HouseholdId, string? YardId, GridPoint? Destination, bool ReleaseLead,
    string? SaddleStockId, string? SaddleReservationId, PlannedAnimalDismount? Dismount, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind switch
    { AnimalTravelKind.Mount => "mount the horse", AnimalTravelKind.Dismount => "dismount the horse", _ => "lead the animal home" };
}

public enum AnimalTripStep { Release, Work, Deliver }

public sealed record AnimalTripChoice(string ActorId)
{
    public string Encode() => WorldRuntime.AnimalSharedRules.AnimalSupplyTripId(ActorId);
}

public sealed record AnimalTripActionPlan(string ActorId, AnimalSupplyTrip Trip, string OwnerId,
    string ItemKind, int Quantity, IReadOnlyList<PlannedStockMember>? Family, AnimalTripStep Step,
    string? HouseholdId, GridPoint? Destination, AnimalWorkActionPlan? Work, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, new AnimalTripChoice(ActorId).Encode())
{
    public override string Activity => Step == AnimalTripStep.Work ? "finish the animal work" :
        Step == AnimalTripStep.Release ? "end the partial animal supply trip" : "deliver the animal supplies";
}

public enum AnimalWorkKind { Care, Tame, Saddle }

public sealed record AnimalWorkSelection(AnimalWorkKind Kind, string AnimalId)
{
    public string Action => Kind switch { AnimalWorkKind.Care => "care", AnimalWorkKind.Tame => "tame", _ => "saddle" };
    public string Encode() => WorldRuntime.AnimalSharedRules.AnimalActionId(Action, AnimalId, null);
}

public sealed record PlannedAnimalInput(string StockId, string OwnerId, string ItemKind, int Quantity,
    string RootId, bool Carried, GridPoint? Position);

public sealed record AnimalWorkActionPlan(string ActorId, AnimalWorkSelection Choice, string? AnimalHouseholdId,
    string? AnimalYardId, GridPoint Position, IReadOnlyList<PlannedAnimalInput> Inputs,
    AnimalSupplyActionPlan? SupplyPreparation, PlannedAnimalDismount? Dismount, bool ApproachOnly,
    string? ReceivingHouseholdId, string? ReceivingYardId, GridPoint? ReceivingYardPosition,
    AnimalTamingWork? InitialTamingWork, int WorkTicks, int CareTicks, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, Choice.Encode())
{
    public string? DismountAnimalId => Dismount?.AnimalId;
    public override string Activity => Choice.Kind switch
    { AnimalWorkKind.Care => "care for the animal", AnimalWorkKind.Tame => "tame the animal", _ => "saddle the horse" };
}
