using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

using static ClankerWorld.Simulation.World.WorldRuntime.AnimalSharedRules;

namespace ClankerWorld.Simulation.World;

internal sealed partial class AnimalSystem
{
    internal sealed record AnimalAgreement(string Action, string AnimalId, string OtherId, string? LotId = null)
    {
        public string Id => AnimalAgreementId(Action, AnimalId, OtherId, LotId);
    }

    internal IEnumerable<AnimalAgreement> AnimalAgreements(IAnimalWorld world, string actor)
    {
        if (!world.IsAdult(actor)) yield break;
        foreach (var animal in State.Animals.Where(animal => AnimalHouseholdMember(world, actor, animal)))
        {
            var permissionRecipients = 0;
            var tradeRecipients = 0;
            foreach (var recipient in world.Agents.Values.Where(person => animal.DiedTick is null && AnimalRecipientIsNear(world, actor, animal, person.AgentId))
                         .OrderBy(person => person.AgentId, StringComparer.Ordinal))
            {
                if (permissionRecipients++ < 8)
                {
                    yield return new(animal.CarePermissions.Contains(recipient.AgentId) ? "revoke_care" : "grant_care", animal.Id, recipient.AgentId);
                    if (animal.Species == "horse") yield return new(animal.RidingPermissions.Contains(recipient.AgentId) ? "revoke_ride" : "grant_ride", animal.Id, recipient.AgentId);
                }
                if (world.HouseholdFor(recipient.AgentId) is not { } home || AnimalYard(world, home) is not { } yard ||
                    !MayOfferAnimalTrade(world, actor, animal, recipient.AgentId, home, yard)) continue;
                if (tradeRecipients++ >= 8) continue;
                yield return new("offer_gift", animal.Id, recipient.AgentId);
                foreach (var lot in world.AnimalPayments(recipient.AgentId).Take(4).Select(match => match.Lot))
                    yield return new("offer_sale", animal.Id, recipient.AgentId, lot.Id);
            }
            // Revocation stays available after the permitted adult moves away.
            foreach (var permission in animal.CarePermissions) yield return new("revoke_care", animal.Id, permission);
            foreach (var permission in animal.RidingPermissions) yield return new("revoke_ride", animal.Id, permission);
        }
        foreach (var offer in State.Offers.Where(offer => offer.BuyerId == actor && ValidAnimalOffer(world, offer)))
        {
            yield return new("accept", offer.AnimalId, offer.Id);
            yield return new("decline", offer.AnimalId, offer.Id);
        }
    }

    internal bool HasAnimalTransferSpace(IAnimalWorld world, string household, PlacedBuilding yard, AnimalState animal, string? ignoringOffer = null)
    {
        var reserved = ignoringOffer is null ? 0 : 1 + (animal.Pregnancy is null ? 0 : 1);
        var required = 1 + (animal.Pregnancy is null ? 0 : 1);
        return AnimalPlacesUsed(household) - reserved + required <= AnimalRules.PopulationCap &&
            AnimalPlacesUsed(household, yard.InstanceId) - reserved + required <= AnimalYardCapacity(world, yard);
    }

    internal bool ValidAnimalOffer(IAnimalWorld world, AnimalTradeOffer offer)
    {
        var animal = Animal(offer.AnimalId);
        var yard = world.Simulation.Buildings.FirstOrDefault(building => building.InstanceId == offer.ReceivingYardId &&
            building.HouseholdId == offer.ReceivingHouseholdId && world.Content.Buildings.Any(definition =>
                definition.CanonicalId == building.DefinitionId && definition.Tags.Contains(AnimalContent.YardTag)));
        if (animal is null || animal.DiedTick is not null || animal.RiderId is not null || animal.LeaderId is not null ||
            animal.ReadyProductLotId is not null || yard is null || !AnimalHouseholdMember(world, offer.SellerId, animal) ||
            !world.IsAdult(offer.BuyerId) || world.HouseholdFor(offer.BuyerId) != offer.ReceivingHouseholdId ||
            animal.HouseholdId == offer.ReceivingHouseholdId || world.WorldTick - offer.OfferedTick >= AnimalDayTicks(world) ||
            !HasAnimalTransferSpace(world, offer.ReceivingHouseholdId, yard, animal, offer.Id) ||
            !world.IsWithinInteractionRange(world.Agents[offer.SellerId].Position, animal.Position, ResourceInteractionRange) ||
            !world.IsWithinInteractionRange(world.Agents[offer.BuyerId].Position, animal.Position, ResourceInteractionRange)) return false;
        return offer.PaymentLotId is null || world.RecheckGoods(new(GoodsUse.AnimalPayment, offer.BuyerId,
            GoodsOwners.One(offer.BuyerId), GoodsKinds.One(offer.PaymentKind!)), offer.PaymentLotId) is { Quantity: var quantity } && quantity >= offer.PaymentQuantity;
    }

