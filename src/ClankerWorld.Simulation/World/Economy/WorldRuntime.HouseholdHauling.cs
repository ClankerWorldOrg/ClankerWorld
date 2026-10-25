using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const int HouseHaulLoadQuantity = 4;

    private InventoryLot? CarriedHouseDelivery(string actor) => CarriedDeliveryGoods(actor)
        .Where(lot => InventoryContainerRules.IsContainer(lot.ItemKind)
            ? !HasActiveContainerReservation(society.Checkpoint.Inventory, lot.Id) && !UnusableDeliveryStock(society.Checkpoint.Inventory, lot)
            : AvailableLotQuantity(lot) > 0)
        .OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();

    private static bool UnusableDeliveryStock(InventoryCheckpoint inventory, InventoryLot root) =>
        root.ConditionBasisPoints == 0 || root.FreshnessBasisPoints == 0 ||
        InventoryContainerRules.IsContainer(root.ItemKind) && InventoryIndex.For(inventory).ContentsOf(root.Id).Any(lot =>
            lot.ConditionBasisPoints == 0 || lot.FreshnessBasisPoints == 0);

    private InventoryLot? RecoverableHouseholdDelivery(string actor, AgentState state)
    {
        if (!IsAdult(actor) || society.Checkpoint.GetAgent(actor).HouseholdId is not { } householdId ||
            !IsWithinInteractionRange(state.Position, SharedStoragePosition, ResourceInteractionRange) &&
            Reach(actor, SharedStoragePosition, ReachPurpose.HouseholdRecovery, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count == 0)
            return null;
        var inventory = society.Checkpoint.Inventory;
        var equipment = state.Equipment;
        return CarriedDeliveryGoods(actor).Where(lot => lot.OwnerId == actor && PersonalEquipmentRules.IsCarried(lot, actor) &&
                lot.ContainerLotId is null && lot.Quantity > 0 && lot.DeliveryBuildingId is { } destinationId &&
                worldSimulation.Buildings.Any(building => building.InstanceId == destinationId && building.HouseholdId == householdId) &&
                lot.Id != equipment?.ClothingLotId && lot.Id != equipment?.CarryAidLotId && lot.Id != equipment?.Repair?.LotId &&
                lot.ItemKind is not ("field_map" or "field_record") &&
                UnusableDeliveryStock(inventory, lot) && !HasActiveContainerReservation(inventory, lot.Id))
            .OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();
    }

    private void AddRecoverHouseholdDeliveryCandidate(List<CognitionCandidate> candidates, string actor, AgentState state)
    {
        if (global::ClankerWorld.Simulation.World.WorldRuntime.HouseholdStockActions.Recovery.Offer(this, actor, new(HouseholdStockRequestKind.RecoverDelivery)) is { } plan)
            candidates.Add(new(global::ClankerWorld.Simulation.World.WorldRuntime.HouseholdStockActions.Recovery.Encode(new(HouseholdStockRequestKind.RecoverDelivery)), "Return unusable delivery supplies to your household's storage pile.", 18)
            { ActionPlan = plan });
    }

    private static int HouseDeliveryPhysicalQuantity(InventoryCheckpoint inventory, InventoryLot carried) =>
        InventoryContainerRules.IsContainer(carried.ItemKind)
            ? ContainerFamilyQuantity(inventory, carried.Id)
            : carried.Quantity;

    private bool CanDeliverHouseDelivery(InventoryLot carried)
    {
        var inventory = society.Checkpoint.Inventory;
        var familyQuantity = HouseDeliveryPhysicalQuantity(inventory, carried);
        return familyQuantity <= DestinationRoom(carried.DeliveryBuildingId!, carried);
    }

    private int HouseHaulPickupQuantity(string actor, InventoryLot stock, string destinationId)
    {
        var inventory = society.Checkpoint.Inventory;
        var room = DestinationRoom(destinationId);
        var destination = worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == destinationId);
        var capacity = destination is null ? 0 : Math.Min(PickupCarryCapacity(actor, stock, destination.Position), room);
        if (capacity == 0)
            return 0;
        return InventoryContainerRules.IsContainer(stock.ItemKind)
            ? ContainerFamilyQuantity(inventory, stock.Id) <= capacity ? 1 : 0
            : Math.Min(HouseHaulLoadQuantity, Math.Min(capacity, AvailableLotQuantity(stock)));
    }

    /// <summary>
    /// Loose household stock to carry to the House. With an actor and House,
    /// only stock that actor can pick up and deliver now is returned, so an
    /// unreachable pile or oversized vessel does not hide usable stock.
    /// </summary>
    private InventoryLot? UnlocatedHouseholdStock(string householdId, string? actor = null,
        string? houseId = null, string? itemKind = null, int maximumQuantity = int.MaxValue) =>
        HouseholdStockGoods(householdId, actor, houseId)
            .Where(lot => lot.CarrierId is null && lot.ContainerLotId is null &&
                lot.StorageBuildingId is null && lot.DeliveryBuildingId is null && !OnBorrowedMarketStall(lot) &&
                (itemKind is null || DeliveryResourceQuantity(lot, itemKind) > 0 &&
                    (!InventoryContainerRules.IsContainer(lot.ItemKind) || DeliveryResourceQuantity(lot, itemKind) <= maximumQuantity)) &&
                (InventoryContainerRules.IsContainer(lot.ItemKind)
                    ? !HasActiveContainerReservation(society.Checkpoint.Inventory, lot.Id) &&
                        !UnusableDeliveryStock(society.Checkpoint.Inventory, lot)
                    : AvailableLotQuantity(lot) > 0) &&
                (!FarmFieldRules.IsFarmStock(lot.ItemKind) || FarmhouseForHousehold(householdId) is null))
            .OrderBy(lot => lot.ItemKind == "food" ? 0 : 1)
            .ThenBy(lot => lot.Id, StringComparer.Ordinal)
            .FirstOrDefault(lot => actor is null || houseId is null ||
                HouseHaulPickupQuantity(actor, lot, houseId) > 0);

    private IEnumerable<InventoryLot> HouseholdStockGoods(string householdId, string? actor, string? houseId)
    {
        var kinds = InventoryIndex.For(society.Checkpoint.Inventory).OwnedBy(householdId)
            .Select(lot => lot.ItemKind).Distinct(StringComparer.Ordinal).ToArray();
        if (kinds.Length == 0) return [];
        var request = new GoodsRequest(actor is not null && houseId is not null ? GoodsUse.Deliver : GoodsUse.Holdings,
            actor, GoodsOwners.One(householdId), new GoodsKinds(kinds), Destination: actor is not null ? houseId : null);
        return FindGoods(request).Matches.Select(match => match.Lot);
    }

    private void AddHouseHaulCandidate(List<CognitionCandidate> candidates, string actor, AgentState state)
    {
        var household = society.Checkpoint.GetAgent(actor).HouseholdId;
        if (household is null) return;
        var carried = CarriedHouseDelivery(actor);
        if (carried is null && (HouseForHousehold(household) is not { } house || DestinationRoom(house.InstanceId) == 0)) return;
        if (global::ClankerWorld.Simulation.World.WorldRuntime.HouseholdStockActions.Hauling.Offer(this, actor, new(HouseholdStockRequestKind.Haul)) is { } plan)
        {
            var description = plan.Kind switch
            {
                HouseholdStockActionKind.Deliver => "Deliver already collected household supplies to their building.",
                HouseholdStockActionKind.StorePreparationTool => "Store a carried work tool in the household House so building materials fit in your load.",
                _ => "Carry a load of household supplies to the household's House.",
            };
            var priority = plan.Kind == HouseholdStockActionKind.Deliver ? 18 : plan.Kind == HouseholdStockActionKind.StorePreparationTool ? 24 : 28;
            candidates.Add(new(global::ClankerWorld.Simulation.World.WorldRuntime.HouseholdStockActions.Hauling.Encode(new(HouseholdStockRequestKind.Haul)), description, priority, plan.HouseId) { ActionPlan = plan });
            if (plan.Kind is HouseholdStockActionKind.Deliver or HouseholdStockActionKind.StorePreparationTool) return;
        }
        if (carried is not null) return;
        if (global::ClankerWorld.Simulation.World.WorldRuntime.HouseholdStockActions.Food.Offer(this, actor, new(HouseholdStockRequestKind.StoreFood)) is { } food)
            candidates.Add(new(global::ClankerWorld.Simulation.World.WorldRuntime.HouseholdStockActions.Food.Encode(new(HouseholdStockRequestKind.StoreFood)), "Bring personally carried spare food into the household House.", 30, food.HouseId)
            { ActionPlan = food });
    }

    private InventoryLot? PersonalSpareFood(string actor, string? itemKind = null, string? sourceLotId = null) => PreferredFood(actor, actor)
        .FirstOrDefault(lot => (itemKind is null || lot.ItemKind == itemKind) &&
            (sourceLotId is null || lot.Id == sourceLotId) && lot.ContainerLotId is null && lot.DeliveryBuildingId is null &&
            !InventoryContainerRules.IsContainer(lot.ItemKind) && AvailableLotQuantity(lot) > 1);

    private void HaulHouseholdStock(string actor, AgentState state) =>
        ApplyHouseholdStockPlan(global::ClankerWorld.Simulation.World.WorldRuntime.HouseholdStockActions.Hauling.Plan(this, actor, new(HouseholdStockRequestKind.Haul)));

    /// <summary>
    /// A hungry resident past infancy whose load leaves no room for a food pickup may make room
    /// by setting down spare supplies for the household. Maps, field records,
    /// worn gear, work reservations and delivery loads stay carried. Urgent
    /// adult food recovery may release only the orchard seeds it actually sets down.
    /// </summary>
    private void AddMakeRoomForFoodCandidate(List<CognitionCandidate> candidates, string actor,
        AgentState state, int priority)
    {
        if (PlanFoodCargo(actor) is not { } plan || !CheckActionPlan(plan).Allowed) return;
        var existing = candidates.FindIndex(candidate => candidate.Id == "make_room_for_food");
        if (existing >= 0)
        {
            candidates[existing] = candidates[existing] with
            {
                DeterministicPriority = Math.Min(candidates[existing].DeterministicPriority, priority),
            };
            return;
        }
        candidates.Add(new("make_room_for_food",
            "Set down spare supplies with your household so food fits in your load.", priority,
            plan.StorageBuildingId)
        { ActionPlan = plan });
    }

    private int FoodRoomMissing(string actor, AgentState state)
    {
        var required = state.FullnessBasisPoints < NeedThresholds.FoodPickupFullness && !PreferredFood(actor, actor).Any()
            ? MinimumFoodPickupCarryUnits(actor, state.Position, forDependent: false) : 0;
        var dependentRequired = FamilySystem.CaregiverFoodCarryRequirement(this.FamilyParenthoodWorld, actor, state);
        if (dependentRequired > 0)
            required = required > 0 ? Math.Min(required, dependentRequired) : dependentRequired;
        return Math.Max(0, required - FreeCarryCapacity(actor));
    }

    internal sealed record SpareCargoMove(InventoryLot Lot, int TransferQuantity, int PhysicalQuantity,
        Identity? OrchardReservationId = null);

    private List<SpareCargoMove> SpareCargoForFood(string actor, int missing, string? protectedLotId = null,
        params string[] additionallyProtectedLotIds)
    {
        var moves = OrdinarySpareCargo(actor, missing, protectedLotId, additionallyProtectedLotIds);
        return moves.Sum(move => move.PhysicalQuantity) >= missing ? moves : [];
    }

    private List<SpareCargoMove> OrdinarySpareCargo(string actor, int missing, string? protectedLotId = null,
        IReadOnlyList<string>? additionallyProtectedLotIds = null,
        IReadOnlyDictionary<string, int>? retainedLotQuantities = null)
    {
        if (missing <= 0) return [];
        var inventory = society.Checkpoint.Inventory;
        var spare = SpareCargoGoods(actor)
            .Where(lot => lot.Id != protectedLotId &&
                additionallyProtectedLotIds?.Contains(lot.Id, StringComparer.Ordinal) != true)
        // Tools speed up later gathering, so they are set down last.
            .OrderBy(lot => lot.ItemKind is "tool" or "wooden_axe" or "wooden_pickaxe" ? 1 : 0)
            .ThenBy(lot => lot.Id, StringComparer.Ordinal);
        var moves = new List<SpareCargoMove>();
        foreach (var lot in spare)
        {
            var available = Math.Max(0, PhysicalUnreservedQuantity(lot) - (retainedLotQuantities?.GetValueOrDefault(lot.Id) ?? 0));
            if (available == 0) continue;
            var vessel = InventoryContainerRules.IsContainer(lot.ItemKind);
            var quantity = vessel ? 1 : Math.Min(missing, available);
            var physicalQuantity = vessel ? ContainerFamilyQuantity(inventory, lot.Id) : quantity;
            moves.Add(new(lot, quantity, physicalQuantity));
            missing -= physicalQuantity;
            if (missing <= 0) return moves;
        }
        return moves;
    }

    private List<SpareCargoMove> FoodRecoveryCargo(string actor, AgentState state)
    {
        var missing = FoodRoomMissing(actor, state);
        var moves = OrdinarySpareCargo(actor, missing);
        missing -= moves.Sum(move => move.PhysicalQuantity);
        if (missing <= 0) return moves;
        if (!IsAdult(actor) || !NeedsUrgentFood(state) || PreferredFood(actor, actor).Any()) return [];

        var inventory = society.Checkpoint.Inventory;
        foreach (var lot in OrchardFoodRecoveryGoods(actor))
        {
            if (OrchardReservationForFood(inventory, actor, lot) is not { } reservation) continue;
            var existing = moves.FindIndex(move => move.Lot.Id == lot.Id);
            var selected = existing >= 0 ? moves[existing].TransferQuantity : 0;
            var quantity = Math.Min(missing, lot.Quantity - selected);
            if (quantity <= 0) continue;
            var combined = new SpareCargoMove(lot, selected + quantity, selected + quantity, reservation.Id);
            if (existing >= 0) moves[existing] = combined;
            else moves.Add(combined);
            missing -= quantity;
            if (missing <= 0) return moves;
        }
        return [];
    }

    private InventoryReservation? OrchardReservationForFood(InventoryCheckpoint inventory, string actor, InventoryLot lot)
    {
        if (lot.OwnerId != actor || lot.ItemKind != TreeGrowthRules.OrchardSeedItem ||
            !PersonalEquipmentRules.IsCarried(lot, actor) || lot.ContainerLotId is not null ||
            lot.DeliveryBuildingId is not null || lot.ConditionBasisPoints <= 0 || lot.FreshnessBasisPoints <= 0 ||
            PersonalEquipmentRules.IsSelected(agents[actor].Equipment, lot.Id))
            return null;
        var claims = inventory.Reservations.Where(reservation => reservation.LotId == lot.Id && reservation.State is
            InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed or InventoryReservationState.Committed).ToArray();
        if (claims.Length != 1) return null;
        var claim = claims[0];
        return claim.Id == OrchardReplantingReservationId(lot.Id) && claim.OwnerId == actor &&
            claim.Purpose == "orchard_replanting" && claim.ExpiryTick >= WorldTick &&
            claim.State is (InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed) &&
            claim.Quantity > 0 && claim.Quantity <= lot.Quantity ? claim : null;
    }

    /// <summary>The household House when it has room, otherwise the household's storage pile.</summary>
    private (GridPoint Position, int Range, string? StorageBuildingId)? SpareCargoDestination(string actor,
        AgentState state, int quantity)
    {
        var householdId = society.Checkpoint.GetAgent(actor).HouseholdId!;
        if (HouseForHousehold(householdId) is { } house && DestinationRoom(house.InstanceId) >= quantity &&
            (state.Position == house.Position || Reach(actor, house.Position, ReachPurpose.FoodCargoStorage, 0, 0, SwimmingPolicy.WhenCapable).Count > 0))
            return (house.Position, 0, house.InstanceId);
        var storagePosition = SharedStoragePosition;
        return IsWithinInteractionRange(state.Position, storagePosition, ResourceInteractionRange) ||
            Reach(actor, storagePosition, ReachPurpose.FoodCargoStorage, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? (storagePosition, ResourceInteractionRange, null) : null;
    }

    private void MakeRoomForFood(string actor, AgentState state) => ApplyFoodCargoPlan(PlanFoodCargo(actor));

    private bool StoreSpareCargo(string actor, AgentState state, string householdId,
        List<SpareCargoMove> cargo)
    {
        if (cargo.Count == 0 ||
            SpareCargoDestination(actor, state, cargo.Sum(move => move.PhysicalQuantity)) is not { } destination)
            return false;
        if (!IsWithinInteractionRange(state.Position, destination.Position, destination.Range))
        {
            MoveToward(actor, state, destination.Position, "make_room", destination.Range);
            return true;
        }
        foreach (var move in cargo)
        {
            var lot = move.Lot;
            var quantity = move.TransferQuantity;
            RunInventoryCommand(InventorySystem.StoreSpareCargo(InventoryEconomyWorld, move, lot, actor, state, quantity, householdId, destination));
            AppendEvent(new SpareCargoStoredEventPayload(actor, lot.ItemKind, quantity, destination.StorageBuildingId ?? "pile"));
        }
        return true;
    }

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
