using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    // Initial small-load and short-trip limits, independent of adult equipment upgrades.
    private const int ChildHelpingLoad = 4;
    private const int ChildHelpingRoute = 8;

    private PlacedBuilding? ChildHelpingHouse(string actor, AgentState state) =>
        IsChild(actor) && state.FullnessBasisPoints >= NeedThresholds.ChildHelpingFullness && !NeedsUrgentWarmth(state) &&
        society.Checkpoint.GetAgent(actor).HouseholdId is { } household &&
        HouseForHousehold(household) is { } house &&
        map.FootDistance(state.Position, house.Position) <= ChildHelpingRoute ? house : null;

    private int ChildHelpingRoom(string actor) => Math.Min(FreeCarryCapacity(actor), Math.Max(0,
        ChildHelpingLoad - PersonalEquipmentRules.CarriedQuantity(society.Checkpoint.Inventory, actor,
            agents[actor].Equipment)));

    private bool ChildHelpingReachable(string actor, GridPoint source, int range, PlacedBuilding house)
    {
        bool ShortRoute(GridPoint from, GridPoint to, int interactionRange)
        {
            if (IsWithinInteractionRange(from, to, interactionRange)) return true;
            var route = ReachLeg(actor, from, to, ReachPurpose.ChildHelping, interactionRange, 0, SwimmingPolicy.WhenCapable);
            return route.Count > 0 && route.Count - 1 <= ChildHelpingRoute &&
                route.All(point => map.FootDistance(point, house.Position) <= ChildHelpingRoute);
        }
        return map.FootDistance(source, house.Position) <= ChildHelpingRoute &&
            ShortRoute(agents[actor].Position, source, range) && ShortRoute(source, house.Position, 0);
    }

    private static bool ChildHelpingItem(string kind) => kind == "wood" || IsEdibleFood(kind);

    private InventoryLot? ChildHelpingDelivery(string actor, PlacedBuilding house) =>
        FindGoods(ChildHelpingGoodsRequest(actor, actor)).Matches
            .Where(match => ChildGoodsCanMove(match, InventoryHoldRules.DeliveryFor(actor, house.InstanceId)))
            .Select(match => match.Lot).Where(lot => lot.OwnerId == actor &&
            PersonalEquipmentRules.IsCarried(lot, actor) && lot.ContainerLotId is null &&
            lot.DeliveryBuildingId == house.InstanceId && ChildHelpingItem(lot.ItemKind) &&
            lot.Quantity <= ChildHelpingLoad &&
            PersonalEquipmentRules.CarriedQuantity(society.Checkpoint.Inventory, actor, agents[actor].Equipment) <= ChildHelpingLoad && AvailableLotQuantity(lot) == lot.Quantity &&
            CanDeliverHouseDelivery(lot)).OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();

    private InventoryLot? ChildHelpingStock(string actor, PlacedBuilding house) =>
        FindGoods(ChildHelpingGoodsRequest(actor, house.HouseholdId!)).Matches
            .Where(match => ChildGoodsCanMove(match, InventoryHoldRules.BorrowedBy(actor)))
            .Select(match => match.Lot).Where(lot => lot.OwnerId == house.HouseholdId &&
            lot.CarrierId is null && lot.ContainerLotId is null && lot.StorageBuildingId is null &&
            lot.DeliveryBuildingId is null && ChildHelpingItem(lot.ItemKind) && !OnBorrowedMarketStall(lot) &&
            AvailableLotQuantity(lot) > 0 && ChildHelpingReachable(actor, HouseholdStockPosition(lot),
                HouseholdStockInteractionRange(lot), house))
            .OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();

    private static GoodsRequest ChildHelpingGoodsRequest(string actor, string owner) =>
        new(GoodsUse.ConsumeAt, actor, GoodsOwners.One(owner), new([.. FoodRules.EdibleKinds, "wood"]));

    private static bool ChildGoodsCanMove(GoodsMatch goods, IReadOnlyList<InventoryHold> access) =>
        goods.RequiredHolds.All(access.Contains);

    private bool ChildHelpingSourceAvailable(string actor, PlacedBuilding house, MapResource source, string kind) =>
        resources.GetValueOrDefault(source.Id) == ResourceState.Available &&
        (kind == "food" ? source.Kind is "food" or "fruit" && source.TreeKind is null &&
            FoodHarvestCarryUnits(source) <= ChildHelpingRoom(actor) &&
            DestinationRoom(house.InstanceId) >= FoodHarvestQuantity(source)
            : source.TreeKind is null && (source.Kind == "wood" || source.NaturalObjectKind == "fallen_wood")) &&
        ChildHelpingReachable(actor, source.Position, ResourceInteractionRange, house);

    private MapResource? ChildHelpingSource(string actor, PlacedBuilding house, string kind) => map.Resources
        .Where(source => ChildHelpingSourceAvailable(actor, house, source, kind))
        .OrderBy(source => map.FootDistance(agents[actor].Position, source.Position))
        .ThenBy(source => source.Id, StringComparer.Ordinal).FirstOrDefault();

    private void AddChildHelpingCandidates(List<CognitionCandidate> candidates, string actor, AgentState state)
    {
        if (ChildHelpingHouse(actor, state) is not { } house) return;
        if (ChildHelpingDelivery(actor, house) is { } delivery)
        {
            if (ChildHelpingReachable(actor, state.Position, 0, house))
                OfferChildChoice(candidates, actor, new(ChildActionKind.Carry, delivery.Id), "Return your small delivery to your household's House.", 20);
            return;
        }
        if (CarriedHouseDelivery(actor) is not null || ChildHelpingRoom(actor) == 0 ||
            DestinationRoom(house.InstanceId) == 0) return;
        if (ChildHelpingStock(actor, house) is { } stock)
            OfferChildChoice(candidates, actor, new(ChildActionKind.Carry, stock.Id), "Carry a small loose household food or wood load to your House.", 28);
        foreach (var kind in new[] { "food", "wood" })
            if (ChildHelpingSource(actor, house, kind) is { } source)
                OfferChildChoice(candidates, actor, new(kind == "food" ? ChildActionKind.GatherFood : ChildActionKind.GatherWood, source.Id),
                    kind == "food" ? "Gather nearby wild food for your household, then carry it home." :
                        "Pick up nearby fallen wood for your household, then carry it home.", 30);
    }

    private void ApplyChildHelpingCandidate(string actor, AgentState state, string candidate) =>
        ApplyChildPlan(PlanChildCandidate(actor, candidate));

    private void ApplyChildHelpingPlan(ChildActionPlan plan)
    {
        var actor = plan.Actor;
        var state = agents[actor];
        if (!IsWithinInteractionRange(state.Position, plan.Destination, plan.InteractionRange))
        {
            MoveToward(actor, state, plan.Destination, "child_help", plan.InteractionRange);
            return;
        }
        if (plan.Choice.Kind == ChildActionKind.Carry)
        {
            if (plan.IsDelivery)
            {
                ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory,
                    $"child-delivery:{WorldTick}:{actor}", actor, plan.HouseholdId!, plan.StockId!, plan.Quantity,
                    "child_household_delivery", plan.HouseId, holdAccess: InventoryHoldRules.DeliveryFor(actor, plan.HouseId)));
                AppendEvent("child_delivered_household", $"{actor}:{plan.ItemKind}:{plan.Quantity}:{plan.HouseId}");
            }
            else
            {
                ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory,
                    $"child-pickup:{WorldTick}:{actor}", plan.HouseholdId!, actor, plan.StockId!, plan.Quantity,
                    "child_household_pickup", destinationDeliveryBuildingId: plan.HouseId, holdAccess: InventoryHoldRules.BorrowedBy(actor)));
                AppendEvent("child_collected_household", $"{actor}:{plan.ItemKind}:{plan.Quantity}");
            }
            return;
        }
        var resource = map.Resources.Single(source => source.Id == plan.Choice.Target);
        if (plan.Choice.Kind == ChildActionKind.GatherWood)
            GatherProjectMaterial(actor, state, "wood", resource, deliveryBuildingId: plan.HouseId);
        else if (HarvestFood(actor, state, resource) is { } food)
            ApplyInventoryTransition(inventory => MarkBuildingMaterialDelivery(inventory, food.LotId, plan.HouseId!));
    }
}
