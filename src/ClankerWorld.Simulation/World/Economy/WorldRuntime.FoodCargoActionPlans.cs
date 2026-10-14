using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private FoodCargoActionPlan? PlanFoodCargo(string actor)
    {
        if (!agents.TryGetValue(actor, out var state) || HouseholdFor(actor) is not { } household ||
            !ActorCapabilities.Check(CapabilitiesFor(actor), "make_room_for_food", FoodCargoPolicy).Allowed)
            return null;
        var cargo = FoodRecoveryCargo(actor, state);
        if (cargo.Count == 0 || SpareCargoDestination(actor, state, cargo.Sum(move => move.PhysicalQuantity)) is not { } destination)
            return null;
        var inventory = society.Checkpoint.Inventory;
        var moves = cargo.Select(move =>
        {
            var reservationQuantity = move.OrchardReservationId is { } reservationId ? inventory.GetReservation(reservationId).Quantity : 0;
            return new FoodCargoMove(move.Lot.Id, move.Lot.OwnerId, move.Lot.ItemKind, move.TransferQuantity, move.PhysicalQuantity,
                InventoryContainerRules.IsContainer(move.Lot.ItemKind) ? GoodsPlanFamily.Capture(inventory, move.Lot) : null,
                move.OrchardReservationId, reservationQuantity, move.OrchardReservationId is null ? 0 :
                    Math.Max(0, move.TransferQuantity - (move.Lot.Quantity - reservationQuantity)));
        }).ToArray();
        return new(actor, household, Array.AsReadOnly(moves), destination.Position, destination.Range,
            destination.StorageBuildingId, Reach(actor, destination.Position, ReachPurpose.FoodCargoStorage,
                destination.Range, 0, SwimmingPolicy.WhenCapable));
    }

    private static ActorCapabilityPolicy FoodCargoPolicy => new(ActionLifeStages.NonInfants,
        AllowUrgentWarmth: true, AllowActiveOrder: true);

    private ActionCheck CheckFoodCargoPlan(FoodCargoActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, FoodCargoPolicy);
        if (!capability.Allowed) return capability;
        var state = agents[plan.Actor];
        if (HouseholdFor(plan.Actor) != plan.HouseholdId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household has changed");
        if (FoodRoomMissing(plan.Actor, state) <= 0)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is already room for food");
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
        if (CheckFoodCargoPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        var state = agents[plan.Actor];
        if (!IsWithinInteractionRange(state.Position, plan.Destination, plan.InteractionRange))
        {
            MoveToward(plan.Actor, state, plan.Destination, "make_room", plan.InteractionRange);
            return;
        }
        foreach (var move in plan.Cargo)
        {
            ApplyInventoryTransition(inventory =>
            {
                if (move.OrchardReservationId is { } reservationId && move.ReleaseQuantity > 0)
                    inventory = InventoryAuthority.ReleaseReservationQuantity(inventory, reservationId, move.ReleaseQuantity, "orchard_food_recovery");
                var access = InventoryHoldRules.DeliveryFor(plan.Actor, plan.StorageBuildingId);
                InventoryGroundPosition? ground = plan.StorageBuildingId is null ? new InventoryGroundPosition(plan.Destination.X, plan.Destination.Y) : null;
                return move.OwnerId == plan.HouseholdId
                    ? InventoryAuthority.Relocate(inventory, DerivedId.Create($"spare-cargo:{WorldTick}:{plan.Actor}:{move.StockId}"), move.StockId,
                        plan.HouseholdId, move.Quantity, storageBuildingId: plan.StorageBuildingId, groundPosition: ground, holdAccess: access)
                    : InventoryAuthority.Transfer(inventory, DerivedId.Create($"spare-cargo:{WorldTick}:{plan.Actor}:{move.StockId}"), plan.Actor,
                        plan.HouseholdId, move.StockId, move.Quantity, "spare_cargo_stored", plan.StorageBuildingId,
                        destinationGroundPosition: ground, holdAccess: access);
            });
            AppendEvent(new SpareCargoStoredEventPayload(plan.Actor, move.ItemKind, move.Quantity, plan.StorageBuildingId ?? "pile"));
        }
    }
}
