using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ExpansionActionPlan? PlanExpansion(string actor, PlacedBuilding building, OwnerQueuedInstruction? instruction = null) =>
        ConstructionSystem.PlanExpansion(ExpansionPlanningWorld, actor, building, instruction);

    private ExpansionMaterialStep? PlanExpansionMaterialStep(string actor, PlacedBuilding building, IReadOnlyList<ContentQuantity> costs)
    {
        var missing = costs.FirstOrDefault(cost => !HasExpansionMaterials(actor, building, [cost]) &&
            ExpansionDelivery(actor, building, cost.ResourceId) is not null);
        if (missing.Amount == 0) missing = costs.FirstOrDefault(cost => !HasExpansionMaterials(actor, building, [cost]));
        if (missing.Amount == 0) return null;
        if (ExpansionDelivery(actor, building, missing.ResourceId) is { } delivery)
        {
            var quantity = Math.Min(AvailableLotQuantity(delivery), DestinationRoom(building.InstanceId, delivery));
            return new ExpansionDeliveryStep(missing.ResourceId, delivery.Id,
                quantity > 0 ? quantity : AvailableLotQuantity(delivery), quantity > 0);
        }
        var pickup = ExpansionSharedMaterialPickup(actor, building, missing);
        if (pickup.Source is { } shared)
            return new ExpansionPickupStep(missing.ResourceId, shared.Id, shared.OwnerId, pickup.Quantity,
                HouseholdStockPosition(shared), HouseholdStockInteractionRange(shared));
        if (MaterialSource(missing.ResourceId, actor, building.Position) is not { } source ||
            ProjectMaterialHarvest(actor, missing.ResourceId, source) is not { } harvest) return null;
        var tool = harvest.ToolLotId is { } id ? society.Checkpoint.Inventory.GetLot(id) : null;
        return new ExpansionGatherStep(missing.ResourceId, source, harvest, tool?.OwnerId, tool?.ItemKind,
            HarvestReturnRoute(actor, source, building.Position, ReachPurpose.BuildingExpansion,
                checked(harvest.Quantity + harvest.TreeSeedQuantity), 0));
    }

    private ActionCheck CheckExpansionMaterialStep(ExpansionActionPlan plan, PlacedBuilding building)
    {
        var definition = worldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        var costs = BuildingStorageRules.ExpansionCosts(definition, building, plan.TargetFootprint);
        if (plan.MaterialStep is { } material)
        {
            var cost = costs.FirstOrDefault(cost => cost.ResourceId == material.ItemKind);
            if (cost.Amount == 0 || HasExpansionMaterials(plan.Actor, building, [cost]))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected expansion material need is already satisfied");
            var remaining = cost.Amount - ExpansionMaterialLots(plan.Actor, building).Where(lot => lot.ItemKind == material.ItemKind).Sum(AvailableLotQuantity) -
                ExpansionInboundQuantity(building, material.ItemKind);
            if (material is ExpansionPickupStep pickup && remaining < pickup.Quantity || material is ExpansionGatherStep && remaining <= 0)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected expansion material need is already covered by inbound stock");
        }
        GridPoint destination;
        int range;
        switch (plan.MaterialStep)
        {
            case ExpansionDeliveryStep step:
                if (ExpansionDelivery(plan.Actor, building, step.ItemKind, step.LotId) is not { } delivery ||
                    AvailableLotQuantity(delivery) < step.Quantity)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected expansion delivery lot is unavailable");
                if (step.Store && DestinationRoom(building.InstanceId, delivery) < step.Quantity)
                    return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected expansion storage has no room for the delivery");
                destination = building.Position;
                range = 0;
                break;
            case ExpansionPickupStep step:
                if (RecheckGoods(DeliveryGoodsRequest(plan.Actor, step.OwnerId, step.ItemKind, building.InstanceId,
                        GoodsDeliveryPurpose.Expansion), step.LotId) is not { } match || match.Quantity < step.Quantity ||
                    HouseholdStockPosition(match.Lot) != step.Position || HouseholdStockInteractionRange(match.Lot) != step.InteractionRange ||
                    match.Lot.StorageBuildingId == building.InstanceId || match.Lot.CarrierId is not null ||
                    match.Lot.GroundPosition == ExpansionGroundPosition(building) || match.Lot.ContainerLotId is not null)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected expansion pickup lot or location has changed");
                if (PickupCarryCapacity(plan.Actor, match.Lot, building.Position) < step.Quantity)
                    return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room to carry the selected expansion pickup");
                destination = step.Position;
                range = step.InteractionRange;
                break;
            case ExpansionGatherStep step:
                var harvest = CheckPlannedProjectHarvest(plan.Actor, step.Source, step.Harvest, step.ToolOwnerId, step.ToolKind);
                if (!harvest.Allowed) return harvest;
                var quantity = checked(step.Harvest.Quantity + step.Harvest.TreeSeedQuantity);
                if (FreeCarryCapacity(plan.Actor) < quantity)
                    return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected expansion harvest");
                if (HarvestReturnRoute(plan.Actor, step.Source, building.Position, ReachPurpose.BuildingExpansion, quantity, 0).Count == 0)
                    return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected expansion harvest cannot return to the building");
                destination = step.Source.Position;
                range = ResourceInteractionRange;
                break;
            default:
                destination = building.Position;
                range = 0;
                break;
        }
        return IsWithinInteractionRange(agents[plan.Actor].Position, destination, range) ||
            Reach(plan.Actor, destination, ReachPurpose.BuildingExpansion, range, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected expansion material step is out of reach");
    }

    private ExpansionMaterialInput[]? PlanExpansionInputs(string actor, PlacedBuilding building, IReadOnlyList<ContentQuantity> costs) =>
        ConstructionSystem.PlanExpansionInputs(ExpansionPlanningWorld, actor, building, costs);

    private static ActorCapabilityPolicy ExpansionCapabilities => ConstructionSystem.ExpansionCapabilities;

    private ActionCheck CheckExpansion(ExpansionActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        constructionSystem.CheckExpansion(ExpansionPlanningWorld, plan, policy);
}
