using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private PersonalGoodsActionPlan? PlanPersonalGoods(string actor, PersonalGoodsChoice choice, int maximum = int.MaxValue,
        PersonalGoodsMode mode = PersonalGoodsMode.Choice, string? requiredHouse = null,
        string? requiredOwner = null, GridPoint? requiredPosition = null)
    {
        if (!agents.ContainsKey(actor) || maximum <= 0) return null;
        var index = InventoryIndex.For(society.Checkpoint.Inventory);
        var lot = index.Find(choice.LotId);
        if (lot is null) return null;
        PlacedBuilding? house = null;
        GridPoint target;
        int range;
        int quantity;
        if (choice.Kind == PersonalGoodsKind.Collect)
        {
            if (PersonalRecoveryRequest(actor, GoodsUse.Recover, lot.ItemKind) is not { } request ||
                RecheckGoods(request, lot.Id) is not { } match) return null;
            target = HouseholdStockPosition(lot);
            range = lot.GroundPosition is not null ? ResourceInteractionRange : 1;
            quantity = Math.Min(match.Quantity, maximum);
        }
        else
        {
            house = HouseForHousehold(choice.Kind == PersonalGoodsKind.Store ? HouseholdFor(actor) : lot.OwnerId);
            if (house is null || requiredHouse is not null && (house.InstanceId != requiredHouse ||
                house.HouseholdId != requiredOwner || house.Position != requiredPosition)) return null;
            var eligible = choice.Kind == PersonalGoodsKind.Store ? PersonalStorageLots(actor, house.InstanceId)
                : mode == PersonalGoodsMode.BoundOrder ? ReturnableBorrowedGoods(actor) : BorrowedGoods(actor);
            if (!eligible.Any(item => item.Id == lot.Id)) return null;
            target = house.Position;
            range = 1;
            var room = DestinationRoom(house.InstanceId);
            if (choice.Kind == PersonalGoodsKind.Store && room <= 0) return null;
            quantity = InventoryContainerRules.IsContainer(lot.ItemKind) ? VesselFits(lot, room) ? 1 : 0
                : Math.Min(PhysicalUnreservedQuantity(lot), room);
            quantity = Math.Min(quantity, maximum);
        }
        var physical = InventoryContainerRules.IsContainer(lot.ItemKind) ? index.FamilyQuantity(lot.Id) : quantity;
        return new(actor, choice, mode, lot.OwnerId, lot.ItemKind, quantity, physical, lot.CarrierId, lot.StorageBuildingId,
            lot.GroundPosition, house?.InstanceId, house?.HouseholdId, house?.DefinitionId, house?.PlacedTick, target, range,
            Reach(actor, target, ReachPurpose.PersonalGoods, range, 0, SwimmingPolicy.WhenCapable),
            InventoryContainerRules.IsContainer(lot.ItemKind) ? GoodsPlanFamily.Capture(society.Checkpoint.Inventory, lot) : null);
    }

    private ActionCheck CheckPersonalGoods(PersonalGoodsActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        if (!agents.ContainsKey(plan.Actor)) return ActionCheck.Refuse(ActionRefusalReason.ActorUnavailable, "the person is no longer here");
        if (plan.Mode != PersonalGoodsMode.Native || plan.Choice.Kind != PersonalGoodsKind.Return)
        {
            var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
                policy ?? PersonalGoodsCapabilities(plan.Mode));
            if (!capability.Allowed) return capability;
            if (!ReadyForBriefInteraction(plan.Actor))
                return ActionCheck.Refuse(ActionRefusalReason.ActorUnavailable, "they cannot handle the selected belongings yet");
        }
        var lot = InventoryIndex.For(society.Checkpoint.Inventory).Find(plan.Choice.LotId);
        if (lot is null || lot.OwnerId != plan.OwnerId || lot.ItemKind != plan.ItemKind ||
            lot.CarrierId != plan.SourceCarrierId || lot.StorageBuildingId != plan.SourceStorageId || lot.GroundPosition != plan.SourceGround)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected belongings or their custody have changed");
        if (plan.Choice.Kind == PersonalGoodsKind.Collect)
        {
            if (PersonalRecoveryRequest(plan.Actor, GoodsUse.Recover, plan.ItemKind) is not { } request ||
                RecheckGoods(request, lot.Id) is not { } match || match.Quantity < plan.Quantity ||
                HouseholdStockPosition(lot) != plan.Target)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected personal collection is unavailable");
            if (FreeCarryCapacity(plan.Actor) < plan.PhysicalQuantity)
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected personal collection");
        }
        else
        {
            var house = HouseForHousehold(plan.Choice.Kind == PersonalGoodsKind.Store ? HouseholdFor(plan.Actor) : plan.OwnerId);
            if (house is null || house.InstanceId != plan.HouseId || house.HouseholdId != plan.HouseOwnerId ||
                house.DefinitionId != plan.HouseDefinitionId || house.PlacedTick != plan.HousePlacedTick || house.Position != plan.Target)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected House for these belongings has changed");
            var eligible = plan.Choice.Kind == PersonalGoodsKind.Store ? PersonalStorageLots(plan.Actor, house.InstanceId)
                : plan.Mode == PersonalGoodsMode.BoundOrder ? ReturnableBorrowedGoods(plan.Actor) : BorrowedGoods(plan.Actor);
            if (!eligible.Any(item => item.Id == lot.Id) || PhysicalUnreservedQuantity(lot) < plan.Quantity)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected stored or borrowed belongings are unavailable");
            if (plan.Quantity > 0 && (DestinationRoom(house.InstanceId) < plan.PhysicalQuantity || !VesselFits(lot, DestinationRoom(house.InstanceId))) ||
                plan.Mode == PersonalGoodsMode.Choice && (DestinationRoom(house.InstanceId) <= 0 || !VesselFits(lot, DestinationRoom(house.InstanceId))))
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected House has no room for these belongings");
        }
        if (!GoodsPlanFamily.Matches(society.Checkpoint.Inventory, lot, plan.Family))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected vessel's contents have changed");
        return plan.Mode != PersonalGoodsMode.Choice || IsWithinInteractionRange(agents[plan.Actor].Position, plan.Target, plan.Range) ||
            Reach(plan.Actor, plan.Target, ReachPurpose.PersonalGoods, plan.Range, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected belongings or House are out of reach");
    }

    private (string MoveId, string ItemKind, int Quantity)? ApplyPersonalGoods(PersonalGoodsActionPlan? plan)
    {
        if (plan is null) return null;
        if (CheckActionPlan(plan).Refusal is { } refusal)
        {
            if (plan.Mode == PersonalGoodsMode.Choice) RefuseChoice(plan, refusal);
            return null;
        }
        return ApplyCheckedPersonalGoods(plan);
    }

    private (string MoveId, string ItemKind, int Quantity)? ApplyCheckedPersonalGoods(PersonalGoodsActionPlan plan)
    {
        if (plan.Mode == PersonalGoodsMode.BoundOrder)
            InterruptOrdinaryFieldWorkForCustody(plan.Actor, plan.Choice.LotId);
        if (!IsWithinInteractionRange(agents[plan.Actor].Position, plan.Target, plan.Range))
        {
            MoveToward(plan.Actor, agents[plan.Actor], plan.Target, "personal_goods", plan.Range);
            return null;
        }
        if (plan.Quantity <= 0) return null;
        var moveId = PersonalMoveId(plan.Actor, WorldTick, plan.Choice.LotId);
        ApplyInventoryTransition(inventory => plan.Choice.Kind == PersonalGoodsKind.Collect
            ? InventoryAuthority.Relocate(inventory, moveId, plan.Choice.LotId, plan.OwnerId, plan.Quantity, plan.Actor,
                holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor))
            : InventoryAuthority.Relocate(inventory, moveId, plan.Choice.LotId, plan.OwnerId, plan.Quantity,
                storageBuildingId: plan.HouseId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.HouseId)));
        WorldEventPayload payload = plan.Choice.Kind switch
        {
            PersonalGoodsKind.Collect => new PersonalGoodsCollectedEventPayload(plan.Actor, plan.ItemKind, plan.Quantity, plan.OwnerId),
            PersonalGoodsKind.Store => new PersonalGoodsStoredEventPayload(plan.Actor, plan.ItemKind, plan.Quantity),
            _ => new BorrowedGoodsReturnedEventPayload(plan.Actor, plan.ItemKind, plan.Quantity, plan.OwnerId),
        };
        AppendEvent(payload);
        return (moveId, plan.ItemKind, plan.Quantity);
    }
}
