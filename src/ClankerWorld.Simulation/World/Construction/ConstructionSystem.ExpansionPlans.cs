using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

internal sealed partial class ConstructionSystem
{
    internal static string ExpansionOwner(PlacedBuilding building) => building.HouseholdId ?? building.TownId!;

    internal static ExpansionActionPlan? PlanExpansion(IExpansionPlanningWorld world, string actor, PlacedBuilding building, OwnerQueuedInstruction? instruction = null)
    {
        var shape = world.ExpansionShapeForOrder(building, instruction);
        if (shape.Footprint is null) return null;
        var definition = world.Content.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        var costs = BuildingStorageRules.ExpansionCosts(definition, building, shape.Footprint);
        var step = world.PlanExpansionMaterialStep(actor, building, costs);
        var destination = step switch { ExpansionPickupStep pickup => pickup.Position, ExpansionGatherStep gather => gather.Source.Position, _ => building.Position };
        var range = step switch { ExpansionPickupStep pickup => pickup.InteractionRange, ExpansionGatherStep => world.ResourceInteractionRange, _ => 0 };
        return new(actor, new(building.InstanceId), building, shape.Position, shape.Footprint, ExpansionOwner(building),
            PlanExpansionInputs(world, actor, building, costs), world.Reach(actor, destination, ReachPurpose.BuildingExpansion, range, 0, SwimmingPolicy.WhenCapable), step);
    }

    internal static ExpansionMaterialInput[]? PlanExpansionInputs(IExpansionPlanningWorld world, string actor, PlacedBuilding building, IReadOnlyList<ContentQuantity> costs)
    {
        var inputs = new List<ExpansionMaterialInput>();
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cost in costs)
        {
            var remaining = cost.Amount;
            foreach (var lot in world.ExpansionMaterialLots(actor, building).Where(lot => lot.ItemKind == cost.ResourceId).OrderBy(lot => lot.Id, StringComparer.Ordinal))
            {
                var quantity = Math.Min(remaining, Math.Max(0, world.AvailableLotQuantity(lot) - used.GetValueOrDefault(lot.Id)));
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

    internal static ActorCapabilityPolicy ExpansionCapabilities =>
        new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    internal ActionCheck CheckExpansion(IExpansionPlanningWorld world, ExpansionActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? ExpansionCapabilities);
        if (!capability.Allowed) return capability;
        if (State.Buildings.FirstOrDefault(building => building.InstanceId == plan.Building.InstanceId) is not { } current ||
            current.DefinitionId != plan.Building.DefinitionId || current.Position != plan.Building.Position || current.Footprint != plan.Building.Footprint ||
            current.HouseholdId != plan.Building.HouseholdId || current.TownId != plan.Building.TownId || ExpansionOwner(current) != plan.OwnerId ||
            !MayExpandBuilding(world, plan.Actor, current, out _) || !world.CanFitExpansion(current, plan.TargetPosition, plan.TargetFootprint, out _))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected expansion building, need or footprint has changed");
        if (plan.Inputs is not null && plan.Inputs.GroupBy(input => (input.LotId, input.OwnerId, input.ItemKind)).Any(group =>
                world.ExpansionMaterialLots(plan.Actor, current).FirstOrDefault(lot => lot.Id == group.Key.LotId && lot.OwnerId == group.Key.OwnerId && lot.ItemKind == group.Key.ItemKind) is not { } lot ||
                world.AvailableLotQuantity(lot) < group.Sum(input => input.Quantity)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected expansion input lots are unavailable");
        return world.CheckExpansionMaterialStep(plan, current);
    }
    internal bool MayExpandBuilding(IExpansionPlanningWorld world, string actor, PlacedBuilding building, out string failure)
    {
        failure = "Only an adult household member or a current Town resident can expand this building.";
        if (!world.IsAdult(actor)) return false;
        var definition = world.Content.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        var nearlyFull = world.StoredQuantity(building.InstanceId) * 100 >=
            BuildingStorageRules.Capacity(definition, building) * BuildingStorageRules.NearlyFullPercent;
        if (definition.Tags.Contains("house", StringComparer.Ordinal))
        {
            if (building.HouseholdId != world.Society.GetAgent(actor).HouseholdId) return false;
            var residentNeed = building.HouseholdId is { } householdId &&
                world.HouseResidentCapacity(householdId) is { } capacity && capacity.ResidentCount >= capacity.Limit;
            if (!nearlyFull && !residentNeed)
            {
                failure = "The House needs more resident places or must be nearly full before storage can be expanded.";
                return false;
            }
        }
        else if (definition.Tags.Contains("warehouse", StringComparer.Ordinal))
        {
            if (building.TownId is null || world.TownForResident(actor) != building.TownId) return false;
            if (!nearlyFull)
            {
                failure = "The building needs to be nearly full before its storage can be expanded.";
                return false;
            }
        }
        else if (definition.Tags.Contains(AnimalContent.YardTag, StringComparer.Ordinal))
        {
            if (building.HouseholdId != world.HouseholdFor(actor) || world.AnimalYardCapacity(building) >= AnimalRules.PopulationCap) return false;
        }
        else return false;
        if ((State.Expansions ?? []).Any(job => job.BuildingInstanceId == building.InstanceId &&
                (job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused)))
        {
            failure = "An expansion is already in progress for this building.";
            return false;
        }
        failure = string.Empty;
        return true;
    }

}
