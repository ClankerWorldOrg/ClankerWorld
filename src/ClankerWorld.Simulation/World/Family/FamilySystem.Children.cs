using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class FamilySystem
{
    internal const int ChildHelpingLoad = 4;
    internal const int ChildHelpingRoute = 8;
    internal const int ChildSocialCooldownTicks = 120;

    internal static PlacedBuilding? ChildHelpingHouse(IFamilyChildWorld world, string actor, AgentState state) =>
        world.IsChild(actor) && state.FullnessBasisPoints >= NeedThresholds.ChildHelpingFullness && !world.NeedsUrgentWarmth(state) &&
        world.Society.GetAgent(actor).HouseholdId is { } household &&
        world.HouseForHousehold(household) is { } house &&
        world.Map.FootDistance(state.Position, house.Position) <= ChildHelpingRoute ? house : null;

    internal static int ChildHelpingRoom(IFamilyChildWorld world, string actor) => Math.Min(world.FreeCarryCapacity(actor), Math.Max(0,
        ChildHelpingLoad - PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, actor,
            world.PhysicalAgents.Living[actor].Equipment)));

    internal static bool ChildHelpingReachable(IFamilyChildWorld world, string actor, GridPoint source, int range, PlacedBuilding house)
    {
        bool ShortRoute(GridPoint from, GridPoint to, int interactionRange)
        {
            if (world.IsWithinInteractionRange(from, to, interactionRange)) return true;
            var route = world.ReachLeg(actor, from, to, ReachPurpose.ChildHelping, interactionRange, 0, SwimmingPolicy.WhenCapable);
            return route.Count > 0 && route.Count - 1 <= ChildHelpingRoute &&
                route.All(point => world.Map.FootDistance(point, house.Position) <= ChildHelpingRoute);
        }
        return world.Map.FootDistance(source, house.Position) <= ChildHelpingRoute &&
            ShortRoute(world.PhysicalAgents.Living[actor].Position, source, range) && ShortRoute(source, house.Position, 0);
    }

    internal static InventoryLot? ChildHelpingDelivery(IFamilyChildWorld world, string actor, PlacedBuilding house) =>
        world.FindGoods(ChildHelpingGoodsRequest(actor, actor)).Matches
            .Where(match => ChildGoodsCanMove(match, InventoryHoldRules.DeliveryFor(actor, house.InstanceId)))
            .Select(match => match.Lot).Where(lot => lot.OwnerId == actor &&
            PersonalEquipmentRules.IsCarried(lot, actor) && lot.ContainerLotId is null &&
            lot.DeliveryBuildingId == house.InstanceId && ChildHelpingItem(lot.ItemKind) &&
            lot.Quantity <= ChildHelpingLoad &&
            PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, actor, world.PhysicalAgents.Living[actor].Equipment) <= ChildHelpingLoad && world.AvailableLotQuantity(lot) == lot.Quantity &&
            world.CanDeliverHouseDelivery(lot)).OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();

    internal static InventoryLot? ChildHelpingStock(IFamilyChildWorld world, string actor, PlacedBuilding house) =>
        world.FindGoods(ChildHelpingGoodsRequest(actor, house.HouseholdId!)).Matches
            .Where(match => ChildGoodsCanMove(match, InventoryHoldRules.BorrowedBy(actor)))
            .Select(match => match.Lot).Where(lot => lot.OwnerId == house.HouseholdId &&
            lot.CarrierId is null && lot.ContainerLotId is null && lot.StorageBuildingId is null &&
            lot.DeliveryBuildingId is null && ChildHelpingItem(lot.ItemKind) && !world.OnBorrowedMarketStall(lot) &&
            world.AvailableLotQuantity(lot) > 0 && ChildHelpingReachable(world, actor, world.HouseholdStockPosition(lot),
                world.HouseholdStockInteractionRange(lot), house))
            .OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();

    internal static bool ChildHelpingSourceAvailable(IFamilyChildWorld world, string actor, PlacedBuilding house, MapResource source, string kind) =>
        world.Resources.GetValueOrDefault(source.Id) == ResourceState.Available &&
        (kind == "food" ? source.Kind is "food" or "fruit" && source.TreeKind is null &&
            world.FoodHarvestCarryUnits(source) <= ChildHelpingRoom(world, actor) &&
            world.DestinationRoom(house.InstanceId) >= world.FoodHarvestQuantity(source)
            : source.TreeKind is null && (source.Kind == "wood" || source.NaturalObjectKind == "fallen_wood")) &&
        ChildHelpingReachable(world, actor, source.Position, world.ResourceInteractionRange, house);

    internal static MapResource? ChildHelpingSource(IFamilyChildWorld world, string actor, PlacedBuilding house, string kind) => world.Map.Resources
        .Where(source => ChildHelpingSourceAvailable(world, actor, house, source, kind))
        .OrderBy(source => world.Map.FootDistance(world.PhysicalAgents.Living[actor].Position, source.Position))
        .ThenBy(source => source.Id, StringComparer.Ordinal).FirstOrDefault();

    internal static void AddChildHelpingCandidates(IFamilyChildWorld world, List<CognitionCandidate> candidates, string actor, AgentState state)
    {
        if (ChildHelpingHouse(world, actor, state) is not { } house) return;
        if (ChildHelpingDelivery(world, actor, house) is { } delivery)
        {
            if (ChildHelpingReachable(world, actor, state.Position, 0, house))
                world.OfferChildChoice(candidates, actor, new(ChildActionKind.Carry, delivery.Id), "Return your small delivery to your household's House.", 20);
            return;
        }
        if (world.CarriedHouseDelivery(actor) is not null || ChildHelpingRoom(world, actor) == 0 ||
            world.DestinationRoom(house.InstanceId) == 0) return;
        if (ChildHelpingStock(world, actor, house) is { } stock)
            world.OfferChildChoice(candidates, actor, new(ChildActionKind.Carry, stock.Id), "Carry a small loose household food or wood load to your House.", 28);
        foreach (var kind in new[] { "food", "wood" })
            if (ChildHelpingSource(world, actor, house, kind) is { } source)
                world.OfferChildChoice(candidates, actor, new(kind == "food" ? ChildActionKind.GatherFood : ChildActionKind.GatherWood, source.Id),
                    kind == "food" ? "Gather nearby wild food for your household, then carry it home." :
                        "Pick up nearby fallen wood for your household, then carry it home.", 30);
    }

    internal static void ApplyChildHelpingPlan(IFamilyChildWorld world, ChildActionPlan plan)
    {
        var actor = plan.Actor;
        var state = world.PhysicalAgents.Living[actor];
        if (!world.IsWithinInteractionRange(state.Position, plan.Destination, plan.InteractionRange))
        {
            world.MoveToward(actor, state, plan.Destination, "child_help", plan.InteractionRange);
            return;
        }
        if (plan.Choice.Kind == ChildActionKind.Carry)
        {
            if (plan.IsDelivery)
            {
                world.DeliverChildLoad(plan);
                world.AppendEvent(new ChildDeliveredHouseholdEventPayload(actor, plan.ItemKind!, plan.Quantity, plan.HouseId!));
            }
            else
            {
                world.CollectChildLoad(plan);
                world.AppendEvent(new ChildCollectedHouseholdEventPayload(actor, plan.ItemKind!, plan.Quantity));
            }
            return;
        }
        world.GatherChildLoad(plan);
    }


    internal static bool ChildHelpingItem(string kind) => kind == "wood" || FoodRules.IsEdible(kind);

    internal static GoodsRequest ChildHelpingGoodsRequest(string actor, string owner) =>
        new(GoodsUse.ConsumeAt, actor, GoodsOwners.One(owner), new([.. FoodRules.EdibleKinds, "wood"]));

    internal static bool ChildGoodsCanMove(GoodsMatch goods, IReadOnlyList<InventoryHold> access) =>
        goods.RequiredHolds.All(access.Contains);

    internal static bool ChildSocialAvailable(IFamilyChildWorld world, string actor, string target, string kind) =>
        world.IsChild(actor) && actor != target && world.PhysicalAgents.Living.ContainsKey(target) &&
        !LifeStageRules.IsInfant(world.Society.GetAgent(target).AgeBand) &&
        !world.Society.Memories.Any(memory => memory.OwnerId == actor && memory.SubjectId == target &&
            memory.Id.Value.StartsWith($"child-social:{kind}:", StringComparison.Ordinal) &&
            memory.SourceTick > world.WorldTick - ChildSocialCooldownTicks);

    internal static void AddChildCandidates(IFamilyChildWorld world, List<CognitionCandidate> candidates, string actor, AgentState state)
    {
        var availableTargets = 0;
        foreach (var target in world.PhysicalAgents.Living.Keys.Where(id => id != actor)
                 .OrderBy(id => world.Map.FootDistance(state.Position, world.PhysicalAgents.Living[id].Position))
                 .ThenBy(id => id, StringComparer.Ordinal))
        {
            var candidateCount = candidates.Count;
            world.OfferChildChoice(candidates, actor, new(ChildActionKind.Converse, target),
                "Talk with a nearby person and remember the encounter.", 38, target);
            world.OfferChildChoice(candidates, actor, new(ChildActionKind.Play, target),
                "Play together and build a friendship.", 34, target);
            world.OfferChildChoice(candidates, actor, new(ChildActionKind.Learn, target),
                "Learn a simple observation from an adult without taking an adult work role.", 42, target);
            if (candidates.Count > candidateCount && ++availableTargets == 3) break;
        }

        AddChildHelpingCandidates(world, candidates, actor, state);
        world.OfferChildChoice(candidates, actor, new(ChildActionKind.HelpFood),
            "Carry one spare food serving back to the household store.", 26);
    }

    internal static void ApplyChildSocial(IFamilyChildWorld world, ChildActionPlan plan)
    {
        var actor = plan.Actor;
        var target = plan.Choice.Target!;
        var kind = plan.Choice.Kind switch
        {
            ChildActionKind.Converse => "converse",
            ChildActionKind.Play => "play",
            _ => "learn",
        };
        var state = world.PhysicalAgents.Living[actor];
        if (!world.IsWithinInteractionRange(state.Position, plan.Destination, plan.InteractionRange))
        {
            world.MoveToward(actor, state, plan.Destination, "child_social", plan.InteractionRange);
            return;
        }

        // Keep short keys and cooldown prefixes; hash the complete identity when
        // ancestry makes it exceed the cognition memory boundary.
        var memoryId = world.ChildSocialMemoryId(kind, actor, target, world.WorldTick);
        world.RecordChildSocialMemory(memoryId, actor, target, kind);
        world.IncreaseTrust(actor, target, 1, "child_" + kind);
        world.AppendEvent(WorldEvents.Child(kind, actor, target));
    }

    internal static ChildActionPlan? PlanChildChoice(IFamilyChildWorld world, string actor, ChildChoice choice)
    {
        if (!world.PhysicalAgents.Living.TryGetValue(actor, out var state)) return null;
        var household = world.Society.GetAgent(actor).HouseholdId;
        var house = household is null ? null : world.HouseForHousehold(household);
        GridPoint destination;
        var range = world.ResourceInteractionRange;
        string? stockId = null;
        string? itemKind = null;
        var quantity = 0;
        var delivery = false;
        var purpose = ReachPurpose.ChildHelping;
        if (choice.Kind is ChildActionKind.Converse or ChildActionKind.Play or ChildActionKind.Learn)
        {
            if (choice.Target is null || !world.PhysicalAgents.Living.TryGetValue(choice.Target, out var target)) return null;
            destination = target.Position;
            purpose = ReachPurpose.ChildSocial;
        }
        else if (choice.Kind == ChildActionKind.HelpFood)
        {
            if (world.PersonalSpareFood(actor) is not { } stock) return null;
            stockId = stock.Id;
            itemKind = stock.ItemKind;
            quantity = 1;
            destination = house?.Position ?? world.SharedStoragePosition;
            range = house is null ? world.ResourceInteractionRange : 0;
        }
        else
        {
            if (house is null) return null;
            if (choice.Kind == ChildActionKind.Carry)
            {
                var owner = ChildHelpingDelivery(world, actor, house)?.Id == choice.Target ? actor : house.HouseholdId!;
                if (choice.Target is null || world.RecheckGoods(ChildHelpingGoodsRequest(actor, owner), choice.Target) is not { } goods)
                    return null;
                var stock = goods.Lot;
                stockId = stock.Id;
                itemKind = stock.ItemKind;
                delivery = owner == actor;
                destination = delivery ? house.Position : world.HouseholdStockPosition(stock);
                range = delivery ? 0 : world.HouseholdStockInteractionRange(stock);
                quantity = delivery ? stock.Quantity : Math.Min(ChildHelpingRoom(world, actor),
                    Math.Min(world.AvailableLotQuantity(stock), world.DestinationRoom(house.InstanceId)));
            }
            else
            {
                if (world.Map.Resources.FirstOrDefault(source => source.Id == choice.Target) is not { } resource) return null;
                destination = resource.Position;
                quantity = choice.Kind == ChildActionKind.GatherFood ? world.FoodHarvestQuantity(resource) : 1;
            }
        }
        var route = world.Reach(actor, destination, purpose, range, 0, SwimmingPolicy.WhenCapable);
        return new(actor, choice, destination, range, route, household, house?.InstanceId, stockId, itemKind, quantity, delivery);
    }

    internal static ActionCheck CheckChildPlan(IFamilyChildWorld world, ChildActionPlan plan, ActorCapabilityPolicy policy)
    {
        var actor = plan.Actor;
        if (ActorCapabilities.Check(world.CapabilitiesFor(actor), plan.CandidateId, policy) is { Allowed: false } capabilities) return capabilities;
        var state = world.PhysicalAgents.Living[actor];
        var choice = plan.Choice;
        var social = choice.Kind is ChildActionKind.Converse or ChildActionKind.Play or ChildActionKind.Learn;
        if (social)
        {
            var kind = choice.Kind switch { ChildActionKind.Converse => "converse", ChildActionKind.Play => "play", _ => "learn" };
            if (choice.Target is null || !ChildSocialAvailable(world, actor, choice.Target, kind) ||
                world.PhysicalAgents.Living[choice.Target].Position != plan.Destination || choice.Kind == ChildActionKind.Learn && !world.IsAdult(choice.Target))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the person is no longer available there");
            if (world.NeedsUrgentFood(state) && !world.IsWithinInteractionRange(state.Position, plan.Destination, plan.InteractionRange))
                return ActionCheck.Refuse(ActionRefusalReason.UrgentNeed, "finding food comes first");
        }
        else
        {
            var household = world.Society.GetAgent(actor).HouseholdId;
            var house = household is null ? null : world.HouseForHousehold(household);
            if (household != plan.HouseholdId || house?.InstanceId != plan.HouseId)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household or House has changed");
            if (state.FullnessBasisPoints < NeedThresholds.ChildHelpingFullness)
                return ActionCheck.Refuse(ActionRefusalReason.UrgentNeed, "they need to keep their food reserve");
            if (choice.Kind == ChildActionKind.HelpFood)
            {
                if (world.PersonalSpareFood(actor, sourceLotId: plan.StockId) is not { } stock || stock.ItemKind != plan.ItemKind)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the spare food is no longer available");
                if ((house?.Position ?? world.SharedStoragePosition) != plan.Destination || house is not null && world.DestinationRoom(house.InstanceId) < plan.Quantity)
                    return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the household store has no room");
            }
            else
            {
                if (house is null || ChildHelpingHouse(world, actor, state)?.InstanceId != plan.HouseId)
                    return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the House is too far away");
                if (choice.Kind == ChildActionKind.Carry)
                {
                    var owner = plan.IsDelivery ? actor : house.HouseholdId!;
                    if (world.RecheckGoods(ChildHelpingGoodsRequest(actor, owner), plan.StockId!) is not { } goods ||
                        goods.Lot.ItemKind != plan.ItemKind || world.AvailableLotQuantity(goods.Lot) < plan.Quantity || plan.Quantity <= 0)
                        return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected goods are no longer available");
                    if (!ChildGoodsCanMove(goods, plan.IsDelivery ? InventoryHoldRules.DeliveryFor(actor, house.InstanceId) : InventoryHoldRules.BorrowedBy(actor)))
                        return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected goods are held for another purpose");
                    var lot = goods.Lot;
                    if (plan.IsDelivery)
                    {
                        if (!PersonalEquipmentRules.IsCarried(lot, actor) || lot.ContainerLotId is not null ||
                            lot.DeliveryBuildingId != house.InstanceId || lot.Quantity != plan.Quantity ||
                            lot.Quantity > ChildHelpingLoad ||
                            PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, actor, state.Equipment) > ChildHelpingLoad ||
                            world.AvailableLotQuantity(lot) != lot.Quantity || !world.CanDeliverHouseDelivery(lot) ||
                            !ChildHelpingReachable(world, actor, state.Position, 0, house))
                            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the delivery can no longer be made");
                    }
                    else if (lot.CarrierId is not null || lot.ContainerLotId is not null || lot.StorageBuildingId is not null ||
                        lot.DeliveryBuildingId is not null || !ChildHelpingItem(lot.ItemKind) || world.OnBorrowedMarketStall(lot) ||
                        world.CarriedHouseDelivery(actor) is not null || ChildHelpingRoom(world, actor) < plan.Quantity ||
                        world.DestinationRoom(house.InstanceId) < plan.Quantity || world.HouseholdStockPosition(lot) != plan.Destination ||
                        !ChildHelpingReachable(world, actor, plan.Destination, plan.InteractionRange, house))
                        return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected load can no longer be carried home");
                }
                else
                {
                    var kind = choice.Kind == ChildActionKind.GatherFood ? "food" : "wood";
                    if (world.CarriedHouseDelivery(actor) is not null || ChildHelpingRoom(world, actor) == 0 || world.DestinationRoom(house.InstanceId) == 0 ||
                        world.Map.Resources.FirstOrDefault(source => source.Id == choice.Target) is not { } resource ||
                        resource.Position != plan.Destination || !ChildHelpingSourceAvailable(world, actor, house, resource, kind) ||
                        (kind == "food" ? world.FoodHarvestQuantity(resource) : 1) != plan.Quantity)
                        return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected resource can no longer be gathered");
                }
            }
        }
        var route = world.Reach(actor, plan.Destination, social ? ReachPurpose.ChildSocial : ReachPurpose.ChildHelping,
            plan.InteractionRange, 0, SwimmingPolicy.WhenCapable);
        return route.Count > 0 ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "it's out of reach now");
    }

    internal static void ApplyCheckedChildPlan(IFamilyChildWorld world, ChildActionPlan plan)
    {
        if (plan.Choice.Kind is ChildActionKind.Converse or ChildActionKind.Play or ChildActionKind.Learn)
            ApplyChildSocial(world, plan);
        else if (plan.Choice.Kind == ChildActionKind.HelpFood)
        {
            var state = world.PhysicalAgents.Living[plan.Actor];
            if (!world.IsWithinInteractionRange(state.Position, plan.Destination, plan.InteractionRange))
            {
                world.MoveToward(plan.Actor, state, plan.Destination, "child_help", plan.InteractionRange);
                return;
            }
            world.ShareChildFood(plan);
            world.AppendEvent(new ChildHelpedHouseholdEventPayload(plan.Actor));
        }
        else ApplyChildHelpingPlan(world, plan);
    }
}
