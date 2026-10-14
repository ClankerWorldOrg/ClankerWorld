using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static GoodsRequest ProjectContributionRequest(string actor, string kind) =>
        new(GoodsUse.ProjectMaterialContribution, actor, GoodsOwners.One(actor), GoodsKinds.One(kind));

    private ProjectAssistanceActionPlan? PlanProjectAssistance(string actor,
        (string Requester, ContentQuantity Input, string OwnerId) request)
    {
        if (!CanReceiveProjectAssistance(request)) return null;
        var project = agents[request.Requester].Project!;
        var house = HouseForHousehold(request.OwnerId);
        var destination = house?.Position ?? agents[request.Requester].Position;
        var range = house is not null && HouseholdFor(actor) == request.OwnerId ? 0 : ResourceInteractionRange;
        var carried = FindGoods(ProjectContributionRequest(actor, request.Input.ResourceId)).First;
        var quantity = carried is null ? 0 : Math.Min(request.Input.Amount,
            Math.Min(carried.Quantity, house is null ? FreeCarryCapacity(request.Requester) : DestinationRoom(house.InstanceId)));
        var source = carried is null ? MaterialSource(request.Input.ResourceId, actor, destination, range) : null;
        var harvest = source is null ? null : ProjectMaterialHarvest(actor, request.Input.ResourceId, source);
        if (carried is null && harvest is null || carried is not null && quantity == 0) return null;
        var tool = harvest?.ToolLotId is { } toolId ? society.Checkpoint.Inventory.GetLot(toolId) : null;
        return new(actor, new(request.Input.ResourceId), request.Requester, project.CandidateId, project.StartedTick,
            request.Input, request.OwnerId, house is null ? request.Requester : request.OwnerId, house?.InstanceId,
            destination, range, carried?.Lot.Id, quantity, source, harvest, tool?.OwnerId, tool?.ItemKind,
            Reach(actor, source?.Position ?? destination, ReachPurpose.ProjectAssistance,
                source is null ? range : ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable),
            source is null ? [] : HarvestReturnRoute(actor, source, destination, ReachPurpose.ProjectAssistance,
                checked(harvest!.Quantity + harvest.TreeSeedQuantity), range));
    }

    private ActionCheck CheckProjectAssistance(ProjectAssistanceActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        var house = HouseForHousehold(plan.OwnerId);
        if (!agents.TryGetValue(plan.RequesterId, out var requester) || requester.Project is not { } project ||
            project.CandidateId != plan.ProjectCandidateId || project.StartedTick != plan.ProjectStartedTick ||
            !ProjectRequests(plan.Actor).Any(request => request.Requester == plan.RequesterId && request.OwnerId == plan.OwnerId && request.Input == plan.RequestedInput) ||
            house?.InstanceId != plan.HouseId || (house?.Position ?? requester.Position) != plan.Destination ||
            (house is null ? plan.RequesterId : plan.OwnerId) != plan.RecipientId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected requesting project or recipient has changed");
        if (plan.LotId is { } lotId)
        {
            if (RecheckGoods(ProjectContributionRequest(plan.Actor, plan.Choice.ItemKind), lotId) is not { } match || match.Quantity < plan.Quantity)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected project contribution lot is unavailable");
            if ((house is null ? FreeCarryCapacity(plan.RequesterId) : DestinationRoom(house.InstanceId)) < plan.Quantity)
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected project recipient has no room for this contribution");
        }
        else
        {
            if (plan.Source is null || plan.Harvest is null)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected project assistance harvest is unavailable");
            if (!CanReceiveProjectAssistance((plan.RequesterId, plan.RequestedInput, plan.OwnerId)) ||
                FindGoods(ProjectContributionRequest(plan.Actor, plan.Choice.ItemKind)).Matches.Count > 0)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected project gathering step is no longer needed");
            var harvest = plan.Harvest!;
            var source = CheckPlannedProjectHarvest(plan.Actor, plan.Source!, harvest, plan.ToolOwnerId, plan.ToolKind);
            if (!source.Allowed) return source;
            if (FreeCarryCapacity(plan.Actor) < checked(harvest.Quantity + harvest.TreeSeedQuantity))
                return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected project assistance harvest");
            if (HarvestReturnRoute(plan.Actor, plan.Source!, plan.Destination, ReachPurpose.ProjectAssistance,
                    checked(harvest.Quantity + harvest.TreeSeedQuantity), plan.Range).Count == 0)
                return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected assistance materials cannot return to the recipient");
        }
        var target = plan.Source?.Position ?? plan.Destination;
        var range = plan.Source is null ? plan.Range : ResourceInteractionRange;
        return IsWithinInteractionRange(agents[plan.Actor].Position, target, range) ||
            Reach(plan.Actor, target, ReachPurpose.ProjectAssistance, range, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected project assistance destination is out of reach");
    }
}
