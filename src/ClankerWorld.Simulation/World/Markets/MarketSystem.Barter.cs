using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class MarketSystem
{
    internal const string DirectBarterPrefix = "direct-barter:";
    internal static ActorCapabilityPolicy PersonalBarterCapabilities => new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true);
    internal static bool HasTradeResponse(IMarketBarterWorld world, string actor) => !world.NeedsUrgentWarmth(world.PhysicalAgents.Living[actor]) && world.Society.Inventory.Offers.Any(offer =>
        offer.Id.Value.StartsWith(DirectBarterPrefix, StringComparison.Ordinal) &&
        offer.State == DirectBarterState.Open && offer.ExpiryTick >= world.WorldTick &&
        (offer.FirstPartyId == actor || offer.SecondPartyId == actor) &&
        !offer.AcceptedBy.Contains(actor, StringComparer.Ordinal));

    internal static bool WantsTradeFoodKind(IMarketBarterWorld world, string actor, string kind) => FoodRules.IsEdible(kind) &&
        world.PhysicalAgents.Living[actor].FullnessBasisPoints < NeedThresholds.TradeFoodWantedFullness &&
        world.Society.Inventory.Lots.Where(lot => lot.OwnerId == actor && lot.ItemKind == kind)
            .Sum(world.AvailableLotQuantity) < 2;

    internal static bool PersonalTradeReceivingSpace(IMarketBarterWorld world, string first, string second, int firstQuantity, int secondQuantity)
    {
        var inventory = world.Society.Inventory;
        return new[] { (Actor: first, Give: firstQuantity, Take: secondQuantity),
                (Actor: second, Give: secondQuantity, Take: firstQuantity) }.All(party =>
            world.PhysicalAgents.Living.TryGetValue(party.Actor, out var person) &&
            (long)PersonalEquipmentRules.CarriedQuantity(inventory, party.Actor, person.Equipment) - party.Give +
                party.Take + world.ReservedShopCarrySpace(party.Actor) <=
                PersonalEquipmentRules.Capacity(inventory, party.Actor, person.Equipment) + world.HorseCargoCapacity(party.Actor));
    }

    internal static bool WantsTradeItem(IMarketBarterWorld world, string actor, InventoryLot item)
    {
        var state = world.PhysicalAgents.Living[actor];
        var kind = item.ItemKind;
        if (world.MedicalSupplyWanted(actor, kind)) return true;
        if (world.WantsOrnamentInput(actor, kind)) return true;
        if (OrnamentContent.IsOrnament(kind))
        {
            // One personal ornament is enough. A diamond setting can replace
            // a plain one; private household stock is never personal equipment.
            var ornaments = world.Society.Inventory.Lots.Where(lot => lot.OwnerId == actor &&
                PersonalEquipmentRules.IsCarried(lot, actor) && lot.ContainerLotId is null &&
                lot.DeliveryBuildingId is null && OrnamentContent.IsOrnament(lot.ItemKind) &&
                world.AvailableLotQuantity(lot) > 0).ToArray();
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
            var artifact = world.Knowledge.Artifacts.FirstOrDefault(candidate => candidate.LotId == item.Id);
            return artifact is not null && world.HasUnknownArtifactContents(actor, artifact);
        }

        if (FoodRules.IsEdible(kind))
        {
            // Keeping a small trade reserve is different from taking a food errand.
            return WantsTradeFoodKind(world, actor, kind);
        }
        var owned = world.Society.Inventory.Lots.Where(lot => lot.OwnerId == actor && lot.ItemKind == kind)
            .Sum(world.AvailableLotQuantity);
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
            ? world.Content.Buildings.FirstOrDefault(item => item.CanonicalId == selection.DefinitionId) : null;
        var recipe = selection.IsBuilding
            ? null : world.Content.Recipes.FirstOrDefault(item => item.CanonicalId == selection.DefinitionId);
        if (building is null && recipe is null) return false;
        var inputs = building?.BuildCosts ?? recipe!.Inputs;
        // Match the owners used by the actual construction and recipe choices.
        // Another household's stock cannot satisfy this actor's production plan.
        var inputOwner = building is not null ? world.BuildingConstructionOwner(actor, building) : world.PersonalProductionOwner(actor);
        return inputs.Any(input => input.ResourceId == kind && !world.HasAvailableQuantities([input], inputOwner) && owned < input.Amount);
    }

    internal static (InventoryLot Give, InventoryLot Take)? TradeOpportunity(IMarketBarterWorld world, string actor, string other, string? giveId = null, string? takeId = null)
    {
        if (actor == other || !world.IsAdult(actor) || !world.IsAdult(other) ||
            !PersonalTradeReceivingSpace(world, actor, other, 1, 1) ||
            world.NeedsUrgentWarmth(world.PhysicalAgents.Living[actor]) || world.NeedsUrgentWarmth(world.PhysicalAgents.Living[other]) ||
            world.Society.Inventory.Offers.Any(offer =>
                offer.State == DirectBarterState.Open &&
                (offer.FirstPartyId == actor || offer.SecondPartyId == actor || offer.FirstPartyId == other || offer.SecondPartyId == other) ||
                offer.Id.Value.StartsWith(DirectBarterPrefix, StringComparison.Ordinal) && offer.ExpiryTick + 120 >= world.WorldTick &&
                (offer.FirstPartyId == actor && offer.SecondPartyId == other || offer.FirstPartyId == other && offer.SecondPartyId == actor)))
        {
            return null;
        }
        var giving = world.SpendingGoods(actor, GoodsUse.Spend, [actor]).Select(match => match.Lot).ToArray();
        var taking = world.SpendingGoods(other, GoodsUse.Spend, [other]).Select(match => match.Lot).ToArray();
        foreach (var give in giving.Where(lot => (giveId is null || lot.Id == giveId) && TradeQuantityAvailable(world, lot) &&
                     !WantsTradeItem(world, actor, lot) && WantsTradeItem(world, other, lot) &&
                     (!FoodRules.IsEdible(lot.ItemKind) || world.PhysicalAgents.Living[actor].FullnessBasisPoints >= NeedThresholds.TradeFoodReserveFullness)))
        {
            // Records of the same kind can contain different discoveries. The
            // reciprocal demand checks still require contents each recipient lacks.
            var take = taking.FirstOrDefault(lot =>
                (takeId is null || lot.Id == takeId) && (lot.ItemKind != give.ItemKind || AgentKnowledgeRules.IsArtifactKind(lot.ItemKind)) &&
                TradeQuantityAvailable(world, lot) && !WantsTradeItem(world, other, lot) && WantsTradeItem(world, actor, lot) &&
                (!FoodRules.IsEdible(lot.ItemKind) || world.PhysicalAgents.Living[other].FullnessBasisPoints >= NeedThresholds.TradeFoodReserveFullness));
            if (take is not null)
            {
                return (give, take);
            }
        }
        return null;
    }

    internal static DirectBarterActionPlan? PlanDirectBarter(IMarketBarterWorld world, string actor, DirectBarterChoice choice,
        (InventoryLot Give, InventoryLot Take)? selected = null, DirectBarterOffer? selectedOffer = null)
    {
        if (choice.Kind == DirectBarterKind.Propose)
        {
            var trade = selected ?? TradeOpportunity(world, actor, choice.Subject);
            return trade is null ? null : new(actor, choice, choice.Subject, trade.Value.Give.Id, trade.Value.Take.Id,
                trade.Value.Give.ItemKind, trade.Value.Take.ItemKind, world.SharedStoragePosition, null,
                world.Reach(actor, world.SharedStoragePosition, ReachPurpose.DirectBarter, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
        }
        var offer = selectedOffer ?? world.Society.Inventory.Offers.SingleOrDefault(item => item.Id == choice.Subject);
        if (offer is null || offer.FirstPartyId != actor && offer.SecondPartyId != actor) return null;
        var first = offer.FirstPartyId == actor;
        var give = world.Society.Inventory.GetLot(first ? offer.FirstLotId : offer.SecondLotId);
        var take = world.Society.Inventory.GetLot(first ? offer.SecondLotId : offer.FirstLotId);
        return new(actor, choice, first ? offer.SecondPartyId : offer.FirstPartyId, give.Id, take.Id, give.ItemKind, take.ItemKind,
            world.SharedStoragePosition, offer, world.Reach(actor, world.SharedStoragePosition, ReachPurpose.DirectBarter, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    internal static bool DirectBarterOfferIsCurrent(IMarketBarterWorld world, DirectBarterActionPlan plan)
    {
        var current = world.Society.Inventory.Offers.SingleOrDefault(item => item.Id == plan.Choice.Subject);
        return current is { State: DirectBarterState.Open } && current.ExpiryTick >= world.WorldTick &&
            TownHearingProcedure.Digest(current) == TownHearingProcedure.Digest(plan.Offer) &&
            (plan.Choice.Kind == DirectBarterKind.Decline || current.AcceptedBy.Contains(plan.Actor, StringComparer.Ordinal) == (plan.Choice.Kind == DirectBarterKind.Wait));
    }

    internal static void ApplyRetainedTradeCandidate(IMarketBarterWorld world, string actor, AgentState state, DirectBarterActionPlan plan)
    {
        var candidate = plan.CandidateId;
        if (candidate.StartsWith("trade_propose:", StringComparison.Ordinal))
        {
            var other = candidate[14..];
            if (!world.PhysicalAgents.Living.ContainsKey(other) || TradeOpportunity(world, actor, other, plan.GiveId, plan.TakeId) is not { } trade)
            {
                return;
            }
            if (!world.IsWithinInteractionRange(state.Position, world.SharedStoragePosition, world.ResourceInteractionRange))
            {
                world.MoveToward(actor, state, world.SharedStoragePosition, "trade", world.ResourceInteractionRange);
                return;
            }
            var id = world.DirectBarterOfferId(world.WorldTick, actor, other);
            world.CreateBarterOffer(InventoryStore.CreateDirectBarterProposal(Identity.Parse(id), 1, actor, other, trade.Give.Id, 1, trade.Take.Id, 1, world.WorldTick + 120));
            world.AcceptBarterOffer(id, 1, actor);
            world.AppendEvent(new BarterOfferedEventPayload(actor, other));
            return;
        }
        if (candidate.StartsWith("trade_wait:", StringComparison.Ordinal))
        {
            var waitingId = candidate[11..];
            var waiting = plan.Offer;
            if (waiting is not null &&
                !world.IsWithinInteractionRange(state.Position, world.SharedStoragePosition, world.ResourceInteractionRange))
                world.MoveToward(actor, state, world.SharedStoragePosition, "trade", world.ResourceInteractionRange);
            return;
        }
        var accept = candidate.StartsWith("trade_accept:", StringComparison.Ordinal);
        var offerId = candidate[(accept ? 13 : 14)..];
        var offer = plan.Offer;
        if (offer is null || offer.ExpiryTick < world.WorldTick ||
            (offer.FirstPartyId != actor && offer.SecondPartyId != actor) || accept && offer.AcceptedBy.Contains(actor, StringComparer.Ordinal))
        {
            return;
        }
        if (!accept)
        {
            world.CancelBarterOffer(offerId, offer.Revision, actor);
            world.AppendEvent(new BarterDeclinedEventPayload(actor));
            return;
        }
        var storagePosition = world.SharedStoragePosition;
        if (!world.IsWithinInteractionRange(state.Position, storagePosition, world.ResourceInteractionRange))
        {
            world.MoveToward(actor, state, storagePosition, "trade", world.ResourceInteractionRange);
            return;
        }
        var otherParty = offer.FirstPartyId == actor ? offer.SecondPartyId : offer.FirstPartyId;
        if (!world.IsAdult(actor) || !world.IsAdult(otherParty) ||
            !world.IsWithinInteractionRange(world.PhysicalAgents.Living[otherParty].Position, storagePosition, world.ResourceInteractionRange) ||
            !PersonalTradeReceivingSpace(world, offer.FirstPartyId, offer.SecondPartyId,
                offer.FirstQuantity, offer.SecondQuantity))
            return;
        world.AcceptBarterOffer(offerId, offer.Revision, actor);
        if (world.Society.Inventory.GetOffer(offerId).State == DirectBarterState.Settled)
        {
            world.ReadTradedKnowledge(offer.FirstPartyId, offer.SecondPartyId, offer.FirstLotId, offer.SecondLotId);
            foreach (var (owner, subject) in new[] { (offer.FirstPartyId, offer.SecondPartyId), (offer.SecondPartyId, offer.FirstPartyId) })
            {
                world.IncreaseTrust(owner, subject, 1, "barter_completed");
                var memoryKey = IdentityRules.PrefixedReference("trust-memory:", $"{owner}:{subject}");
                var memoryId = world.TrustMemoryId(owner, subject);
                if (!world.Society.Memories.Any(memory => memory.Id.Value == memoryId || memory.Id.Value == memoryKey))
                {
                    world.RecordBarterMemory(memoryId, owner, subject);
                }
            }
            world.AppendEvent(new BarterCompletedEventPayload(actor));
        }
    }

    internal static bool TradeQuantityAvailable(IMarketBarterWorld world, InventoryLot lot) => UsablePersonalTradeLot(world, lot) &&
        (world.AvailableLotQuantity(lot) >= 2 || AgentKnowledgeRules.IsArtifactKind(lot.ItemKind) ||
         OrnamentContent.IsOrnament(lot.ItemKind) || world.SpendingGoods(lot.OwnerId, GoodsUse.Spend, [lot.OwnerId], lot.ItemKind)
             .Select(match => match.Lot).Any(other => other.Id != lot.Id &&
             UsablePersonalTradeLot(world, other)));

    internal static bool UsablePersonalTradeLot(IMarketBarterWorld world, InventoryLot lot) =>
        PersonalEquipmentRules.IsCarried(lot, lot.OwnerId) && lot.ContainerLotId is null &&
        !InventoryContainerRules.IsContainer(lot.ItemKind) && lot.DeliveryBuildingId is null &&
        (!world.PhysicalAgents.Living.TryGetValue(lot.OwnerId, out var carrier) ||
         !PersonalEquipmentRules.IsSelected(carrier.Equipment, lot.Id)) &&
        world.AvailableLotQuantity(lot) > 0;

    internal static void MaintainDirectBarters(IMarketBarterWorld world)
    {
        foreach (var offer in world.Society.Inventory.Offers.Where(offer => offer.State == DirectBarterState.Open &&
                     offer.Id.Value.StartsWith(DirectBarterPrefix, StringComparison.Ordinal)).ToArray())
        {
            var currentInventory = world.Society.Inventory;
            var parties = new[]
            {
                (Owner: offer.FirstPartyId, Lot: offer.FirstLotId, Quantity: offer.FirstQuantity, Reservation: DerivedId.Create(offer.Id + ":first")),
                (Owner: offer.SecondPartyId, Lot: offer.SecondLotId, Quantity: offer.SecondQuantity, Reservation: DerivedId.Create(offer.Id + ":second")),
            };
            if (parties.Any(party => !world.Society.Agents.Any(person => person.Id == party.Owner && person.Status == SocietyAgentStatus.Active) ||
                    !currentInventory.Lots.Any(lot => lot.Id == party.Lot && PersonalEquipmentRules.IsCarried(lot, party.Owner) &&
                        lot.ContainerLotId is null && lot.DeliveryBuildingId is null &&
                        !InventoryContainerRules.IsContainer(lot.ItemKind) &&
                        !PersonalEquipmentRules.IsSelected(world.PhysicalAgents.Living[party.Owner].Equipment, lot.Id) &&
                        lot.Quantity >= party.Quantity &&
                        lot.FreshnessBasisPoints > 0 && lot.ConditionBasisPoints > 0) ||
                    !currentInventory.Reservations.Any(reservation => reservation.Id == party.Reservation && reservation.State == InventoryReservationState.Reserved)))
            {
                world.CancelBarterOffer(offer.Id, offer.Revision, offer.FirstPartyId);
                world.AppendEvent(new BarterCancelledEventPayload(offer.FirstPartyId));
            }
        }
        var stale = world.Society.Inventory.Offers.Where(offer => offer.Id.Value.StartsWith(DirectBarterPrefix, StringComparison.Ordinal) &&
                offer.State != DirectBarterState.Open && offer.ExpiryTick + 120 < world.WorldTick)
            .OrderByDescending(offer => offer.ExpiryTick).Skip(16).Select(offer => offer.Id.Value).ToHashSet(StringComparer.Ordinal);
        if (stale.Count > 0)
        {
            world.RetireBarterOffers(stale);
        }
    }

    internal static ActionCheck CheckDirectBarter(IMarketBarterWorld world, DirectBarterActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? PersonalBarterCapabilities);
        if (!capability.Allowed) return capability;
        if (!world.PhysicalAgents.Living.ContainsKey(plan.OtherId) || !world.IsAdult(plan.OtherId) || world.SharedStoragePosition != plan.Destination)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected trader or meeting place has changed");
        if (plan.Choice.Kind == DirectBarterKind.Propose)
        {
            if (TradeOpportunity(world, plan.Actor, plan.OtherId, plan.GiveId, plan.TakeId) is not { } trade ||
                trade.Give.ItemKind != plan.GiveKind || trade.Take.ItemKind != plan.TakeKind)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the retained barter goods or reciprocal demand are unavailable");
        }
        else if (!DirectBarterOfferIsCurrent(world, plan))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected personal offer or its response state has changed");
        if (plan.Choice.Kind == DirectBarterKind.Decline) return new();
        return world.IsWithinInteractionRange(world.PhysicalAgents.Living[plan.Actor].Position, plan.Destination, world.ResourceInteractionRange) ||
            world.Reach(plan.Actor, plan.Destination, ReachPurpose.DirectBarter, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the retained personal barter meeting is out of reach");
    }

    internal static void ApplyCheckedDirectBarter(IMarketBarterWorld world, DirectBarterActionPlan plan)
    {
        ApplyRetainedTradeCandidate(world, plan.Actor, world.PhysicalAgents.Living[plan.Actor], plan);
    }
}
