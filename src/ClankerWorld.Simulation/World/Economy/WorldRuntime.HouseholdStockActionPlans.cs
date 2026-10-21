using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActionDefinition<HouseholdStockRequest, HouseholdStockActionPlan> HouseholdRecoveryDefinition => HouseholdStockActions.Recovery;
    private static ActionDefinition<HouseholdStockRequest, HouseholdStockActionPlan> HouseholdHaulingDefinition => HouseholdStockActions.Hauling;
    private static ActionDefinition<HouseholdStockRequest, HouseholdStockActionPlan> HouseholdFoodStorageDefinition => HouseholdStockActions.Food;

    private static class HouseholdStockActions
    {
        internal static readonly ActionDefinition<HouseholdStockRequest, HouseholdStockActionPlan> Recovery = Create(HouseholdStockRequestKind.RecoverDelivery);
        internal static readonly ActionDefinition<HouseholdStockRequest, HouseholdStockActionPlan> Hauling = Create(HouseholdStockRequestKind.Haul);
        internal static readonly ActionDefinition<HouseholdStockRequest, HouseholdStockActionPlan> Food = Create(HouseholdStockRequestKind.StoreFood);

        private static ActionDefinition<HouseholdStockRequest, HouseholdStockActionPlan> Create(HouseholdStockRequestKind kind) => new(
            (string candidate, out HouseholdStockRequest choice) => HouseholdStockRequest.TryDecode(candidate, out choice) && choice.Kind == kind,
            static choice => choice.Encode(),
            new(ActionLifeStages.Adults, AllowUrgentFood: kind == HouseholdStockRequestKind.RecoverDelivery,
                AllowUrgentWarmth: true, AllowActiveOrder: true),
            static (world, actor, choice, _) => world.PlanHouseholdStock(actor, choice),
            static (world, plan, policy) => world.CheckHouseholdStockPlan(plan, policy),
            static (world, plan, _, _) =>
            {
                world.ApplyCheckedHouseholdStockPlan(plan);
                return true;
            });
    }

    private HouseholdStockActionPlan? PlanHouseholdStock(string actor, HouseholdStockRequest choice)
    {
        if (!agents.TryGetValue(actor, out var state) || society.Checkpoint.GetAgent(actor).HouseholdId is not { } household)
            return null;
        InventoryLot? stock;
        PlacedBuilding? house;
        HouseholdStockActionKind kind;
        GridPoint destination;
        var range = 0;
        int quantity;
        if (choice.Kind == HouseholdStockRequestKind.RecoverDelivery)
        {
            stock = RecoverableHouseholdDelivery(actor, state);
            if (stock is null) return null;
            kind = HouseholdStockActionKind.RecoverDelivery;
            house = null;
            destination = SharedStoragePosition;
            range = ResourceInteractionRange;
            quantity = InventoryContainerRules.IsContainer(stock.ItemKind) ? 1 : stock.Quantity;
        }
        else if (choice.Kind == HouseholdStockRequestKind.StoreFood)
        {
            stock = PersonalSpareFood(actor);
            house = HouseForHousehold(household);
            if (stock is null || house is null) return null;
            kind = HouseholdStockActionKind.StoreFood;
            destination = house.Position;
            quantity = Math.Min(DestinationRoom(house.InstanceId), Math.Min(HouseHaulLoadQuantity, AvailableLotQuantity(stock) - 1));
        }
        else if (CarriedHouseDelivery(actor) is { } carried)
        {
            stock = carried;
            house = worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == carried.DeliveryBuildingId);
            if (house is null) return null;
            kind = HouseholdStockActionKind.Deliver;
            destination = house.Position;
            var room = DestinationRoom(house.InstanceId, carried);
            quantity = InventoryContainerRules.IsContainer(carried.ItemKind)
                ? HouseDeliveryPhysicalQuantity(society.Checkpoint.Inventory, carried) <= room ? 1 : 0
                : Math.Min(room, AvailableLotQuantity(carried));
        }
        else
        {
            house = HouseForHousehold(household);
            if (house is null) return null;
            if (BuildingPreparationToolToStore(actor, household) is { } tool &&
                Reach(actor, house.Position, ReachPurpose.HouseholdReturn, 0, 0, SwimmingPolicy.WhenCapable).Count > 0 &&
                InventoryHoldRules.Allows(InventoryIndex.For(society.Checkpoint.Inventory), tool, InventoryHoldRules.DeliveryFor(actor, house.InstanceId)))
            {
                stock = tool;
                kind = HouseholdStockActionKind.StorePreparationTool;
                destination = house.Position;
                quantity = Math.Min(1, AvailableLotQuantity(tool));
            }
            else
            {
                stock = UnlocatedHouseholdStock(household, actor, house.InstanceId);
                if (stock is null) return null;
                kind = HouseholdStockActionKind.Collect;
                destination = HouseholdStockPosition(stock);
                range = HouseholdStockInteractionRange(stock);
                quantity = HouseHaulPickupQuantity(actor, stock, house.InstanceId);
            }
        }
        var physical = InventoryContainerRules.IsContainer(stock.ItemKind)
            ? HouseDeliveryPhysicalQuantity(society.Checkpoint.Inventory, stock) : quantity;
        return new(actor, kind, household, stock.Id, stock.OwnerId, stock.ItemKind, quantity,
            physical, destination, range, house?.InstanceId, stock.DeliveryBuildingId,
            Reach(actor, destination, kind == HouseholdStockActionKind.Collect ? ReachPurpose.HouseholdPickup : ReachPurpose.HouseholdReturn,
                range, 0, SwimmingPolicy.WhenCapable),
            InventoryContainerRules.IsContainer(stock.ItemKind) ? GoodsPlanFamily.Capture(society.Checkpoint.Inventory, stock) : null);
    }

    private ActionCheck CheckHouseholdStockPlan(HouseholdStockActionPlan plan, ActorCapabilityPolicy policy)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy);
        if (!capability.Allowed) return capability;
        if (society.Checkpoint.GetAgent(plan.Actor).HouseholdId != plan.HouseholdId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household has changed");
        if (plan.Quantity <= 0) return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "there is no selected load to move");
        InventoryLot? stock;
        if (plan.Kind is HouseholdStockActionKind.Deliver or HouseholdStockActionKind.RecoverDelivery)
        {
            stock = CarriedDeliveryGoods(plan.Actor).SingleOrDefault(lot => lot.Id == plan.StockId);
            if (stock is null || stock.OwnerId != plan.OwnerId || stock.ItemKind != plan.ItemKind ||
                stock.DeliveryBuildingId != plan.DeliveryBuildingId || !GoodsPlanFamily.Matches(society.Checkpoint.Inventory, stock, plan.Family))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected delivery has changed");
        }
        else if (plan.Kind == HouseholdStockActionKind.StoreFood)
        {
            stock = PersonalSpareFood(plan.Actor, plan.ItemKind, plan.StockId);
            if (stock is null || AvailableLotQuantity(stock) - 1 < plan.Quantity ||
                agents[plan.Actor].FullnessBasisPoints < NeedThresholds.SpareFoodStorageFullness)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected spare food is no longer available");
        }
        else if (plan.Kind == HouseholdStockActionKind.StorePreparationTool)
        {
            stock = BuildingPreparationToolToStore(plan.Actor, plan.HouseholdId, plan.StockId);
            if (stock is null || stock.OwnerId != plan.OwnerId || AvailableLotQuantity(stock) < plan.Quantity)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected tool no longer needs storing");
        }
        else
        {
            stock = HouseholdStockGoods(plan.HouseholdId, plan.Actor, plan.HouseId).SingleOrDefault(lot => lot.Id == plan.StockId);
            if (stock is null || stock.ItemKind != plan.ItemKind || stock.CarrierId is not null || stock.ContainerLotId is not null ||
                stock.StorageBuildingId is not null || stock.DeliveryBuildingId is not null || OnBorrowedMarketStall(stock) ||
                !GoodsPlanFamily.Matches(society.Checkpoint.Inventory, stock, plan.Family) || UnusableDeliveryStock(society.Checkpoint.Inventory, stock) ||
                FarmFieldRules.IsFarmStock(stock.ItemKind) && FarmhouseForHousehold(plan.HouseholdId) is not null ||
                HouseHaulPickupQuantity(plan.Actor, stock, plan.HouseId!) < plan.Quantity ||
                HouseholdStockPosition(stock) != plan.Destination || HouseholdStockInteractionRange(stock) != plan.InteractionRange)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected household load is no longer available");
            if (CarriedHouseDelivery(plan.Actor) is not null)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the current delivery must finish first");
            if (ReachLeg(plan.Actor, plan.Destination,
                worldSimulation.Buildings.Single(building => building.InstanceId == plan.HouseId).Position,
                ReachPurpose.HouseholdReturn, 0, 0, SwimmingPolicy.WhenCapable).Count == 0)
                return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected load cannot get back to the House");
        }
        if (plan.Kind == HouseholdStockActionKind.RecoverDelivery)
        {
            var equipment = agents[plan.Actor].Equipment;
            if (stock.OwnerId != plan.Actor || stock.Quantity < plan.Quantity ||
                worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == stock.DeliveryBuildingId)?.HouseholdId != plan.HouseholdId ||
                stock.Id == equipment?.ClothingLotId || stock.Id == equipment?.CarryAidLotId || stock.Id == equipment?.Repair?.LotId ||
                stock.ItemKind is "field_map" or "field_record" || !UnusableDeliveryStock(society.Checkpoint.Inventory, stock) ||
                HasActiveContainerReservation(society.Checkpoint.Inventory, stock.Id) || SharedStoragePosition != plan.Destination)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected supplies no longer need recovery");
        }
        else
        {
            var house = worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == plan.HouseId);
            if (house is null || house.HouseholdId != plan.HouseholdId || plan.Kind != HouseholdStockActionKind.Collect && house.Position != plan.Destination)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected household House has changed");
            if (plan.Kind == HouseholdStockActionKind.Deliver &&
                (stock.OwnerId != plan.Actor || HasActiveContainerReservation(society.Checkpoint.Inventory, stock.Id) ||
                    UnusableDeliveryStock(society.Checkpoint.Inventory, stock) || !CanDeliverHouseDelivery(stock) || AvailableLotQuantity(stock) < plan.Quantity) ||
                plan.Kind != HouseholdStockActionKind.Deliver && DestinationRoom(house.InstanceId) < plan.PhysicalQuantity)
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the House no longer has room for that load");
        }
        var access = plan.Kind == HouseholdStockActionKind.Collect ? InventoryHoldRules.BorrowedBy(plan.Actor) :
            InventoryHoldRules.DeliveryFor(plan.Actor, plan.DeliveryBuildingId ?? plan.HouseId);
        var index = InventoryIndex.For(society.Checkpoint.Inventory);
        if (index.ContentsOf(stock.Id).Prepend(stock).Any(member => !InventoryHoldRules.Allows(index, member, access)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected supplies are held for another purpose");
        return Reach(plan.Actor, plan.Destination, ReachPurpose.HouseholdReturn, plan.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "it's out of reach now");
    }

    private void ApplyHouseholdStockPlan(HouseholdStockActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedHouseholdStockPlan(plan);
    }

    private void ApplyCheckedHouseholdStockPlan(HouseholdStockActionPlan plan)
    {
        var state = agents[plan.Actor];
        if (!IsWithinInteractionRange(state.Position, plan.Destination, plan.InteractionRange))
        {
            var reason = plan.Kind == HouseholdStockActionKind.RecoverDelivery ? "recover_household_delivery" :
                plan.Kind == HouseholdStockActionKind.StoreFood ? "household_food" : "household_stock";
            MoveToward(plan.Actor, state, plan.Destination, reason, plan.InteractionRange);
            return;
        }
        if (plan.Kind == HouseholdStockActionKind.Collect)
        {
            RunInventoryCommand(InventorySystem.CollectHouseholdHaul(InventoryEconomyWorld, plan));
            AppendEvent(new HouseholdStockPickedUpEventPayload(plan.Actor, plan.StockId, plan.Quantity, plan.HouseId!));
        }
        else if (plan.Kind == HouseholdStockActionKind.RecoverDelivery)
        {
            RunInventoryCommand(InventorySystem.RecoverHouseDelivery(InventoryEconomyWorld, plan));
            AppendEvent(new HouseholdDeliveryRecoveredEventPayload(plan.Actor, plan.StockId, plan.PhysicalQuantity));
        }
        else if (plan.Kind == HouseholdStockActionKind.StorePreparationTool)
        {
            RunInventoryCommand(InventorySystem.CollectHousePreparationTool(InventoryEconomyWorld, plan));
            AppendEvent(new HouseholdPreparationToolStoredEventPayload(plan.Actor, plan.ItemKind, plan.Quantity, plan.HouseId!));
        }
        else
        {
            var food = plan.Kind == HouseholdStockActionKind.StoreFood;
            var operation = food ? DerivedId.Create($"house-food-storage:{WorldTick}:{plan.Actor}") : DerivedId.Create($"house-haul-delivery:{WorldTick}:{plan.Actor}");
            var eventKind = food ? "household_food_stored" : "household_stock_delivered";
            RunInventoryCommand(InventorySystem.CollectHouseholdBuildingStock(operation, plan, eventKind));
            AppendEvent(food
                ? new HouseholdFoodStoredEventPayload(plan.Actor, plan.StockId, plan.Quantity, plan.HouseId!)
                : new HouseholdStockDeliveredEventPayload(plan.Actor, plan.StockId, plan.Quantity, plan.HouseId!));
        }
    }
}
