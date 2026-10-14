namespace ClankerWorld.Simulation.World;

internal sealed partial class AnimalSystem
{
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
