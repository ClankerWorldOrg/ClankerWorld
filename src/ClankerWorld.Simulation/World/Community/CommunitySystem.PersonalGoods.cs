using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CommunitySystem
{
    internal static ActorCapabilityPolicy PersonalGoodsCapabilities(PersonalGoodsMode mode) =>
        new(ActionLifeStages.Adults, AllowBoat: mode != PersonalGoodsMode.Choice,
            AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    internal static PersonalGoodsActionPlan? PlanPersonalGoods(ICommunityGoodsWorld world, string actor, PersonalGoodsChoice choice, int maximum = int.MaxValue,
        PersonalGoodsMode mode = PersonalGoodsMode.Choice, string? requiredHouse = null,
        string? requiredOwner = null, GridPoint? requiredPosition = null)
    {
        if (!world.PhysicalAgents.Living.ContainsKey(actor) || maximum <= 0) return null;
        var index = InventoryIndex.For(world.Society.Inventory);
        var lot = index.Find(choice.LotId);
        if (lot is null) return null;
        PlacedBuilding? house = null;
        GridPoint target;
        int range;
        int quantity;
        if (choice.Kind == PersonalGoodsKind.Collect)
        {
            if (world.PersonalRecoveryRequest(actor, GoodsUse.Recover, lot.ItemKind) is not { } request ||
                world.RecheckGoods(request, lot.Id) is not { } match) return null;
            target = world.HouseholdStockPosition(lot);
            range = lot.GroundPosition is not null ? world.ResourceInteractionRange : 1;
            quantity = Math.Min(match.Quantity, maximum);
        }
        else
        {
            house = world.HouseForHousehold(choice.Kind == PersonalGoodsKind.Store ? world.HouseholdFor(actor) : lot.OwnerId);
            if (house is null || requiredHouse is not null && (house.InstanceId != requiredHouse ||
                house.HouseholdId != requiredOwner || house.Position != requiredPosition)) return null;
            var eligible = choice.Kind == PersonalGoodsKind.Store ? world.PersonalStorageLots(actor, house.InstanceId)
                : mode == PersonalGoodsMode.BoundOrder ? world.ReturnableBorrowedGoods(actor) : world.BorrowedGoods(actor);
            if (!eligible.Any(item => item.Id == lot.Id)) return null;
            target = house.Position;
            range = 1;
            var room = world.DestinationRoom(house.InstanceId);
            if (choice.Kind == PersonalGoodsKind.Store && room <= 0) return null;
            quantity = InventoryContainerRules.IsContainer(lot.ItemKind) ? world.VesselFits(lot, room) ? 1 : 0
                : Math.Min(world.PhysicalUnreservedQuantity(lot), room);
            quantity = Math.Min(quantity, maximum);
        }
        var physical = InventoryContainerRules.IsContainer(lot.ItemKind) ? index.FamilyQuantity(lot.Id) : quantity;
        return new(actor, choice, mode, lot.OwnerId, lot.ItemKind, quantity, physical, lot.CarrierId, lot.StorageBuildingId,
            lot.GroundPosition, house?.InstanceId, house?.HouseholdId, house?.DefinitionId, house?.PlacedTick, target, range,
            world.Reach(actor, target, ReachPurpose.PersonalGoods, range, 0, SwimmingPolicy.WhenCapable),
            InventoryContainerRules.IsContainer(lot.ItemKind) ? GoodsPlanFamily.Capture(world.Society.Inventory, lot) : null);
    }

    internal static ActionCheck CheckPersonalGoods(ICommunityGoodsWorld world, PersonalGoodsActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        if (!world.PhysicalAgents.Living.ContainsKey(plan.Actor)) return ActionCheck.Refuse(ActionRefusalReason.ActorUnavailable, "the person is no longer here");
        if (plan.Mode != PersonalGoodsMode.Native || plan.Choice.Kind != PersonalGoodsKind.Return)
        {
            var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
                policy ?? PersonalGoodsCapabilities(plan.Mode));
            if (!capability.Allowed) return capability;
            if (!world.ReadyForBriefInteraction(plan.Actor))
                return ActionCheck.Refuse(ActionRefusalReason.ActorUnavailable, "they cannot handle the selected belongings yet");
        }
        var lot = InventoryIndex.For(world.Society.Inventory).Find(plan.Choice.LotId);
        if (lot is null || lot.OwnerId != plan.OwnerId || lot.ItemKind != plan.ItemKind ||
            lot.CarrierId != plan.SourceCarrierId || lot.StorageBuildingId != plan.SourceStorageId || lot.GroundPosition != plan.SourceGround)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected belongings or their custody have changed");
        if (plan.Choice.Kind == PersonalGoodsKind.Collect)
        {
            if (world.PersonalRecoveryRequest(plan.Actor, GoodsUse.Recover, plan.ItemKind) is not { } request ||
                world.RecheckGoods(request, lot.Id) is not { } match || match.Quantity < plan.Quantity ||
                world.HouseholdStockPosition(lot) != plan.Target)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected personal collection is unavailable");
            if (world.FreeCarryCapacity(plan.Actor) < plan.PhysicalQuantity)
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected personal collection");
        }
        else
        {
            var house = world.HouseForHousehold(plan.Choice.Kind == PersonalGoodsKind.Store ? world.HouseholdFor(plan.Actor) : plan.OwnerId);
            if (house is null || house.InstanceId != plan.HouseId || house.HouseholdId != plan.HouseOwnerId ||
                house.DefinitionId != plan.HouseDefinitionId || house.PlacedTick != plan.HousePlacedTick || house.Position != plan.Target)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected House for these belongings has changed");
            var eligible = plan.Choice.Kind == PersonalGoodsKind.Store ? world.PersonalStorageLots(plan.Actor, house.InstanceId)
                : plan.Mode == PersonalGoodsMode.BoundOrder ? world.ReturnableBorrowedGoods(plan.Actor) : world.BorrowedGoods(plan.Actor);
            if (!eligible.Any(item => item.Id == lot.Id) || world.PhysicalUnreservedQuantity(lot) < plan.Quantity)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected stored or borrowed belongings are unavailable");
            if (plan.Quantity > 0 && (world.DestinationRoom(house.InstanceId) < plan.PhysicalQuantity || !world.VesselFits(lot, world.DestinationRoom(house.InstanceId))) ||
                plan.Mode == PersonalGoodsMode.Choice && (world.DestinationRoom(house.InstanceId) <= 0 || !world.VesselFits(lot, world.DestinationRoom(house.InstanceId))))
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected House has no room for these belongings");
        }
        if (!GoodsPlanFamily.Matches(world.Society.Inventory, lot, plan.Family))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected vessel's contents have changed");
        return plan.Mode != PersonalGoodsMode.Choice || world.IsWithinInteractionRange(world.PhysicalAgents.Living[plan.Actor].Position, plan.Target, plan.Range) ||
            world.Reach(plan.Actor, plan.Target, ReachPurpose.PersonalGoods, plan.Range, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected belongings or House are out of reach");
    }

    internal static (string MoveId, string ItemKind, int Quantity)? ApplyCheckedPersonalGoods(ICommunityGoodsWorld world, PersonalGoodsActionPlan plan)
    {
        if (plan.Mode == PersonalGoodsMode.BoundOrder)
            world.InterruptOrdinaryFieldWorkForCustody(plan.Actor, plan.Choice.LotId);
        if (!world.IsWithinInteractionRange(world.PhysicalAgents.Living[plan.Actor].Position, plan.Target, plan.Range))
        {
            world.MoveToward(plan.Actor, world.PhysicalAgents.Living[plan.Actor], plan.Target, "personal_goods", plan.Range);
            return null;
        }
        if (plan.Quantity <= 0) return null;
        var moveId = world.PersonalMoveId(plan.Actor, world.WorldTick, plan.Choice.LotId);
        world.MovePersonalGoods(plan, moveId);
        WorldEventPayload payload = plan.Choice.Kind switch
        {
            PersonalGoodsKind.Collect => new PersonalGoodsCollectedEventPayload(plan.Actor, plan.ItemKind, plan.Quantity, plan.OwnerId),
            PersonalGoodsKind.Store => new PersonalGoodsStoredEventPayload(plan.Actor, plan.ItemKind, plan.Quantity),
            _ => new BorrowedGoodsReturnedEventPayload(plan.Actor, plan.ItemKind, plan.Quantity, plan.OwnerId),
        };
        world.AppendEvent(payload);
        return (moveId, plan.ItemKind, plan.Quantity);
    }
}
