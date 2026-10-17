using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string DirectBarterPrefix = "direct-barter:";

    private bool HasTradeResponse(string actor) => !NeedsUrgentWarmth(agents[actor]) && society.Checkpoint.Inventory.Offers.Any(offer =>
        offer.Id.Value.StartsWith(DirectBarterPrefix, StringComparison.Ordinal) &&
        offer.State == DirectBarterState.Open && offer.ExpiryTick >= WorldTick &&
        (offer.FirstPartyId == actor || offer.SecondPartyId == actor) &&
        !offer.AcceptedBy.Contains(actor, StringComparer.Ordinal));

    private bool WantsTradeFoodKind(string actor, string kind) => IsEdibleFood(kind) &&
        agents[actor].FullnessBasisPoints < NeedThresholds.TradeFoodWantedFullness &&
        society.Checkpoint.Inventory.Lots.Where(lot => lot.OwnerId == actor && lot.ItemKind == kind)
            .Sum(AvailableLotQuantity) < 2;

    private bool PersonalTradeReceivingSpace(string first, string second, int firstQuantity, int secondQuantity)
    {
        var inventory = society.Checkpoint.Inventory;
        return new[] { (Actor: first, Give: firstQuantity, Take: secondQuantity),
                (Actor: second, Give: secondQuantity, Take: firstQuantity) }.All(party =>
            agents.TryGetValue(party.Actor, out var person) &&
            (long)PersonalEquipmentRules.CarriedQuantity(inventory, party.Actor, person.Equipment) - party.Give +
                party.Take + ReservedShopCarrySpace(party.Actor) <=
                PersonalEquipmentRules.Capacity(inventory, party.Actor, person.Equipment) + HorseCargoCapacity(party.Actor));
    }

    private bool WantsTradeItem(string actor, InventoryLot item)
    {
        var state = agents[actor];
        var kind = item.ItemKind;
        if (MedicalSupplyWanted(actor, kind)) return true;
        if (WantsOrnamentInput(actor, kind)) return true;
        if (OrnamentContent.IsOrnament(kind))
        {
            // One personal ornament is enough. A diamond setting can replace
            // a plain one; private household stock is never personal equipment.
            var ornaments = society.Checkpoint.Inventory.Lots.Where(lot => lot.OwnerId == actor &&
                PersonalEquipmentRules.IsCarried(lot, actor) && lot.ContainerLotId is null &&
                lot.DeliveryBuildingId is null && OrnamentContent.IsOrnament(lot.ItemKind) &&
                AvailableLotQuantity(lot) > 0).ToArray();
            return kind == OrnamentContent.DiamondOrnament
                ? !ornaments.Any(lot => lot.ItemKind == OrnamentContent.DiamondOrnament)
                : ornaments.Length == 0;
        }
        if (AgentKnowledgeRules.IsArtifactKind(kind))
        {
            // An agent can offer a record they physically hold; a prospective
            // recipient wants it only if it contains a fact they have not learned.
            if (item.OwnerId == actor)
                return false;
            var artifact = knowledge.Artifacts.FirstOrDefault(candidate => candidate.LotId == item.Id);
            return artifact is not null && HasUnknownArtifactContents(actor, artifact);
        }

        if (IsEdibleFood(kind))
        {
            // Keeping a small trade reserve is different from taking a food errand.
            return WantsTradeFoodKind(actor, kind);
        }
        var owned = society.Checkpoint.Inventory.Lots.Where(lot => lot.OwnerId == actor && lot.ItemKind == kind)
            .Sum(AvailableLotQuantity);
        if (kind is "tool" or "clothing")
        {
            return owned == 0;
        }
        if (state.Project is not { Stage: not ("completed" or "cancelled") } project)
        {
            return false;
        }
        if (!TownConstructionCandidateIds.TryParse(project.CandidateId, out var selection))
            return false;
        var building = selection.IsBuilding
            ? worldContent.Buildings.FirstOrDefault(item => item.CanonicalId == selection.DefinitionId) : null;
        var recipe = selection.IsBuilding
            ? null : worldContent.Recipes.FirstOrDefault(item => item.CanonicalId == selection.DefinitionId);
        if (building is null && recipe is null) return false;
        var inputs = building?.BuildCosts ?? recipe!.Inputs;
        // Match the owners used by the actual construction and recipe choices.
        // Another household's stock cannot satisfy this actor's production plan.
        var inputOwner = building is not null ? BuildingConstructionOwner(actor, building) : ProductionOwnerFor(null, actor);
        return inputs.Any(input => input.ResourceId == kind && !HasAvailableQuantities([input], inputOwner) && owned < input.Amount);
    }

    private (InventoryLot Give, InventoryLot Take)? TradeOpportunity(string actor, string other, string? giveId = null, string? takeId = null)
    {
        if (actor == other || !IsAdult(actor) || !IsAdult(other) ||
            !PersonalTradeReceivingSpace(actor, other, 1, 1) ||
            NeedsUrgentWarmth(agents[actor]) || NeedsUrgentWarmth(agents[other]) ||
            society.Checkpoint.Inventory.Offers.Any(offer =>
                offer.State == DirectBarterState.Open &&
                (offer.FirstPartyId == actor || offer.SecondPartyId == actor || offer.FirstPartyId == other || offer.SecondPartyId == other) ||
                offer.Id.Value.StartsWith(DirectBarterPrefix, StringComparison.Ordinal) && offer.ExpiryTick + 120 >= WorldTick &&
                (offer.FirstPartyId == actor && offer.SecondPartyId == other || offer.FirstPartyId == other && offer.SecondPartyId == actor)))
        {
            return null;
        }
        var giving = SpendingGoods(actor, GoodsUse.Spend, [actor]).Select(match => match.Lot).ToArray();
        var taking = SpendingGoods(other, GoodsUse.Spend, [other]).Select(match => match.Lot).ToArray();
        foreach (var give in giving.Where(lot => (giveId is null || lot.Id == giveId) && TradeQuantityAvailable(lot) &&
                     !WantsTradeItem(actor, lot) && WantsTradeItem(other, lot) &&
                     (!IsEdibleFood(lot.ItemKind) || agents[actor].FullnessBasisPoints >= NeedThresholds.TradeFoodReserveFullness)))
        {
            // Records of the same kind can contain different discoveries. The
            // reciprocal demand checks still require contents each recipient lacks.
            var take = taking.FirstOrDefault(lot =>
                (takeId is null || lot.Id == takeId) && (lot.ItemKind != give.ItemKind || AgentKnowledgeRules.IsArtifactKind(lot.ItemKind)) &&
                TradeQuantityAvailable(lot) && !WantsTradeItem(other, lot) && WantsTradeItem(actor, lot) &&
                (!IsEdibleFood(lot.ItemKind) || agents[other].FullnessBasisPoints >= NeedThresholds.TradeFoodReserveFullness));
            if (take is not null)
            {
                return (give, take);
            }
        }
        return null;
    }

    private DirectBarterActionPlan? PlanDirectBarter(string actor, DirectBarterChoice choice,
        (InventoryLot Give, InventoryLot Take)? selected = null, DirectBarterOffer? selectedOffer = null)
    {
        if (choice.Kind == DirectBarterKind.Propose)
        {
            var trade = selected ?? TradeOpportunity(actor, choice.Subject);
            return trade is null ? null : new(actor, choice, choice.Subject, trade.Value.Give.Id, trade.Value.Take.Id,
                trade.Value.Give.ItemKind, trade.Value.Take.ItemKind, SharedStoragePosition, null,
                Reach(actor, SharedStoragePosition, ReachPurpose.DirectBarter, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
        }
        var offer = selectedOffer ?? society.Checkpoint.Inventory.Offers.SingleOrDefault(item => item.Id == choice.Subject);
        if (offer is null || offer.FirstPartyId != actor && offer.SecondPartyId != actor) return null;
        var first = offer.FirstPartyId == actor;
        var give = society.Checkpoint.Inventory.GetLot(first ? offer.FirstLotId : offer.SecondLotId);
        var take = society.Checkpoint.Inventory.GetLot(first ? offer.SecondLotId : offer.FirstLotId);
        return new(actor, choice, first ? offer.SecondPartyId : offer.FirstPartyId, give.Id, take.Id, give.ItemKind, take.ItemKind,
            SharedStoragePosition, offer, Reach(actor, SharedStoragePosition, ReachPurpose.DirectBarter, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    private bool DirectBarterOfferIsCurrent(DirectBarterActionPlan plan)
    {
        var current = society.Checkpoint.Inventory.Offers.SingleOrDefault(item => item.Id == plan.Choice.Subject);
        return current is { State: DirectBarterState.Open } && current.ExpiryTick >= WorldTick &&
            TownHearingProcedure.Digest(current) == TownHearingProcedure.Digest(plan.Offer) &&
            (plan.Choice.Kind == DirectBarterKind.Decline || current.AcceptedBy.Contains(plan.Actor, StringComparer.Ordinal) == (plan.Choice.Kind == DirectBarterKind.Wait));
    }

    private void OfferDirectBarter(List<CognitionCandidate> candidates, DirectBarterActionPlan? plan, string text, int priority)
    {
        if (plan is not null && CheckDirectBarter(plan).Allowed) candidates.Add(new(plan.CandidateId, text, priority) { ActionPlan = plan });
    }

    private void AddTradeCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (NeedsUrgentWarmth(agents[actor]))
        {
            return;
        }
        foreach (var offer in society.Checkpoint.Inventory.Offers.Where(offer => offer.State == DirectBarterState.Open &&
                     offer.Id.Value.StartsWith(DirectBarterPrefix, StringComparison.Ordinal) &&
                     offer.ExpiryTick >= WorldTick && (offer.FirstPartyId == actor || offer.SecondPartyId == actor)))
        {
            if (offer.AcceptedBy.Contains(actor, StringComparer.Ordinal))
            {
                OfferDirectBarter(candidates, PlanDirectBarter(actor, new(DirectBarterKind.Wait, offer.Id), selectedOffer: offer),
                    "Meet the other trader and wait for their answer.", 12);
                OfferDirectBarter(candidates, PlanDirectBarter(actor, new(DirectBarterKind.Decline, offer.Id), selectedOffer: offer),
                    "Withdraw the pending exchange and release both reserved items.", 110);
                continue;
            }
            var giveId = offer.FirstPartyId == actor ? offer.FirstLotId : offer.SecondLotId;
            var takeId = offer.FirstPartyId == actor ? offer.SecondLotId : offer.FirstLotId;
            var give = society.Checkpoint.Inventory.GetLot(giveId);
            var take = society.Checkpoint.Inventory.GetLot(takeId);
            var useful = WantsTradeItem(actor, take) && (!IsEdibleFood(give.ItemKind) || agents[actor].FullnessBasisPoints >= NeedThresholds.TradeFoodReserveFullness);
            OfferDirectBarter(candidates, PlanDirectBarter(actor, new(DirectBarterKind.Accept, offer.Id), selectedOffer: offer),
                $"Accept exchange: give one {give.ItemKind}, receive one {take.ItemKind}.", useful ? 12 : 60);
            OfferDirectBarter(candidates, PlanDirectBarter(actor, new(DirectBarterKind.Decline, offer.Id), selectedOffer: offer),
                "Decline this exchange and release both reserved items.", useful ? 60 : 12);
        }
        foreach (var other in agents.Keys.Where(other => other != actor)
                     .OrderByDescending(other => TrustScore(actor, other)).ThenBy(other => other, StringComparer.Ordinal))
        {
            if (TradeOpportunity(actor, other) is { } trade)
            {
                OfferDirectBarter(candidates, PlanDirectBarter(actor, new(DirectBarterKind.Propose, other), trade),
                    $"Offer {society.Checkpoint.GetAgent(other).Name} one {trade.Give.ItemKind} for one {trade.Take.ItemKind}; they may refuse.", 14);
            }
        }
    }

    private void ApplyTradeCandidate(string actor, AgentState state, string candidate, ActionPlan? originalPlan = null)
    {
        var plan = originalPlan as DirectBarterActionPlan ?? (DirectBarterChoice.Decode(candidate) is { } choice ? PlanDirectBarter(actor, choice) : null);
        if (plan is not null) ApplyDirectBarter(plan);
    }

    private void ApplyRetainedTradeCandidate(string actor, AgentState state, DirectBarterActionPlan plan)
    {
        var candidate = plan.CandidateId;
        if (candidate.StartsWith("trade_propose:", StringComparison.Ordinal))
        {
            var other = candidate[14..];
            if (!agents.ContainsKey(other) || TradeOpportunity(actor, other, plan.GiveId, plan.TakeId) is not { } trade)
            {
                return;
            }
            if (!IsWithinInteractionRange(state.Position, SharedStoragePosition, ResourceInteractionRange))
            {
                MoveToward(actor, state, SharedStoragePosition, "trade", ResourceInteractionRange);
                return;
            }
            var id = DirectBarterOfferId(WorldTick, actor, other);
            society.Apply(checkpoint => SocietyRules.CreateBarterOffer(checkpoint,
                new DirectBarterProposal(Identity.Parse(id), 1, actor, other, trade.Give.Id, 1, trade.Take.Id, 1, WorldTick + 120)));
            society.Apply(checkpoint => SocietyRules.AcceptBarterOffer(checkpoint, id, 1, actor));
            AppendEvent(new BarterOfferedEventPayload(actor, other));
            return;
        }
        if (candidate.StartsWith("trade_wait:", StringComparison.Ordinal))
        {
            var waitingId = candidate[11..];
            var waiting = plan.Offer;
            if (waiting is not null &&
                !IsWithinInteractionRange(state.Position, SharedStoragePosition, ResourceInteractionRange))
                MoveToward(actor, state, SharedStoragePosition, "trade", ResourceInteractionRange);
            return;
        }
        var accept = candidate.StartsWith("trade_accept:", StringComparison.Ordinal);
        var offerId = candidate[(accept ? 13 : 14)..];
        var offer = plan.Offer;
        if (offer is null || offer.ExpiryTick < WorldTick ||
            (offer.FirstPartyId != actor && offer.SecondPartyId != actor) || accept && offer.AcceptedBy.Contains(actor, StringComparer.Ordinal))
        {
            return;
        }
        if (!accept)
        {
            ApplyInventoryTransition(inventory => InventoryAuthority.CancelDirectBarterOffer(inventory, offerId, offer.Revision, actor));
            AppendEvent(new BarterDeclinedEventPayload(actor));
            return;
        }
        var storagePosition = SharedStoragePosition;
        if (!IsWithinInteractionRange(state.Position, storagePosition, ResourceInteractionRange))
        {
            MoveToward(actor, state, storagePosition, "trade", ResourceInteractionRange);
            return;
        }
        var otherParty = offer.FirstPartyId == actor ? offer.SecondPartyId : offer.FirstPartyId;
        if (!IsAdult(actor) || !IsAdult(otherParty) ||
            !IsWithinInteractionRange(agents[otherParty].Position, storagePosition, ResourceInteractionRange) ||
            !PersonalTradeReceivingSpace(offer.FirstPartyId, offer.SecondPartyId,
                offer.FirstQuantity, offer.SecondQuantity))
            return;
        society.Apply(checkpoint => SocietyRules.AcceptBarterOffer(checkpoint, offerId, offer.Revision, actor));
        if (society.Checkpoint.Inventory.GetOffer(offerId).State == DirectBarterState.Settled)
        {
            ReadTradedKnowledge(offer.FirstPartyId, offer.SecondPartyId, offer.FirstLotId, offer.SecondLotId);
            foreach (var (owner, subject) in new[] { (offer.FirstPartyId, offer.SecondPartyId), (offer.SecondPartyId, offer.FirstPartyId) })
            {
                IncreaseTrust(owner, subject, 1, "barter_completed");
                var memoryKey = "trust-memory:" + owner + ":" + subject;
                var memoryId = TrustMemoryId(owner, subject);
                if (!society.Checkpoint.Memories.Any(memory => memory.Id == memoryId || memory.Id == memoryKey))
                {
                    society.Apply(checkpoint => SocietyRules.RecordSocialMemory(checkpoint, new(Identity.Parse(memoryId), owner, subject,
                        $"Completed a mutually accepted exchange with {checkpoint.GetAgent(subject).Name}.", "public", WorldTick)
                    { Kind = SocietyMemoryKind.Relationship }));
                }
            }
            AppendEvent(new BarterCompletedEventPayload(actor));
        }
    }

    private bool TradeQuantityAvailable(InventoryLot lot) => UsablePersonalTradeLot(lot) &&
        (AvailableLotQuantity(lot) >= 2 || AgentKnowledgeRules.IsArtifactKind(lot.ItemKind) ||
         OrnamentContent.IsOrnament(lot.ItemKind) || SpendingGoods(lot.OwnerId, GoodsUse.Spend, [lot.OwnerId], lot.ItemKind)
             .Select(match => match.Lot).Any(other => other.Id != lot.Id &&
             UsablePersonalTradeLot(other)));

    private bool UsablePersonalTradeLot(InventoryLot lot) =>
        PersonalEquipmentRules.IsCarried(lot, lot.OwnerId) && lot.ContainerLotId is null &&
        !InventoryContainerRules.IsContainer(lot.ItemKind) && lot.DeliveryBuildingId is null &&
        (!agents.TryGetValue(lot.OwnerId, out var carrier) ||
         !PersonalEquipmentRules.IsSelected(carrier.Equipment, lot.Id)) &&
        AvailableLotQuantity(lot) > 0;

    private void MaintainDirectBarters()
    {
        foreach (var offer in society.Checkpoint.Inventory.Offers.Where(offer => offer.State == DirectBarterState.Open &&
                     offer.Id.Value.StartsWith(DirectBarterPrefix, StringComparison.Ordinal)).ToArray())
        {
            var currentInventory = society.Checkpoint.Inventory;
            var parties = new[]
            {
                (Owner: offer.FirstPartyId, Lot: offer.FirstLotId, Quantity: offer.FirstQuantity, Reservation: DerivedId.Create(offer.Id + ":first")),
                (Owner: offer.SecondPartyId, Lot: offer.SecondLotId, Quantity: offer.SecondQuantity, Reservation: DerivedId.Create(offer.Id + ":second")),
            };
            if (parties.Any(party => !society.Checkpoint.Agents.Any(person => person.Id == party.Owner && person.Status == SocietyAgentStatus.Active) ||
                    !currentInventory.Lots.Any(lot => lot.Id == party.Lot && PersonalEquipmentRules.IsCarried(lot, party.Owner) &&
                        lot.ContainerLotId is null && lot.DeliveryBuildingId is null &&
                        !InventoryContainerRules.IsContainer(lot.ItemKind) &&
                        !PersonalEquipmentRules.IsSelected(agents[party.Owner].Equipment, lot.Id) &&
                        lot.Quantity >= party.Quantity &&
                        lot.FreshnessBasisPoints > 0 && lot.ConditionBasisPoints > 0) ||
                    !currentInventory.Reservations.Any(reservation => reservation.Id == party.Reservation && reservation.State == InventoryReservationState.Reserved)))
            {
                ApplyInventoryTransition(inventory => InventoryAuthority.CancelDirectBarterOffer(inventory, offer.Id, offer.Revision, offer.FirstPartyId));
                AppendEvent(new BarterCancelledEventPayload(offer.FirstPartyId));
            }
        }
        var stale = society.Checkpoint.Inventory.Offers.Where(offer => offer.Id.Value.StartsWith(DirectBarterPrefix, StringComparison.Ordinal) &&
                offer.State != DirectBarterState.Open && offer.ExpiryTick + 120 < WorldTick)
            .OrderByDescending(offer => offer.ExpiryTick).Skip(16).Select(offer => offer.Id).ToHashSet();
        if (stale.Count > 0)
        {
            ApplyInventoryTransition(inventory => inventory with
            {
                Offers = inventory.Offers.Where(offer => !stale.Contains(offer.Id)).ToArray(),
                Reservations = inventory.Reservations.Where(reservation => !stale.Any(id => reservation.Id == DerivedId.Create(id + ":first") || reservation.Id == DerivedId.Create(id + ":second"))).ToArray(),
            });
        }
    }
}
