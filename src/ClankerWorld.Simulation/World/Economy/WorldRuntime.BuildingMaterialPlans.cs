namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private BuildingMaterialActionPlan? PlanBuildingMaterial(string actor, string household)
    {
        if (NeededBuildingMaterial(actor, household) is not { } need) return null;
        var house = HouseForHousehold(household);
        var destination = house?.Position ?? SharedStoragePosition;
        var range = house is null ? ResourceInteractionRange : 0;
        var source = need.Source;
        var bonus = UseHarvestBonusForBuildingMaterial(actor, need.Material.ResourceId, source, destination, range);
        var harvest = ProjectMaterialHarvest(actor, need.Material.ResourceId, source, bonus);
        if (harvest is null) return null;
        var tool = harvest.ToolLotId is { } id ? society.Checkpoint.Inventory.GetLot(id) : null;
        return new(actor, new(need.Material.ResourceId), household, need.Building, need.Material, house?.InstanceId,
            destination, range, source, harvest, tool?.OwnerId, tool?.ItemKind,
            Reach(actor, source.Position, ReachPurpose.BuildingPreparation, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable),
            HarvestReturnRoute(actor, source, destination, ReachPurpose.BuildingPreparation,
                checked(harvest.Quantity + harvest.TreeSeedQuantity), range));
    }

    private ActionCheck CheckBuildingMaterial(BuildingMaterialActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? ProjectMaterialCapabilities);
        if (!capability.Allowed) return capability;
        var house = HouseForHousehold(plan.HouseholdId);
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || house?.InstanceId != plan.HouseId ||
            (house?.Position ?? SharedStoragePosition) != plan.Destination ||
            HouseholdBuildingProjectInProgress(plan.HouseholdId, retryingActor: plan.Actor) ||
            !PlannableHouseholdBuildings(plan.HouseholdId, plan.Actor).Any(definition => definition.CanonicalId == plan.Building.CanonicalId &&
                definition.PayloadDigest == plan.Building.PayloadDigest) ||
            !WorkBuildingHasInputs(plan.Actor, plan.HouseholdId, plan.Building) ||
            HouseholdMaterialInHand(plan.HouseholdId, plan.Material.ResourceId) >= plan.Material.Amount)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected household building need or storage destination has changed");
        var harvest = CheckPlannedProjectHarvest(plan.Actor, plan.Source, plan.Harvest, plan.ToolOwnerId, plan.ToolKind);
        if (!harvest.Allowed) return harvest;
        if (FreeCarryCapacity(plan.Actor) < plan.PhysicalQuantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected building material harvest");
        if (HarvestReturnRoute(plan.Actor, plan.Source, plan.Destination, ReachPurpose.BuildingPreparation,
                plan.PhysicalQuantity, plan.DestinationRange).Count == 0)
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected building materials cannot return to household storage");
        return IsWithinInteractionRange(agents[plan.Actor].Position, plan.Source.Position, ResourceInteractionRange) ||
            Reach(plan.Actor, plan.Source.Position, ReachPurpose.BuildingPreparation, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected building material source is out of reach");
    }

    private void ApplyCheckedBuildingMaterial(BuildingMaterialActionPlan plan)
    {
        GatherPlannedProjectMaterial(plan.Actor, agents[plan.Actor], plan.Choice.ItemKind, plan.Source, plan.Harvest, plan.HouseId);
    }
}
