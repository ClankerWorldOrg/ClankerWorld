using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using DeliveryOrderPlan = ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const int WarehouseLoadQuantity = 4;
    private static readonly HashSet<string> WarehouseResourceKinds =
        new(StringComparer.Ordinal) { "wood", "stone", "fiber", "tree_seed" };

    private PlacedBuilding? WarehouseForResident(string actor)
    {
        var townId = TownForResident(actor);
        return townId is null ? null : WarehousesForTown(townId).FirstOrDefault();
    }

    private IEnumerable<PlacedBuilding> WarehousesForTown(string townId)
    {
        var definitions = worldContent.Buildings.Where(definition =>
                definition.Tags.Contains("warehouse", StringComparer.Ordinal))
            .Select(definition => definition.CanonicalId).ToHashSet(StringComparer.Ordinal);
        return worldSimulation.Buildings.Where(building => building.TownId == townId &&
                definitions.Contains(building.DefinitionId))
            .OrderBy(building => building.InstanceId, StringComparer.Ordinal);
    }

    private IEnumerable<PlacedBuilding> WarehousesAccessibleTo(string actor)
    {
        if (!agents.ContainsKey(actor)) yield break;
        var residentTownId = TownForResident(actor);
        foreach (var town in towns.Where(item => item.Id == residentTownId || item.IsAbandoned)
                     .OrderBy(item => item.Id == residentTownId ? 0 : 1)
                     .ThenBy(item => item.Id, StringComparer.Ordinal))
            foreach (var warehouse in WarehousesForTown(town.Id))
                yield return warehouse;
    }

    private (PlacedBuilding Warehouse, GoodsMatch Source)? WarehouseMaterialSource(string actor, string itemKind,
        GridPoint destination, int destinationRange)
    {
        foreach (var warehouse in WarehousesAccessibleTo(actor).Where(warehouse => MayCollectWarehouseStock(actor, warehouse)))
            foreach (var match in WarehouseGoods(actor, warehouse, itemKind, GoodsUse.ReachableHoldings))
                if (PickupCarryCapacity(actor, match.Lot, destination, destinationRange) > 0)
                    return (warehouse, match);
        return null;
    }

    private bool MayCollectWarehouseStock(string actor, PlacedBuilding warehouse) =>
        agents.ContainsKey(actor) && warehouse.TownId is { } townId &&
        towns.SingleOrDefault(item => item.Id == townId) is { } town &&
        (TownForResident(actor) == townId || town.IsAbandoned) &&
        WarehousesForTown(townId).Any(item => item.InstanceId == warehouse.InstanceId);

    private IEnumerable<InventoryLot> AvailableWarehouseStock(string actor, string? itemKind = null) =>
        WarehouseStockGoods(actor, itemKind, GoodsUse.ReachableHoldings).Select(match => match.Lot);

    /// <summary>Town Warehouse stock the actor may collect, before checking for a route to it.</summary>
    private IEnumerable<InventoryLot> WarehouseStockLots(string actor, string? itemKind = null) =>
        WarehouseStockGoods(actor, itemKind, GoodsUse.Holdings).Select(match => match.Lot);

    private IEnumerable<GoodsMatch> WarehouseStockGoods(string actor, string? itemKind, GoodsUse use) =>
        WarehousesAccessibleTo(actor).Where(warehouse => MayCollectWarehouseStock(actor, warehouse))
            .SelectMany(warehouse => WarehouseGoods(actor, warehouse, itemKind, use));

    private IEnumerable<GoodsMatch> WarehouseGoods(string actor, PlacedBuilding warehouse, string? itemKind, GoodsUse use)
    {
        var kinds = itemKind is not null ? GoodsKinds.One(itemKind) : new GoodsKinds(
            InventoryIndex.For(society.Checkpoint.Inventory).StoredAt(warehouse.InstanceId)
                .Select(lot => lot.ItemKind).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        if (kinds.Ids.Count == 0) return [];
        return FindGoods(new GoodsRequest(use, actor, GoodsOwners.One(warehouse.TownId!), kinds,
                AtBuilding: warehouse.InstanceId)).Matches
            .Where(match => match.Lot.ContainerLotId is null);
    }

    private IEnumerable<InventoryLot> PersonalWarehouseLots(string actor, string? itemKind = null) =>
        SpendingGoods(actor, GoodsUse.Spend, [actor], itemKind).Select(match => match.Lot)
            .Where(lot => PersonalEquipmentRules.IsCarried(lot, actor) && WarehouseResourceKinds.Contains(lot.ItemKind))
            .OrderBy(lot => lot.Id, StringComparer.Ordinal);

    private long PersonalWarehouseAvailableQuantity(string actor, string itemKind) =>
        PersonalWarehouseLots(actor, itemKind).Sum(lot => (long)AvailableLotQuantity(lot));

    private (InventoryLot Lot, int Quantity)? PersonalWarehouseSurplus(string actor, string? itemKind = null,
        string? sourceLotId = null)
    {
        var lots = PersonalWarehouseLots(actor, itemKind).ToArray();
        // Keep four usable units of each kind, regardless of how storage or collection split them.
        // A bound source limits the transfer, not which carried lots can satisfy the personal reserve.
        var spareByKind = lots.GroupBy(lot => lot.ItemKind, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => Math.Max(0L, group.Sum(lot => (long)AvailableLotQuantity(lot)) - WarehouseLoadQuantity),
                StringComparer.Ordinal);
        foreach (var lot in lots)
        {
            if (sourceLotId is not null && lot.Id != sourceLotId) continue;
            var quantity = (int)Math.Min(AvailableLotQuantity(lot), spareByKind[lot.ItemKind]);
            if (quantity > 0) return (lot, quantity);
        }
        return null;
    }

    private void AddWarehouseStockCandidate(List<CognitionCandidate> candidates, string actor, AgentState state)
    {
        var choice = new TownStockRequest(TownStockActionKind.StoreSurplus, TownStockRequest.WarehouseCandidate);
        if (global::ClankerWorld.Simulation.World.WorldRuntime.TownStockActions.Warehouse.Offer(this, actor, choice) is not { } plan) return;
        candidates.Add(new CognitionCandidate(global::ClankerWorld.Simulation.World.WorldRuntime.TownStockActions.Warehouse.Encode(choice),
            $"Carry spare {plan.ItemKind} to the Town Warehouse as communal stock for residents.",
            32, plan.WarehouseId)
        { ActionPlan = plan });
    }

    private DeliveryOrderPlan? GetTownOrderPlan(string actor, AgentState person,
        OrderTask order, int maximumQuantity)
    {
        if (!IsAdult(actor) || TownForResident(actor) is not { } townId ||
            WarehousesForTown(townId).FirstOrDefault(building => DeliveryDestinationMatches(order, building)) is not { } warehouse ||
            PersonalWarehouseSurplus(actor, order.TargetItemKind, order.DeliveryLotId) is not { } surplus)
            return null;
        var quantity = Math.Min(maximumQuantity, Math.Min(DestinationRoom(warehouse.InstanceId),
            Math.Min(WarehouseLoadQuantity, surplus.Quantity)));
        return quantity <= 0 ? null : new DeliveryOrderPlan("town_surplus", warehouse, townId,
            surplus.Lot, surplus.Lot, quantity, quantity, DirectDelivery: true);
    }

    // Trial shelf target and load size. Goods above this target stay at their source.
    private const int StoreShelfTarget = 8;
    private sealed record StoreStockLoad(PlacedBuilding Store, InventoryLot Goods, int Quantity);

    private StoreStockLoad? NextStoreLoad(string actor, string? itemKind = null, string? buildingId = null,
        string? sourceLotId = null, bool allowOrdinaryProject = false)
    {
        if (!IsAdult(actor) || CarriedHouseDelivery(actor) is not null ||
            agents[actor].Project is { Stage: not ("completed" or "cancelled") } project &&
                (!allowOrdinaryProject || !OrdinaryProjectCanYieldToOrder(project)) ||
            society.Checkpoint.GetAgent(actor).HouseholdId is not { } householdId ||
            HouseholdBuildingWithTag(householdId, "store") is not { } store ||
            buildingId is not null && store.InstanceId != buildingId)
            return null;
        var inventory = society.Checkpoint.Inventory;
        var room = DestinationRoom(inventory, store.InstanceId);
        if (room == 0) return null;
        var protectedToolIds = MarketSystem.BestUsableToolIds(inventory, actor);
        var sources = StoreStockGoods(actor, householdId, store.InstanceId, itemKind);
        var foodSurpluses = new Dictionary<(string OwnerId, string ItemKind), int>();
        foreach (var lot in sources)
        {
            if (sourceLotId is not null && lot.Id != sourceLotId) continue;
            var quantity = StoreStockLoadLimit(actor, householdId, store, lot, sources, protectedToolIds, foodSurpluses);
            if (quantity <= 0 || !StoreStockTripIsReachable(actor, store, lot)) continue;
            return new(store, lot, quantity);
        }
        return null;
    }

    private void AddStoreStockCandidate(List<CognitionCandidate> candidates, string actor)
    {
        if (PlanStoreStock(actor) is { } plan && CheckActionPlan(plan).Allowed)
            candidates.Add(new("shop_stock_store",
                $"Carry {plan.Quantity} {plan.ItemKind} into the household Store before offering it for sale.",
                38, plan.StoreId)
            { ActionPlan = plan });
    }

    private void StockStore(string actor, AgentState state) => ApplyStoreStockPlan(PlanStoreStock(actor));

    private DeliveryOrderPlan? GetStoreOrderPlan(string actor, AgentState person,
        OrderTask order, int maximumQuantity)
    {
        if (NextStoreLoad(actor, order.TargetItemKind, order.TargetStorageBuildingId, order.DeliveryLotId,
                allowOrdinaryProject: true) is not { } load ||
            !DeliveryDestinationMatches(order, load.Store)) return null;
        var quantity = Math.Min(maximumQuantity, load.Quantity);
        return quantity <= 0 ? null : new DeliveryOrderPlan("store_stock", load.Store, load.Store.HouseholdId!,
            load.Goods, load.Goods, quantity, quantity, DirectDelivery: load.Goods.OwnerId == actor);
    }
    private FoodCargoActionPlan? PlanFoodCargo(string actor)
    {
        if (!agents.TryGetValue(actor, out var state) || HouseholdFor(actor) is not { } household ||
            !ActorCapabilities.Check(CapabilitiesFor(actor), "make_room_for_food", FoodCargoPolicy).Allowed)
            return null;
        var cargo = FoodRecoveryCargo(actor, state);
        if (cargo.Count == 0 || SpareCargoDestination(actor, state, cargo.Sum(move => move.PhysicalQuantity)) is not { } destination)
            return null;
        var moves = CaptureCargoMoves(cargo);
        return new(actor, household, moves, destination.Position, destination.Range,
            destination.StorageBuildingId, Reach(actor, destination.Position, ReachPurpose.FoodCargoStorage,
                destination.Range, 0, SwimmingPolicy.WhenCapable));
    }

    private System.Collections.ObjectModel.ReadOnlyCollection<PlannedCargoMove> CaptureCargoMoves(IReadOnlyList<SpareCargoMove> cargo)
    {
        var inventory = society.Checkpoint.Inventory;
        return Array.AsReadOnly(cargo.Select(move =>
        {
            var reservationQuantity = move.OrchardReservationId is { } reservationId ? inventory.GetReservation(reservationId).Quantity : 0;
            return new PlannedCargoMove(move.Lot.Id, move.Lot.OwnerId, move.Lot.ItemKind, move.TransferQuantity, move.PhysicalQuantity,
                InventoryContainerRules.IsContainer(move.Lot.ItemKind) ? GoodsPlanFamily.Capture(inventory, move.Lot) : null,
                move.OrchardReservationId, reservationQuantity, move.OrchardReservationId is null ? 0 :
                    Math.Max(0, move.TransferQuantity - (move.Lot.Quantity - reservationQuantity)));
        }).ToArray());
    }

    private static ActorCapabilityPolicy FoodCargoPolicy => new(ActionLifeStages.NonInfants,
        AllowUrgentWarmth: true, AllowActiveOrder: true);

    private ActionCheck CheckFoodCargoPlan(FoodCargoActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? FoodCargoPolicy);
        if (!capability.Allowed) return capability;
        var state = agents[plan.Actor];
        if (HouseholdFor(plan.Actor) != plan.HouseholdId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household has changed");
        if (FoodRoomMissing(plan.Actor, state) <= 0)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is already room for food");
        return CheckCargoStorage(plan.Storage);
    }

    private ActionCheck CheckCargoStorage(PlannedCargoStorage plan)
    {
        var state = agents[plan.Actor];
        var inventory = society.Checkpoint.Inventory;
        foreach (var move in plan.Cargo)
        {
            var request = new GoodsRequest(move.OrchardReservationId is null ? GoodsUse.SpareCargo : GoodsUse.OrchardFoodRecovery,
                plan.Actor, GoodsOwners.One(move.OwnerId), GoodsKinds.One(move.ItemKind));
            if (RecheckGoods(request, move.StockId) is not { } match || match.Quantity < move.Quantity ||
                !GoodsPlanFamily.Matches(inventory, match.Lot, move.Family))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected supplies are no longer available");
            if (move.OrchardReservationId is { } reservationId)
            {
                var reservation = OrchardReservationForFood(inventory, plan.Actor, match.Lot);
                if (!IsAdult(plan.Actor) || !NeedsUrgentFood(state) || PreferredFood(plan.Actor, plan.Actor).Any() ||
                    reservation is null || reservation.Id != reservationId || reservation.Quantity != move.OrchardReservationQuantity ||
                    Math.Max(0, move.Quantity - (match.Lot.Quantity - reservation.Quantity)) != move.ReleaseQuantity)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "those orchard seeds must remain reserved");
            }
        }
        if (plan.StorageBuildingId is { } houseId)
        {
            if (HouseForHousehold(plan.HouseholdId) is not { } house || house.InstanceId != houseId || house.Position != plan.Destination)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the household House has changed");
            if (DestinationRoom(houseId) < plan.Cargo.Sum(move => move.PhysicalQuantity))
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the household House has no room for these supplies");
        }
        else if (SharedStoragePosition != plan.Destination)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the storage pile has moved");
        return IsWithinInteractionRange(state.Position, plan.Destination, plan.InteractionRange) ||
            Reach(plan.Actor, plan.Destination, ReachPurpose.FoodCargoStorage, plan.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected storage place is out of reach now");
    }

    private void ApplyFoodCargoPlan(FoodCargoActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCargoStorage(plan.Storage);
    }

    private void ApplyCargoStorage(PlannedCargoStorage plan)
    {
        var state = agents[plan.Actor];
        if (!IsWithinInteractionRange(state.Position, plan.Destination, plan.InteractionRange))
        {
            MoveToward(plan.Actor, state, plan.Destination, "make_room", plan.InteractionRange);
            return;
        }
        foreach (var move in plan.Cargo)
        {
            RunInventoryCommand(InventorySystem.ApplyCargoStorage(InventoryEconomyWorld, move, plan));
            AppendEvent(new SpareCargoStoredEventPayload(plan.Actor, move.ItemKind, move.Quantity, plan.StorageBuildingId ?? "pile"));
        }
    }

    private static ActorCapabilityPolicy StoreStockPolicy => new(ActionLifeStages.Adults, AllowUrgentFood: false,
        AllowActiveOrder: true);

    private int StoreStockLotQuantity(string actor, InventoryLot lot, IReadOnlySet<string> protectedTools) => Math.Max(0,
        (lot.OwnerId == actor ? TransportSystem.SpareCarriedQuantity(this.HandcartWorld, actor, lot) : AvailableLotQuantity(lot)) -
        (protectedTools.Contains(lot.Id) ? 1 : 0));

    private int StoreStockLoadLimit(string actor, string household, PlacedBuilding store, InventoryLot lot,
        IReadOnlyList<InventoryLot> sources, IReadOnlySet<string> protectedTools,
        Dictionary<(string OwnerId, string ItemKind), int>? foodSurpluses = null)
    {
        var surplus = StoreStockLotQuantity(actor, lot, protectedTools);
        if (IsEdibleFood(lot.ItemKind))
        {
            var key = (lot.OwnerId, lot.ItemKind);
            if (foodSurpluses is null || !foodSurpluses.TryGetValue(key, out var foodSurplus))
            {
                var reserve = lot.OwnerId == actor ? 2 : 2 * society.Checkpoint.GetHousehold(household)
                    .MemberIds.Count(id => agents.ContainsKey(id));
                foodSurplus = Math.Max(0, sources.Where(source => source.OwnerId == lot.OwnerId && source.ItemKind == lot.ItemKind &&
                    StoreStockTripIsReachable(actor, store, source)).Sum(source => StoreStockLotQuantity(actor, source, protectedTools)) - reserve);
                foodSurpluses?.Add(key, foodSurplus);
            }
            surplus = Math.Min(surplus, foodSurplus);
        }
        var quantity = Math.Min(Math.Min(surplus, StoreShelfTarget - StoreShelfQuantity(store.InstanceId, lot.ItemKind)),
            Math.Min(HouseHaulLoadQuantity, DestinationRoom(store.InstanceId)));
        return lot.OwnerId == actor ? quantity : Math.Min(quantity, PickupCarryCapacity(actor, lot, store.Position));
    }

    private bool StoreStockTripIsReachable(string actor, PlacedBuilding store, InventoryLot lot)
    {
        var source = lot.OwnerId == actor ? agents[actor].Position : HouseholdStockPosition(lot);
        var range = lot.OwnerId == actor ? 0 : HouseholdStockInteractionRange(lot);
        return CanReach(actor, source, ReachPurpose.StoreStock, range, 0, SwimmingPolicy.WhenCapable) &&
            CanReachLeg(actor, source, store.Position, ReachPurpose.StoreStock, 0, 0, SwimmingPolicy.WhenCapable);
    }

    private StoreStockActionPlan? PlanStoreStock(string actor)
    {
        if (!ActorCapabilities.Check(CapabilitiesFor(actor), "shop_stock_store", StoreStockPolicy).Allowed ||
            NextStoreLoad(actor) is not { } load) return null;
        var direct = load.Goods.OwnerId == actor;
        var target = direct ? load.Store.Position : HouseholdStockPosition(load.Goods);
        var range = direct ? 0 : HouseholdStockInteractionRange(load.Goods);
        return new(actor, load.Store.HouseholdId!, load.Store.InstanceId, load.Store.Position, load.Goods.Id,
            load.Goods.OwnerId, load.Goods.ItemKind, load.Quantity, direct, target, range,
            Reach(actor, target, ReachPurpose.StoreStock, range, 0, SwimmingPolicy.WhenCapable),
            direct ? Array.AsReadOnly<GridPoint>([]) : ReachLeg(actor, target, load.Store.Position, ReachPurpose.StoreStock, 0, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckStoreStockPlan(StoreStockActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? StoreStockPolicy);
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || HouseholdBuildingWithTag(plan.HouseholdId, "store") is not { } store ||
            store.InstanceId != plan.StoreId || store.Position != plan.Destination || CarriedHouseDelivery(plan.Actor) is not null ||
            agents[plan.Actor].Project is { Stage: not ("completed" or "cancelled") })
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their Store or current work has changed");
        var request = new GoodsRequest(GoodsUse.Sell, plan.Actor, GoodsOwners.One(plan.OwnerId), GoodsKinds.One(plan.ItemKind));
        if (RecheckGoods(request, plan.StockId) is not { } match || !MayUseStoreStock(plan.Actor, plan.HouseholdId, plan.StoreId, match.Lot) ||
            plan.DirectDelivery != (match.Lot.OwnerId == plan.Actor) ||
            !plan.DirectDelivery && (HouseholdStockPosition(match.Lot) != plan.Target || HouseholdStockInteractionRange(match.Lot) != plan.InteractionRange))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected Store stock is no longer available");
        if (DestinationRoom(plan.StoreId) < plan.Quantity || StoreShelfTarget - StoreShelfQuantity(plan.StoreId, plan.ItemKind) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the Store has no room for the selected load");
        var sources = StoreStockGoods(plan.Actor, plan.HouseholdId, plan.StoreId);
        var protectedTools = MarketSystem.BestUsableToolIds(society.Checkpoint.Inventory, plan.Actor);
        if (StoreStockLoadLimit(plan.Actor, plan.HouseholdId, store, match.Lot, sources, protectedTools) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected load would use supplies that must stay");
        return StoreStockTripIsReachable(plan.Actor, store, match.Lot) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected Store trip is out of reach now");
    }

    private void ApplyStoreStockPlan(StoreStockActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedStoreStockPlan(plan);
    }

    private void ApplyCheckedStoreStockPlan(StoreStockActionPlan plan)
    {
        var state = agents[plan.Actor];
        if (!IsWithinInteractionRange(state.Position, plan.Target, plan.InteractionRange))
        {
            MoveToward(plan.Actor, state, plan.Target, "store_stock", plan.InteractionRange);
            return;
        }
        var operationId = DerivedId.Create($"{(plan.DirectDelivery ? "store-stock" : "store-pickup")}:{WorldTick}:{plan.Actor}");
        RunInventoryCommand(InventorySystem.ApplyCheckedStoreStockPlan(plan, operationId));
        AppendEvent(plan.DirectDelivery
            ? new StoreStockDeliveredEventPayload(plan.Actor, plan.StockId, plan.Quantity, plan.StoreId)
            : new StoreStockCollectedEventPayload(plan.Actor, plan.StockId, plan.Quantity, plan.StoreId));
    }

    private static class TownStockActions
    {
        internal static readonly ActionDefinition<TownStockRequest, TownStockActionPlan> Warehouse = Create(
            TownStockRequest.TryDecodeWarehouse,
            new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: true, AllowActiveOrder: true));
        internal static readonly ActionDefinition<TownStockRequest, TownStockActionPlan> Salvage = Create(
            TownStockRequest.TryDecodeSalvage, TownSystem.SalvageCapabilities);

        private static ActionDefinition<TownStockRequest, TownStockActionPlan> Create(
            ChoiceDecoder<TownStockRequest> decode, ActorCapabilityPolicy policy) => new(
            decode, static choice => choice.Encode(), policy,
            static (world, actor, choice, _) => choice.Kind == TownStockActionKind.StoreSurplus
                ? world.PlanWarehouseSurplus(actor)
                : world.townSystem.PlanTownSalvage(world.civicWorld, actor, choice.CandidateId),
            static (world, plan, capabilities) => world.CheckTownStockPlan(plan, capabilities),
            static (world, plan, _, _) =>
            {
                world.ApplyCheckedTownStockPlan(plan);
                return true;
            });
    }

    private TownStockActionPlan? PlanWarehouseSurplus(string actor)
    {
        if (WarehouseForResident(actor) is not { } warehouse) return null;
        var index = InventoryIndex.For(society.Checkpoint.Inventory);
        var surplus = PersonalWarehouseLots(actor)
            .Where(lot => InventoryHoldRules.Allows(index, lot, InventoryHoldRules.DeliveryFor(actor, warehouse.InstanceId)))
            .Select(lot => PersonalWarehouseSurplus(actor, lot.ItemKind, lot.Id)).FirstOrDefault(item => item is not null);
        if (surplus is not { } selected) return null;
        var quantity = Math.Min(DestinationRoom(warehouse.InstanceId), Math.Min(WarehouseLoadQuantity, selected.Quantity));
        return new(actor, new(TownStockActionKind.StoreSurplus, warehouse.TownId!, selected.Lot.Id), selected.Lot.ItemKind,
            quantity, warehouse.Position, 0, warehouse.InstanceId,
            Reach(actor, warehouse.Position, ReachPurpose.TownWarehouse, 0, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckTownStockPlan(TownStockActionPlan plan, ActorCapabilityPolicy policy)
    {
        if (plan.Choice.Kind == TownStockActionKind.Salvage) return townSystem.CheckTownSalvagePlan(civicWorld, plan, policy);
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy);
        if (!capability.Allowed) return capability;
        if (TownForResident(plan.Actor) != plan.Choice.TownId ||
            WarehousesForTown(plan.Choice.TownId).SingleOrDefault(building => building.InstanceId == plan.WarehouseId) is not { } warehouse ||
            warehouse.Position != plan.Destination)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Warehouse is no longer available");
        if (plan.Quantity <= 0 || DestinationRoom(warehouse.InstanceId) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the Warehouse no longer has room for that load");
        if (PersonalWarehouseSurplus(plan.Actor, plan.ItemKind, plan.Choice.StockId) is not { } surplus ||
            surplus.Quantity < plan.Quantity || !InventoryHoldRules.Allows(InventoryIndex.For(society.Checkpoint.Inventory), surplus.Lot,
                InventoryHoldRules.DeliveryFor(plan.Actor, plan.WarehouseId)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected spare goods are no longer available");
        return Reach(plan.Actor, plan.Destination, ReachPurpose.TownWarehouse, plan.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "it's out of reach now");
    }

    private void ApplyCheckedTownStockPlan(TownStockActionPlan plan)
    {
        if (plan.Choice.Kind == TownStockActionKind.Salvage)
        {
            TownSystem.ApplyTownSalvagePlan(civicWorld, plan);
            return;
        }
        var state = agents[plan.Actor];
        if (state.Position != plan.Destination)
        {
            MoveToward(plan.Actor, state, plan.Destination, "town_warehouse", plan.InteractionRange);
            return;
        }
        var lot = society.Checkpoint.Inventory.GetLot(plan.Choice.StockId);
        var availableBefore = PersonalWarehouseAvailableQuantity(plan.Actor, plan.ItemKind);
        var operation = WarehouseStockId(WorldTick, plan.Actor);
        RunInventoryCommand(InventorySystem.ApplyCheckedTownStockPlan(operation, plan));
        RecordNonLandGoodsCompletion(plan.Actor, lot, plan.Choice.TownId, plan.Quantity, plan.WarehouseId,
            operation, "public_service_goods", availableBefore);
        AppendEvent(new TownResourcesStoredEventPayload(plan.Actor, plan.Choice.StockId, plan.Quantity, plan.WarehouseId!));
    }
}
