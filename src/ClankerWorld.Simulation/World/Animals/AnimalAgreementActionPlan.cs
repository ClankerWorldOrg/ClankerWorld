namespace ClankerWorld.Simulation.World;

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
