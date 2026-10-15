namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private TownProjectRecoveryActionPlan PlanTownProjectRecovery(string actor, TownProjectChoice choice)
    {
        var recover = choice.Kind == "recover";
        var target = recover ? HouseholdStockPosition(choice.Lot!) : choice.Warehouse?.Position ?? agents[actor].Position;
        var destination = choice.Warehouse?.Position ?? target;
        return new(actor, new(recover, choice.Project.Id, choice.Lot!.Id, choice.Quantity, choice.Warehouse?.InstanceId),
            choice.Town.Id, choice.Project.Plan, choice.ItemKind, choice.Delivery!, target, destination,
            Reach(actor, target, ReachPurpose.TownProjectRecovery, 0, 0, SwimmingPolicy.WhenCapable,
                recover ? ReachObstacles.OccupiedTiles : ReachObstacles.Terrain),
            ReachLeg(actor, recover ? target : agents[actor].Position, destination, ReachPurpose.TownProjectRecovery,
                0, 0, SwimmingPolicy.WhenCapable, recover ? ReachObstacles.OccupiedTiles : ReachObstacles.Terrain));
    }

    private ActionCheck CheckTownProjectRecovery(TownProjectRecoveryActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? TownProjectRecoveryCapabilities(plan));
        if (!capability.Allowed) return capability;
        if (plan.Choice.Recover && !ReadyForTownProject(plan.Actor) || !plan.Choice.Recover &&
            !ReadyForTownProject(plan.Actor) && !NeedsUrgentFood(agents[plan.Actor]) && !NeedsUrgentWarmth(agents[plan.Actor]))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their current work or survival ability has changed");
        if (towns.SingleOrDefault(town => town.Id == plan.TownId) is not { } town ||
            town.Projects.SingleOrDefault(project => project.Id == plan.Choice.ProjectId) is not { } project ||
            TownHearingProcedure.Digest(project.Plan) != TownHearingProcedure.Digest(plan.Project) ||
            !project.Deliveries.Contains(plan.ReleasedDelivery) || plan.ReleasedDelivery.ReleasedTick is null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected released project load has changed");
        var warehouse = plan.Choice.WarehouseId is null ? null : WarehousesForTown(plan.TownId).SingleOrDefault(item => item.InstanceId == plan.Choice.WarehouseId);
        if (plan.Choice.WarehouseId is not null && (warehouse is null || warehouse.Position != plan.Destination))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected return Warehouse has changed");
        var selected = CurrentTownProjectReleasedChoice(plan, town, project, warehouse);
        if (selected is null || selected.Quantity < plan.Choice.Quantity || selected.ItemKind != plan.ItemKind ||
            plan.Choice.Recover && HouseholdStockPosition(selected.Lot!) != plan.Target)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected released goods, carrying room or Warehouse room are unavailable");
        if (!plan.Choice.Recover && warehouse is null)
            return agents[plan.Actor].Position == plan.Destination ? new() :
                ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "their selected ground-drop position has changed");
        var origin = plan.Choice.Recover ? plan.Target : agents[plan.Actor].Position;
        var obstacles = plan.Choice.Recover ? ReachObstacles.OccupiedTiles : ReachObstacles.Terrain;
        if (origin != plan.Destination && ReachLeg(plan.Actor, origin, plan.Destination, ReachPurpose.TownProjectRecovery,
                0, 0, SwimmingPolicy.WhenCapable, obstacles).Count == 0)
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected released load cannot return to its Warehouse");
        return !plan.Choice.Recover || agents[plan.Actor].Position == plan.Target ||
            Reach(plan.Actor, plan.Target, ReachPurpose.TownProjectRecovery, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected released load is out of reach");
    }

    private void ApplyCheckedTownProjectRecovery(TownProjectRecoveryActionPlan plan)
    {
        var town = towns.Single(item => item.Id == plan.TownId);
        var project = town.Projects.Single(item => item.Id == plan.Choice.ProjectId);
        var warehouse = plan.Choice.WarehouseId is null ? null : WarehousesForTown(plan.TownId).Single(item => item.InstanceId == plan.Choice.WarehouseId);
        var selected = CurrentTownProjectReleasedChoice(plan, town, project, warehouse)! with { Id = plan.CandidateId, Quantity = plan.Choice.Quantity };
        if (plan.Choice.Recover) RecoverTownProjectLoad(plan.Actor, agents[plan.Actor], selected);
        else ReturnTownProjectLoad(plan.Actor, agents[plan.Actor], selected);
    }
}
