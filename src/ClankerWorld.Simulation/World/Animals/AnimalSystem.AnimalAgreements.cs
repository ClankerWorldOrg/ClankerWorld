using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
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
        if (!world.AdultResident(actor)) yield break;
        foreach (var animal in State.Animals.Where(animal => AnimalHouseholdMember(world, actor, animal)))
        {
            foreach (var recipient in world.Agents.Values.Where(person => animal.DiedTick is null &&
                         person.AgentId != actor && world.AdultResident(person.AgentId) &&
                         world.HouseholdFor(person.AgentId) != animal.HouseholdId &&
                         world.IsWithinInteractionRange(world.Agents[actor].Position, person.Position, ResourceInteractionRange))
                         .OrderBy(person => person.AgentId, StringComparer.Ordinal).Take(8))
            {
                yield return new(animal.CarePermissions.Contains(recipient.AgentId) ? "revoke_care" : "grant_care", animal.Id, recipient.AgentId);
                if (animal.Species == "horse") yield return new(animal.RidingPermissions.Contains(recipient.AgentId) ? "revoke_ride" : "grant_ride", animal.Id, recipient.AgentId);
                if (animal.RiderId is not null || animal.LeaderId is not null || animal.ReadyProductLotId is not null ||
                    State.Offers.Any(offer => offer.AnimalId == animal.Id) ||
                    world.HouseholdFor(recipient.AgentId) is not { } home || AnimalYard(world, home) is not { } yard ||
                    !HasAnimalTransferSpace(world, home, yard, animal) ||
                    !world.IsWithinInteractionRange(world.Agents[actor].Position, animal.Position, ResourceInteractionRange) ||
                    !world.IsWithinInteractionRange(recipient.Position, animal.Position, ResourceInteractionRange)) continue;
                yield return new("offer_gift", animal.Id, recipient.AgentId);
                foreach (var lot in world.Society.Inventory.Lots.Where(lot => lot.OwnerId == recipient.AgentId &&
                             PersonalEquipmentRules.IsCarried(lot, recipient.AgentId) && lot.ContainerLotId is null &&
                             lot.DeliveryBuildingId is null && !InventoryContainerRules.IsContainer(lot.ItemKind) && world.AvailableLotQuantity(lot) > 0 &&
                             !PersonalEquipmentRules.IsSelected(recipient.Equipment, lot.Id)).OrderBy(lot => lot.Id, StringComparer.Ordinal).Take(4))
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
            !world.AdultResident(offer.BuyerId) || world.HouseholdFor(offer.BuyerId) != offer.ReceivingHouseholdId ||
            animal.HouseholdId == offer.ReceivingHouseholdId || world.WorldTick - offer.OfferedTick >= AnimalDayTicks(world) ||
            !HasAnimalTransferSpace(world, offer.ReceivingHouseholdId, yard, animal, offer.Id) ||
            !world.IsWithinInteractionRange(world.Agents[offer.SellerId].Position, animal.Position, ResourceInteractionRange) ||
            !world.IsWithinInteractionRange(world.Agents[offer.BuyerId].Position, animal.Position, ResourceInteractionRange)) return false;
        return offer.PaymentLotId is null || world.Society.Inventory.Lots.Any(lot => lot.Id == offer.PaymentLotId &&
            lot.OwnerId == offer.BuyerId && lot.ItemKind == offer.PaymentKind && PersonalEquipmentRules.IsCarried(lot, offer.BuyerId) &&
            lot.ContainerLotId is null && lot.DeliveryBuildingId is null && world.AvailableLotQuantity(lot) >= offer.PaymentQuantity &&
            !PersonalEquipmentRules.IsSelected(world.Agents[offer.BuyerId].Equipment, lot.Id));
    }

    internal void AddAnimalPermissionAndTradeCandidates(IAnimalWorld world, List<CognitionCandidate> candidates, string actor)
    {
        foreach (var agreement in AnimalAgreements(world, actor).DistinctBy(item => item.Id).Take(48))
        {
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
            candidates.Add(new(agreement.Id, description, 190, animal.Id));
        }
    }

    internal bool ApplyAnimalConsentDecision(IAnimalWorld world, SocietyCognitionDispatchResult decision)
    {
        var selected = decision.Admission.Intention;
        if (selected is null || !selected.CandidateId.StartsWith("animal:agreement:", StringComparison.Ordinal)) return false;
        if (!decision.Admission.Accepted || decision.Admission.FellBack || selected.Provider != DecisionProviderKind.LargeLanguageModel ||
            selected.AgentId != decision.AgentId || world.PendingInstructionFor(decision.AgentId)?.Kind == OwnerInstructionKind.MustDo) return true;
        var actor = decision.AgentId;
        var agreement = AnimalAgreements(world, actor).FirstOrDefault(agreement => agreement.Id == selected.CandidateId);
        if (agreement is null) return true;
        var animal = Animal(agreement.AnimalId)!;
        if (agreement.Action.StartsWith("grant_", StringComparison.Ordinal) || agreement.Action.StartsWith("revoke_", StringComparison.Ordinal))
        {
            var riding = agreement.Action.EndsWith("ride", StringComparison.Ordinal);
            var grant = agreement.Action.StartsWith("grant_", StringComparison.Ordinal);
            var permissions = (riding ? animal.RidingPermissions : animal.CarePermissions).Where(id => id != agreement.OtherId)
                .Concat(grant ? [agreement.OtherId] : Array.Empty<string>()).Order(StringComparer.Ordinal).ToArray();
            SetAnimal(riding ? animal with { RidingPermissions = permissions } : animal with { CarePermissions = permissions });
            if (riding && !grant && animal.RiderId == agreement.OtherId) EndAnimalRide(world, Animal(animal.Id)!, "permission_revoked");
            world.AppendEvent("animal_permission_changed", actor + ":" + animal.Id + ":" + agreement.Action + ":" + agreement.OtherId);
        }
        else if (agreement.Action is "offer_sale" or "offer_gift")
        {
            var home = world.HouseholdFor(agreement.OtherId)!;
            var payment = agreement.LotId is null ? null : world.Society.Inventory.GetLot(agreement.LotId);
            var offer = new AnimalTradeOffer(AnimalOfferId(actor, animal.Id, world.WorldTick, world.NextEventId),
                animal.Id, actor, agreement.OtherId, home, AnimalYard(world, home)!.InstanceId, payment?.ItemKind, payment is null ? 0 : 1, world.WorldTick)
            { PaymentLotId = payment?.Id };
            State = State with { Offers = State.Offers.Append(offer).OrderBy(offer => offer.Id, StringComparer.Ordinal).ToArray() };
            world.AppendEvent("animal_trade_offered", offer.Id + ":" + animal.Id + ":" + (payment?.Id ?? "gift"));
        }
        else
        {
            var offer = State.Offers.Single(offer => offer.Id == agreement.OtherId);
            if (agreement.Action == "accept")
            {
                if (offer.PaymentLotId is { } payment)
                    world.ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory, DerivedId.Create(offer.Id + "-payment"), actor,
                        animal.HouseholdId!, payment, offer.PaymentQuantity, "animal_sale_payment",
                        destinationGroundPosition: new(animal.Position.X, animal.Position.Y)));
                if (animal.SaddleReservationId is { } saddleHeld)
                    world.ApplyInventoryTransition(inventory => InventoryAuthority.ReleaseReservation(inventory, saddleHeld, "animal_transferred"));
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
                    LeaderId = world.Agents[actor].Position == animal.Position && MayLeadAnimal(world, actor, animal) ? actor : null,
                    LeadDestination = world.Agents[actor].Position == animal.Position && MayLeadAnimal(world, actor, animal) ?
                        world.Simulation.Buildings.Single(yard => yard.InstanceId == offer.ReceivingYardId).Position : null
                });
                world.AppendEvent("animal_transferred", offer.Id + ":" + animal.Id + ":" + offer.ReceivingHouseholdId, animal.Position);
            }
            State = State with { Offers = State.Offers.Where(item => item.Id != offer.Id).ToArray() };
        }
        return true;
    }
}

