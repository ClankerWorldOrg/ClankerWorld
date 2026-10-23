namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ActionCheck CheckTownProjectWork(TownProjectWorkActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? TownProjectPhysicalCapabilities);
        if (!capability.Allowed) return capability;
        if (!ReadyForTownProject(plan.Actor) || TownForResident(plan.Actor) != plan.TownId ||
            towns.SingleOrDefault(town => town.Id == plan.TownId) is not { Council: not null } town ||
            town.Projects.SingleOrDefault(project => project.Id == plan.Choice.ProjectId) is not { Stage: "supplying" or "working" } project ||
            project.ProposalId != plan.ProposalId || TownHearingProcedure.Digest(project.Plan) != TownHearingProcedure.Digest(plan.Project) ||
            !TownSystem.CivicHistory(town).Knows(plan.Actor, project.ProposalId) || TownProjectRules.WorkSite(project.Plan) != plan.Destination ||
            TownProjectSiteFailure(town, project.Plan, project.Id, plan.Actor) is not null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected project scope, knowledge or work ability has changed");
        if (plan.Choice.ItemKind is null)
        {
            if (!TownProjectHasAllMaterials(town, project) || !project.Deliveries.Where(delivery => delivery.DeliveredTick is not null && delivery.ReleasedTick is null)
                    .SequenceEqual(plan.DeliveredMaterials))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected delivered project budget has changed");
            if (plan.Hammer is { } hammer && (CarriedTownProjectTool(plan.Actor, hammer.ToolLotId) is not { } selectedHammer || selectedHammer.ConditionBasisPoints <= 0 ||
                    selectedHammer.FreshnessBasisPoints <= 0 || AvailableLotQuantity(selectedHammer) <= 0 ||
                    ToolProgressionRules.Find(selectedHammer.ItemKind) is not { Family: ToolFamily.Hammer } definition ||
                    definition.WorkUnits != hammer.WorkUnits || definition.WearLossBasisPoints != hammer.WearLossBasisPoints))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected work hammer is no longer usable");
            return IsWithinInteractionRange(agents[plan.Actor].Position, plan.Destination, 0) ||
                Reach(plan.Actor, plan.Destination, ReachPurpose.TownProjectWork, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
                ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected project site is out of reach");
        }
        if (TownProjectHasAllMaterials(town, project) || TownProjectMissing(town, project, plan.Choice.ItemKind) <= 0 ||
            PersonalTownProjectDonationLots(plan.Actor, plan.Choice.ItemKind).Any())
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected project no longer needs this personal gathering trip");
        if (plan.Source is not { } source || !map.Resources.Contains(source) || resources.GetValueOrDefault(source.Id) != ResourceState.Available ||
            !EcologyRules.Harvest(worldSystems.Ecology.GetResource(source.Id), 1).IsValid)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected material source is no longer available");
        if (plan.Harvest is { } harvest)
        {
            if (harvest.ToolLotId is { } toolId && (RecheckGoods(new(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(plan.ToolOwnerId!),
                    GoodsKinds.One(plan.ToolKind!)), toolId) is not { Lot: var tool } || !ToolProgressionRules.IsTopLevelCarriedLot(tool, plan.Actor)))
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected gathering tool is no longer usable");
            if (FreeCarryCapacity(plan.Actor) < checked(harvest.Quantity + harvest.TreeSeedQuantity))
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected personal harvest");
        }
        else if (plan.ToolPickup is { } pickup)
        {
            if (FreeCarryCapacity(plan.Actor) <= 0 || HasCarriedEquipmentAtLeast(plan.Actor, pickup.ItemKind))
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected gathering tool is no longer needed or cannot be carried");
            var check = CheckEquipmentPickup(plan.Actor, pickup);
            if (!check.Allowed) return check;
        }
        else return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected gathering preparation is unavailable");
        return IsWithinInteractionRange(agents[plan.Actor].Position, source.Position, ResourceInteractionRange) ||
            Reach(plan.Actor, source.Position, ReachPurpose.MaterialGathering, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected material source is out of reach");
    }

    private void ApplyCheckedTownProjectWork(TownProjectWorkActionPlan plan)
    {
        if (plan.Choice.ItemKind is { } kind)
        {
            if (plan.ToolPickup is { } pickup) ApplyGatheringToolPickup(plan.Actor, pickup);
            else GatherPlannedProjectMaterial(plan.Actor, agents[plan.Actor], kind, plan.Source!, plan.Harvest!);
            return;
        }
        var town = towns.Single(item => item.Id == plan.TownId);
        var project = town.Projects.Single(item => item.Id == plan.Choice.ProjectId);
        WorkOnTownProject(plan.Actor, agents[plan.Actor], new(plan.CandidateId, "work", town, project), plan.Hammer);
    }
}
