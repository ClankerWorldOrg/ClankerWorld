using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CommunitySystem
{
    internal static IEnumerable<(string Id, string BuildingId, string Worker, long Completion, long PausedAt, IReadOnlyList<Identity> Reservations)> PausedHouseholdWork(ICommunityWorkWorld world, string actor)
    {
        var household = world.Society.GetAgent(actor).HouseholdId;
        if (household is null) yield break;
        foreach (var job in world.ProductionJobs.Where(job => job.State == WorldProductionJobState.Paused &&
                     !world.IsActiveProductionOrderJob(job.JobId) &&
                     world.Buildings.Any(building => building.InstanceId == job.BuildingInstanceId && building.HouseholdId == household)))
            yield return (job.JobId, job.BuildingInstanceId, job.WorkerId, job.CompletionTick, job.PausedAtTick!.Value, job.InputReservationIds);
        foreach (var job in (world.Expansions ?? []).Where(job => job.State == WorldProductionJobState.Paused &&
                     job.OrderInstructionId is null && job.OwnerId == household))
            yield return (job.JobId, job.BuildingInstanceId, job.WorkerId, job.CompletionTick, job.PausedAtTick!.Value, job.InputReservationIds);
    }

    internal static bool CanResumeHeldInputs(ICommunityWorkWorld world, string actor, string buildingId, IReadOnlyList<Identity> reservationIds)
    {
        var building = world.Buildings.Single(item => item.InstanceId == buildingId);
        var site = new InventoryGroundPosition(building.Position.X, building.Position.Y);
        return reservationIds.All(id =>
        {
            var reservation = world.Society.Inventory.GetReservation(id);
            var lot = world.Society.Inventory.GetLot(reservation.LotId);
            return reservation.State == InventoryReservationState.Reserved && lot.Quantity >= reservation.Quantity &&
                lot.ConditionBasisPoints > 0 && lot.FreshnessBasisPoints > 0 &&
                (lot.OwnerId == building.HouseholdId || lot.OwnerId == actor) &&
                (lot.StorageBuildingId == buildingId || lot.GroundPosition == site ||
                 PersonalEquipmentRules.IsPhysicallyCarried(world.Society.Inventory, lot, actor));
        });
    }

    internal static bool TryResumeKnife(ICommunityWorkWorld world, string actor, string jobId, out string? knifeId)
    {
        knifeId = world.ProductionJobs.SingleOrDefault(item => item.JobId == jobId)?.ToolLotId;
        if (knifeId is null) return true;
        var inventory = world.Society.Inventory;
        if (ToolProgressionRules.PlanWorkForLot(inventory, actor, ToolFamily.Knife, knifeId) is not null) return true;
        knifeId = ToolProgressionRules.BestUsableTool(inventory, actor, ToolFamily.Knife)?.Id;
        return knifeId is not null;
    }

    internal static void CompleteHouseholdResume(ICommunityWorkWorld world, HouseholdResumeActionPlan plan)
    {
        var actor = plan.Actor;
        var building = plan.Building;
        if (world.PhysicalAgents.Living[actor].Position != building.Position)
        {
            world.MoveToward(actor, world.PhysicalAgents.Living[actor], building.Position, "resume_household_work");
            return;
        }
        var expansion = (world.Expansions ?? []).SingleOrDefault(item => item.JobId == plan.Choice.JobId);
        if (expansion is not null && !world.CanFitExpansion(building, expansion.TargetPosition, expansion.TargetFootprint, expansion.JobId)) return;
        var deadline = checked(world.WorldTick + Math.Max(1, plan.CompletionTick - plan.PausedAtTick));
        world.SetReservationDeadline(plan.ReservationIds, deadline);
        world.ResumeProduction(plan.Choice.JobId, deadline, plan.KnifeId, actor);
        world.ResumeExpansion(plan.Choice.JobId, deadline, actor);
        world.AppendEvent(new HouseholdWorkResumedEventPayload(actor, plan.Choice.JobId, building.HouseholdId));
    }

    internal static void ReconcilePausedHouseholdWork(ICommunityWorkWorld world)
    {
        bool Available(IReadOnlyList<Identity> ids) => ids.All(id =>
        {
            var reservation = world.Society.Inventory.Reservations.SingleOrDefault(item => item.Id == id);
            var lot = reservation is null ? null : world.Society.Inventory.Lots.SingleOrDefault(item => item.Id == reservation.LotId);
            return reservation is { State: InventoryReservationState.Reserved } && lot is { ConditionBasisPoints: > 0, FreshnessBasisPoints: > 0 } &&
                reservation.OwnerId == lot.OwnerId && lot.Quantity >= reservation.Quantity;
        });
        var cancelledProduction = world.ProductionJobs.Where(job => job.State == WorldProductionJobState.Paused &&
            !Available(job.InputReservationIds)).ToArray();
        var cancelledExpansions = (world.Expansions ?? []).Where(job => job.State == WorldProductionJobState.Paused &&
            (!Available(job.InputReservationIds) || job.InputReservationIds.Any(id =>
                world.Society.Inventory.Reservations.SingleOrDefault(item => item.Id == id) is { } reservation &&
                reservation.OwnerId != job.OwnerId && reservation.OwnerId != job.WorkerId))).ToArray();
        foreach (var ids in cancelledProduction.Select(job => job.InputReservationIds).Concat(cancelledExpansions.Select(job => job.InputReservationIds)))
            world.ReleaseUnavailablePausedInputs(ids);
        var productionIds = cancelledProduction.Select(job => job.JobId).ToHashSet(StringComparer.Ordinal);
        var expansionIds = cancelledExpansions.Select(job => job.JobId).ToHashSet(StringComparer.Ordinal);
        world.CancelProduction(productionIds);
        world.CancelPausedExpansions(expansionIds, "Paused expansion materials are no longer available.");
        foreach (var job in cancelledProduction) world.AppendEvent(new RecipeCancelledEventPayload(job.WorkerId, job.JobId, job.RecipeId, null));
        foreach (var job in cancelledExpansions) world.AppendEvent(new BuildingExpansionCancelledEventPayload(job.WorkerId, job.BuildingInstanceId, job.JobId, "Paused expansion materials are no longer available."));
    }

    internal static HouseholdResumeActionPlan? PlanHouseholdResume(ICommunityWorkWorld world, string actor, HouseholdResumeChoice choice)
    {
        if (!world.PhysicalAgents.Living.ContainsKey(actor) || world.HouseholdFor(actor) is not { } household) return null;
        var matches = PausedHouseholdWork(world, actor).Where(job => job.Id == choice.JobId).ToArray();
        if (matches.Length != 1) return null;
        var job = matches[0];
        var building = world.Buildings.SingleOrDefault(item => item.InstanceId == job.BuildingId);
        if (building is null || !CanResumeHeldInputs(world, actor, job.BuildingId, job.Reservations) || !TryResumeKnife(world, actor, job.Id, out var knife)) return null;
        return new(actor, choice, household, building, job.Worker, job.Completion, job.PausedAt, job.Reservations.ToArray(), knife,
            world.ProductionJobs.SingleOrDefault(item => item.JobId == job.Id),
            (world.Expansions ?? []).SingleOrDefault(item => item.JobId == job.Id),
            world.Reach(actor, building.Position, ReachPurpose.HouseholdResume, 0, 0, SwimmingPolicy.WhenCapable));
    }

    internal static ActionCheck CheckHouseholdResumePlan(ICommunityWorkWorld world, HouseholdResumeActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? HouseholdCommunityCapabilities);
        if (!capability.Allowed) return capability;
        if (world.HouseholdFor(plan.Actor) != plan.HouseholdId ||
            world.Buildings.SingleOrDefault(item => item.InstanceId == plan.Building.InstanceId) != plan.Building)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household or selected workstation has changed");
        var jobs = PausedHouseholdWork(world, plan.Actor).Where(job => job.Id == plan.Choice.JobId).ToArray();
        if (jobs.Length != 1 || jobs[0].BuildingId != plan.Building.InstanceId || jobs[0].Worker != plan.WorkerId ||
            jobs[0].Completion != plan.CompletionTick || jobs[0].PausedAt != plan.PausedAtTick ||
            !jobs[0].Reservations.SequenceEqual(plan.ReservationIds))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected paused job or its committed inputs have changed");
        var production = world.ProductionJobs.SingleOrDefault(item => item.JobId == plan.Choice.JobId);
        var expansion = (world.Expansions ?? []).SingleOrDefault(item => item.JobId == plan.Choice.JobId);
        if (production is not null && plan.Production is not null &&
                production with { InputReservationIds = plan.Production.InputReservationIds } != plan.Production ||
            expansion is not null && plan.Expansion is not null &&
                expansion with { InputReservationIds = plan.Expansion.InputReservationIds } != plan.Expansion ||
            (production is null) != (plan.Production is null) || (expansion is null) != (plan.Expansion is null))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected job definition has changed");
        if (!CanResumeHeldInputs(world, plan.Actor, plan.Building.InstanceId, plan.ReservationIds) ||
            plan.KnifeId is { } knife && ToolProgressionRules.PlanWorkForLot(world.Society.Inventory, plan.Actor, ToolFamily.Knife, knife) is null)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected committed materials or knife are unavailable");
        // Native movement waits when no route is open. Expansion space is
        // rechecked on arrival, when the existing writer resumes the job.
        return new();
    }
}
