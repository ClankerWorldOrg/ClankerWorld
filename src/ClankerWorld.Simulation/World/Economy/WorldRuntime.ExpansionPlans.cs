using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ExpansionActionPlan? PlanExpansion(string actor, PlacedBuilding building, OwnerQueuedInstruction? instruction = null)
    {
        var shape = ExpansionShapeForOrder(building, instruction);
        if (shape.Footprint is null) return null;
        var definition = worldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        var costs = BuildingStorageRules.ExpansionCosts(definition, building, shape.Footprint);
        var step = PlanExpansionMaterialStep(actor, building, costs);
        var destination = step switch { ExpansionPickupStep pickup => pickup.Position, ExpansionGatherStep gather => gather.Source.Position, _ => building.Position };
        var range = step switch { ExpansionPickupStep pickup => pickup.InteractionRange, ExpansionGatherStep => ResourceInteractionRange, _ => 0 };
        return new(actor, new(building.InstanceId), building, shape.Position, shape.Footprint, ExpansionOwner(building),
            PlanExpansionInputs(actor, building, costs), Reach(actor, destination, ReachPurpose.BuildingExpansion, range, 0, SwimmingPolicy.WhenCapable), step);
    }

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

    private ExpansionMaterialInput[]? PlanExpansionInputs(string actor, PlacedBuilding building, IReadOnlyList<ContentQuantity> costs)
    {
        var inputs = new List<ExpansionMaterialInput>();
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cost in costs)
        {
            var remaining = cost.Amount;
            foreach (var lot in ExpansionMaterialLots(actor, building).Where(lot => lot.ItemKind == cost.ResourceId).OrderBy(lot => lot.Id, StringComparer.Ordinal))
            {
                var quantity = Math.Min(remaining, Math.Max(0, AvailableLotQuantity(lot) - used.GetValueOrDefault(lot.Id)));
                if (quantity == 0) continue;
                inputs.Add(new(lot.Id, lot.OwnerId, lot.ItemKind, quantity));
                used[lot.Id] = used.GetValueOrDefault(lot.Id) + quantity;
                remaining -= quantity;
                if (remaining == 0) break;
            }
            if (remaining > 0) return null;
        }
        return inputs.ToArray();
    }

    private static ActorCapabilityPolicy ExpansionCapabilities =>
        new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private ActionCheck CheckExpansion(ExpansionActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? ExpansionCapabilities);
        if (!capability.Allowed) return capability;
        if (worldSimulation.Buildings.FirstOrDefault(building => building.InstanceId == plan.Building.InstanceId) is not { } current ||
            current.DefinitionId != plan.Building.DefinitionId || current.Position != plan.Building.Position || current.Footprint != plan.Building.Footprint ||
            current.HouseholdId != plan.Building.HouseholdId || current.TownId != plan.Building.TownId || ExpansionOwner(current) != plan.OwnerId ||
            !MayExpandBuilding(plan.Actor, current, out _) || !CanFitExpansion(current, plan.TargetPosition, plan.TargetFootprint, out _))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected expansion building, need or footprint has changed");
        if (plan.Inputs is not null && plan.Inputs.GroupBy(input => (input.LotId, input.OwnerId, input.ItemKind)).Any(group =>
                ExpansionMaterialLots(plan.Actor, current).FirstOrDefault(lot => lot.Id == group.Key.LotId && lot.OwnerId == group.Key.OwnerId && lot.ItemKind == group.Key.ItemKind) is not { } lot ||
                AvailableLotQuantity(lot) < group.Sum(input => input.Quantity)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected expansion input lots are unavailable");
        return CheckExpansionMaterialStep(plan, current);
    }
}
