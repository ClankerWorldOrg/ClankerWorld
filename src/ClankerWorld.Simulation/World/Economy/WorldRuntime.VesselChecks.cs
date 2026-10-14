using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy VesselPolicy(VesselActionKind kind) =>
        new(ActionLifeStages.Adults, AllowUrgentFood: kind == VesselActionKind.TakeFood,
            AllowUrgentWarmth: true, AllowActiveOrder: true);

    private ActionCheck CheckVesselPlan(VesselActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var kind = plan.Choice.Kind;
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? VesselPolicy(kind));
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || HouseForHousehold(plan.HouseholdId) is not { } house ||
            house.InstanceId != plan.HouseId || house.Position != plan.HousePosition)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household or House has changed");
        if (plan.Quantity <= 0) return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "there is no selected quantity to move");
        var use = kind == VesselActionKind.ReturnJug ? GoodsUse.ReturnVessel : GoodsUse.VesselFor;
        var extra = kind is VesselActionKind.FillJug or VesselActionKind.StoreFood ? plan.Quantity : kind == VesselActionKind.CollectJug ? 1 : 0;
        var contents = kind is VesselActionKind.FillJug or VesselActionKind.CollectJug ? InventoryContainerRules.FreshWater : null;
        var request = new GoodsRequest(use, plan.Actor, GoodsOwners.One(plan.VesselOwner), GoodsKinds.One(plan.VesselKind),
            ExtraUnits: extra, ContentsKind: contents);
        if (RecheckGoods(request, plan.VesselId) is not { Lot: var vessel } ||
            !GoodsPlanFamily.Matches(society.Checkpoint.Inventory, vessel, plan.Family))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected vessel or its contents are no longer available");
        var inventory = society.Checkpoint.Inventory;
        var index = InventoryIndex.For(inventory);
        var access = kind == VesselActionKind.ReturnJug ? InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId) : InventoryHoldRules.BorrowedBy(plan.Actor);
        if (index.ContentsOf(vessel.Id).Prepend(vessel).Any(lot => !InventoryHoldRules.Allows(index, lot, access)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected vessel or its contents are promised to another task");
        var physical = ContainerFamilyQuantity(inventory, vessel.Id);
        if (kind is VesselActionKind.FillJug or VesselActionKind.ReturnJug)
        {
            if (!PersonalEquipmentRules.IsCarried(vessel, plan.Actor) || vessel.OwnerId != plan.Actor)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "they no longer carry the selected jug");
            if (kind == VesselActionKind.ReturnJug && DestinationRoom(plan.HouseId) < physical)
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the House has no room for the selected jug and contents");
            if (kind == VesselActionKind.FillJug && (!FreshWaterShorePositions().Contains(plan.Target) ||
                WaterJugRefillReturnDestination(plan.Actor, vessel) != plan.ReturnTo || WaterFillQuantity(plan.Actor, vessel, plan.Target, plan.ReturnTo) < plan.Quantity))
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected water load no longer fits its planned trip");
        }
        else if (kind is VesselActionKind.CollectJug or VesselActionKind.ReturnEmpty)
        {
            var source = worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == plan.SourceBuildingId);
            if (vessel.OwnerId != plan.HouseholdId || vessel.CarrierId is not null || vessel.GroundPosition is not null ||
                vessel.StorageBuildingId != plan.SourceBuildingId || source is null || source.HouseholdId != plan.HouseholdId || source.Position != plan.Target)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected vessel is no longer at the household pickup place");
            if (kind == VesselActionKind.CollectJug)
            {
                if (WaterJugStockLocation(vessel, plan.HouseholdId)?.InstanceId != source.InstanceId ||
                    !CanCollectJugForRefill(plan.Actor, vessel))
                    return ActionCheck.Refuse(ActionRefusalReason.Capacity, "they cannot collect the selected jug and leave room for water");
                if (plan.Shore is not { } shore || !FreshWaterShorePositions().Contains(shore) ||
                    ReachLeg(plan.Actor, source.Position, shore, ReachPurpose.HouseholdVessel, 0, physical, SwimmingPolicy.WhenCapable).Count == 0 ||
                    PickupCarryCapacity(plan.Actor, shore, plan.HousePosition) <= physical)
                    return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected jug's water trip is out of reach now");
            }
            else if (!IsEmptyWorkplaceVessel(vessel, plan.HouseId) || !CanStartEmptyVesselReturn(plan.Actor, plan.HouseId) ||
                PickupCarryCapacity(plan.Actor, vessel, plan.HousePosition) < physical)
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "they cannot return the selected empty household vessel");
        }
        else
        {
            if (vessel.OwnerId != plan.HouseholdId || vessel.StorageBuildingId != plan.HouseId || plan.Food is not { } food ||
                RecheckGoods(new(GoodsUse.ConsumeAt, plan.Actor, GoodsOwners.One(food.OwnerId), GoodsKinds.One(food.ItemKind)), food.StockId) is not { Lot: var lot, Quantity: var available } ||
                !InventoryHoldRules.Allows(index, lot, access))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected pot or food is no longer available");
            if (kind == VesselActionKind.StoreFood)
            {
                if (lot.ContainerLotId is not null || available - (lot.OwnerId == plan.Actor ? 1 : 0) < plan.Quantity ||
                    (lot.OwnerId == plan.Actor ? !PersonalEquipmentRules.IsCarried(lot, plan.Actor) || lot.DeliveryBuildingId is not null || DestinationRoom(plan.HouseId) < plan.Quantity :
                        lot.OwnerId != plan.HouseholdId || lot.StorageBuildingId != plan.HouseId))
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected food load no longer fits the pot");
            }
            else if (lot.OwnerId != plan.HouseholdId || lot.ContainerLotId != vessel.Id || available < plan.Quantity ||
                !WantsFoodFromPot(plan.Actor))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected serving is no longer available for their food needs");
        }
        return Reach(plan.Actor, plan.Target, ReachPurpose.HouseholdVessel, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected vessel action is out of reach now");
    }
}
