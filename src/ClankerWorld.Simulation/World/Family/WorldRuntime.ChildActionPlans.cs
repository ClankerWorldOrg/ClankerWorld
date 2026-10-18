using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<ChildChoice, ChildActionPlan> ChildDefinition => ChildActions.Definition;

    private static class ChildActions
    {
        internal static readonly ActionDefinition<ChildChoice, ChildActionPlan> Definition = new(
            static (string candidate, out ChildChoice choice) =>
            {
                var decoded = ChildChoice.Parse(candidate);
                choice = decoded.GetValueOrDefault();
                return decoded.HasValue;
            },
            static choice => choice.Id,
            new(ActionLifeStages.Children),
            static (world, actor, choice, _) => world.PlanChildChoice(actor, choice),
            static (world, plan, policy) => world.CheckChildPlan(plan, policy),
            static (world, plan, _, _) =>
            {
                world.ApplyCheckedChildPlan(plan);
                return true;
            });
    }

    private void OfferChildChoice(List<CognitionCandidate> candidates, string actor, ChildChoice choice,
        string description, int priority, string? destinationId = null)
    {
        if (ChildDefinition.Offer(this, actor, choice) is { } plan)
            candidates.Add(new(ChildDefinition.Encode(choice), description, priority, destinationId) { ActionPlan = plan });
    }

    private ChildActionPlan? PlanChildCandidate(string actor, string candidate) =>
        ChildDefinition.Decode(candidate, out var choice) ? ChildDefinition.Plan(this, actor, choice) : null;

    private ChildActionPlan? PlanChildChoice(string actor, ChildChoice choice)
    {
        if (!agents.TryGetValue(actor, out var state)) return null;
        var household = society.Checkpoint.GetAgent(actor).HouseholdId;
        var house = household is null ? null : HouseForHousehold(household);
        GridPoint destination;
        var range = ResourceInteractionRange;
        string? stockId = null;
        string? itemKind = null;
        var quantity = 0;
        var delivery = false;
        var purpose = ReachPurpose.ChildHelping;
        if (choice.Kind is ChildActionKind.Converse or ChildActionKind.Play or ChildActionKind.Learn)
        {
            if (choice.Target is null || !agents.TryGetValue(choice.Target, out var target)) return null;
            destination = target.Position;
            purpose = ReachPurpose.ChildSocial;
        }
        else if (choice.Kind == ChildActionKind.HelpFood)
        {
            if (PersonalSpareFood(actor) is not { } stock) return null;
            stockId = stock.Id;
            itemKind = stock.ItemKind;
            quantity = 1;
            destination = house?.Position ?? SharedStoragePosition;
            range = house is null ? ResourceInteractionRange : 0;
        }
        else
        {
            if (house is null) return null;
            if (choice.Kind == ChildActionKind.Carry)
            {
                var owner = ChildHelpingDelivery(actor, house)?.Id == choice.Target ? actor : house.HouseholdId!;
                if (choice.Target is null || RecheckGoods(ChildHelpingGoodsRequest(actor, owner), choice.Target) is not { } goods)
                    return null;
                var stock = goods.Lot;
                stockId = stock.Id;
                itemKind = stock.ItemKind;
                delivery = owner == actor;
                destination = delivery ? house.Position : HouseholdStockPosition(stock);
                range = delivery ? 0 : HouseholdStockInteractionRange(stock);
                quantity = delivery ? stock.Quantity : Math.Min(ChildHelpingRoom(actor),
                    Math.Min(AvailableLotQuantity(stock), DestinationRoom(house.InstanceId)));
            }
            else
            {
                if (map.Resources.FirstOrDefault(source => source.Id == choice.Target) is not { } resource) return null;
                destination = resource.Position;
                quantity = choice.Kind == ChildActionKind.GatherFood ? FoodHarvestQuantity(resource) : 1;
            }
        }
        var route = Reach(actor, destination, purpose, range, 0, SwimmingPolicy.WhenCapable);
        return new(actor, choice, destination, range, route, household, house?.InstanceId, stockId, itemKind, quantity, delivery);
    }

    private ActionCheck CheckChildPlan(ChildActionPlan plan, ActorCapabilityPolicy policy)
    {
        var actor = plan.Actor;
        if (ActorCapabilities.Check(CapabilitiesFor(actor), plan.CandidateId, policy) is { Allowed: false } capabilities) return capabilities;
        var state = agents[actor];
        var choice = plan.Choice;
        var social = choice.Kind is ChildActionKind.Converse or ChildActionKind.Play or ChildActionKind.Learn;
        if (social)
        {
            var kind = choice.Kind switch { ChildActionKind.Converse => "converse", ChildActionKind.Play => "play", _ => "learn" };
            if (choice.Target is null || !ChildSocialAvailable(actor, choice.Target, kind) ||
                agents[choice.Target].Position != plan.Destination || choice.Kind == ChildActionKind.Learn && !IsAdult(choice.Target))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the person is no longer available there");
            if (NeedsUrgentFood(state) && !IsWithinInteractionRange(state.Position, plan.Destination, plan.InteractionRange))
                return ActionCheck.Refuse(ActionRefusalReason.UrgentNeed, "finding food comes first");
        }
        else
        {
            var household = society.Checkpoint.GetAgent(actor).HouseholdId;
            var house = household is null ? null : HouseForHousehold(household);
            if (household != plan.HouseholdId || house?.InstanceId != plan.HouseId)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household or House has changed");
            if (state.FullnessBasisPoints < NeedThresholds.ChildHelpingFullness)
                return ActionCheck.Refuse(ActionRefusalReason.UrgentNeed, "they need to keep their food reserve");
            if (choice.Kind == ChildActionKind.HelpFood)
            {
                if (PersonalSpareFood(actor, sourceLotId: plan.StockId) is not { } stock || stock.ItemKind != plan.ItemKind)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the spare food is no longer available");
                if ((house?.Position ?? SharedStoragePosition) != plan.Destination || house is not null && DestinationRoom(house.InstanceId) < plan.Quantity)
                    return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the household store has no room");
            }
            else
            {
                if (house is null || ChildHelpingHouse(actor, state)?.InstanceId != plan.HouseId)
                    return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the House is too far away");
                if (choice.Kind == ChildActionKind.Carry)
                {
                    var owner = plan.IsDelivery ? actor : house.HouseholdId!;
                    if (RecheckGoods(ChildHelpingGoodsRequest(actor, owner), plan.StockId!) is not { } goods ||
                        goods.Lot.ItemKind != plan.ItemKind || AvailableLotQuantity(goods.Lot) < plan.Quantity || plan.Quantity <= 0)
                        return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected goods are no longer available");
                    if (!ChildGoodsCanMove(goods, plan.IsDelivery ? InventoryHoldRules.DeliveryFor(actor, house.InstanceId) : InventoryHoldRules.BorrowedBy(actor)))
                        return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected goods are held for another purpose");
                    var lot = goods.Lot;
                    if (plan.IsDelivery)
                    {
                        if (!PersonalEquipmentRules.IsCarried(lot, actor) || lot.ContainerLotId is not null ||
                            lot.DeliveryBuildingId != house.InstanceId || lot.Quantity != plan.Quantity ||
                            lot.Quantity > ChildHelpingLoad ||
                            PersonalEquipmentRules.CarriedQuantity(society.Checkpoint.Inventory, actor, state.Equipment) > ChildHelpingLoad ||
                            AvailableLotQuantity(lot) != lot.Quantity || !CanDeliverHouseDelivery(lot) ||
                            !ChildHelpingReachable(actor, state.Position, 0, house))
                            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the delivery can no longer be made");
                    }
                    else if (lot.CarrierId is not null || lot.ContainerLotId is not null || lot.StorageBuildingId is not null ||
                        lot.DeliveryBuildingId is not null || !ChildHelpingItem(lot.ItemKind) || OnBorrowedMarketStall(lot) ||
                        CarriedHouseDelivery(actor) is not null || ChildHelpingRoom(actor) < plan.Quantity ||
                        DestinationRoom(house.InstanceId) < plan.Quantity || HouseholdStockPosition(lot) != plan.Destination ||
                        !ChildHelpingReachable(actor, plan.Destination, plan.InteractionRange, house))
                        return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected load can no longer be carried home");
                }
                else
                {
                    var kind = choice.Kind == ChildActionKind.GatherFood ? "food" : "wood";
                    if (CarriedHouseDelivery(actor) is not null || ChildHelpingRoom(actor) == 0 || DestinationRoom(house.InstanceId) == 0 ||
                        map.Resources.FirstOrDefault(source => source.Id == choice.Target) is not { } resource ||
                        resource.Position != plan.Destination || !ChildHelpingSourceAvailable(actor, house, resource, kind) ||
                        (kind == "food" ? FoodHarvestQuantity(resource) : 1) != plan.Quantity)
                        return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected resource can no longer be gathered");
                }
            }
        }
        var route = Reach(actor, plan.Destination, social ? ReachPurpose.ChildSocial : ReachPurpose.ChildHelping,
            plan.InteractionRange, 0, SwimmingPolicy.WhenCapable);
        return route.Count > 0 ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "it's out of reach now");
    }

    private void ApplyChildPlan(ChildActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal)
        {
            RefuseChoice(plan, refusal);
            return;
        }
        ApplyCheckedChildPlan(plan);
    }

    private void ApplyCheckedChildPlan(ChildActionPlan plan)
    {
        if (plan.Choice.Kind is ChildActionKind.Converse or ChildActionKind.Play or ChildActionKind.Learn)
            ApplyChildSocial(plan);
        else if (plan.Choice.Kind == ChildActionKind.HelpFood)
        {
            var state = agents[plan.Actor];
            if (!IsWithinInteractionRange(state.Position, plan.Destination, plan.InteractionRange))
            {
                MoveToward(plan.Actor, state, plan.Destination, "child_help", plan.InteractionRange);
                return;
            }
            ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory,
                DerivedId.Create($"child-help:{WorldTick}:{plan.Actor}"), plan.Actor, HouseholdFor(plan.Actor), plan.StockId!, plan.Quantity,
                "child_household_help", plan.HouseId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId)));
            AppendEvent(new ChildHelpedHouseholdEventPayload(plan.Actor));
        }
        else ApplyChildHelpingPlan(plan);
    }

}