    internal void AddAnimalPermissionAndTradeCandidates(IAnimalWorld world, List<CognitionCandidate> candidates, string actor)
    {
        foreach (var plan in AnimalAgreements(world, actor).DistinctBy(item => item.Id).Select(agreement => PlanAnimalAgreement(world, actor, agreement))
            .Where(plan => plan is not null && CheckAnimalAgreementPlan(world, plan).Allowed).Cast<AnimalAgreementActionPlan>().Take(48))
        {
            var agreement = new AnimalAgreement(plan.Choice.Action, plan.Choice.AnimalId, plan.Choice.OtherId, plan.Choice.StockId);
            var animal = Animal(agreement.AnimalId)!;
            var name = world.Society.Agents.FirstOrDefault(person => person.Id == agreement.OtherId)?.Name ?? agreement.OtherId;
            var offer = State.Offers.FirstOrDefault(offer => offer.Id == agreement.OtherId);
            var description = agreement.Action switch
            {
                "grant_care" => $"Allow {name} to care for {animal.Name} using its animal-yard supplies and collect its household products.",
                "grant_ride" => $"Allow {name} to ride {animal.Name}; this grants no ownership or stock access.",
                "revoke_care" => $"Revoke {name}'s care permission for {animal.Name}.",
                "revoke_ride" => $"Revoke {name}'s riding permission for {animal.Name}.",
                "offer_gift" => $"Offer the specific animal {animal.Name} as a gift to {name}'s household; they must accept.",
                "offer_sale" => $"Offer {animal.Name} to {name}'s household for one {world.Society.Inventory.GetLot(agreement.LotId!).ItemKind.Replace('_', ' ')} from their specific carried lot; they must accept.",
                "accept" => $"Accept {animal.Name} for your household and lead it home, " +
                    (offer!.PaymentKind is null ? "as an offered gift." : $"paying exactly {offer.PaymentQuantity} {offer.PaymentKind.Replace('_', ' ')} from the named carried lot."),
                _ => $"Decline the offer for {animal.Name}.",
            };
            candidates.Add(new(agreement.Id, description, 190, animal.Id) { ActionPlan = plan });
        }
    }

    internal bool TryPlanAnimalConsentDecision(IAnimalWorld world, SocietyCognitionDispatchResult decision, out AnimalAgreementActionPlan? plan)
    {
        plan = null;
        var selected = decision.Admission.Intention;
        if (selected is null || !selected.CandidateId.StartsWith("animal:agreement:", StringComparison.Ordinal)) return false;
        if (!decision.Admission.Accepted || decision.Admission.FellBack || selected.Provider != DecisionProviderKind.LargeLanguageModel ||
            selected.AgentId != decision.AgentId || world.PendingInstructionFor(decision.AgentId)?.Kind == OwnerInstructionKind.Order) return true;
        var actor = decision.AgentId;
        plan = decision.ActionPlan as AnimalAgreementActionPlan;
        if (plan is null && AnimalAgreements(world, actor).FirstOrDefault(item => item.Id == selected.CandidateId) is { } current)
            plan = PlanAnimalAgreement(world, actor, current);
        return true;
    }

    internal void CompleteAnimalAgreement(IAnimalWorld world, AnimalAgreementActionPlan plan)
    {
        var actor = plan.Actor;
        var agreement = new AnimalAgreement(plan.Choice.Action, plan.Choice.AnimalId, plan.Choice.OtherId, plan.Choice.StockId);
        var animal = Animal(agreement.AnimalId)!;
        if (agreement.Action.StartsWith("grant_", StringComparison.Ordinal) || agreement.Action.StartsWith("revoke_", StringComparison.Ordinal))
        {
            var riding = agreement.Action.EndsWith("ride", StringComparison.Ordinal);
            var grant = agreement.Action.StartsWith("grant_", StringComparison.Ordinal);
            var permissions = (riding ? animal.RidingPermissions : animal.CarePermissions).Where(id => id != agreement.OtherId)
                .Concat(grant ? [agreement.OtherId] : Array.Empty<string>()).Order(StringComparer.Ordinal).ToArray();
            SetAnimal(riding ? animal with { RidingPermissions = permissions } : animal with { CarePermissions = permissions });
            if (plan.RiderDismount is { } dismount) CompleteAnimalDismount(world, dismount, "permission_revoked");
            world.AppendEvent(new AnimalPermissionChangedEventPayload(actor, animal.Id, agreement.Action, agreement.OtherId));
        }
        else if (agreement.Action is "offer_sale" or "offer_gift")
        {
            var home = plan.ReceivingHouseholdId!;
            var payment = agreement.LotId is null ? null : world.Society.Inventory.GetLot(agreement.LotId);
            var offer = new AnimalTradeOffer(Identity.Parse(AnimalOfferId(actor, animal.Id, world.WorldTick, world.NextEventId)),
                animal.Id, actor, agreement.OtherId, home, plan.ReceivingYardId!, payment?.ItemKind, payment is null ? 0 : 1, world.WorldTick)
            { PaymentLotId = payment?.Id };
            Commit(State with { Offers = State.Offers.Append(offer).OrderBy(offer => offer.Id.Value, StringComparer.Ordinal).ToArray() });
            world.AppendEvent(new AnimalTradeOfferedEventPayload(actor, offer.Id, animal.Id, (payment?.Id ?? "gift")));
        }
        else
        {
            var offer = plan.Offer!;
            if (agreement.Action == "accept")
            {
                if (offer.PaymentLotId is { } payment)
                    world.RunInventoryCommand(InventorySystem.PayForAnimalTransfer(offer, actor, animal, payment));
                if (animal.SaddleReservationId is { } saddleHeld)
                    world.RunInventoryCommand(InventorySystem.ReleaseTransferredSaddle(saddleHeld));
                SetAnimal(animal with
                {
                    HouseholdId = offer.ReceivingHouseholdId,
                    YardId = offer.ReceivingYardId,
                    HerdId = AnimalHerdId(offer.ReceivingHouseholdId),
                    CarePermissions = [],
                    RidingPermissions = [],
                    SaddleLotId = null,
                    SaddleReservationId = null,
                    TamingWork = null,
                    LeaderId = plan.LeadAfterTransfer ? actor : null,
                    LeadDestination = plan.LeadAfterTransfer ?
                        plan.ReceivingYardPosition : null
                });
                world.AppendEvent(new AnimalTransferredEventPayload(actor, offer.Id, animal.Id, offer.ReceivingHouseholdId), animal.Position);
            }
            Commit(State with { Offers = State.Offers.Where(item => item.Id != offer.Id).ToArray() });
        }
    }

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
