using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private HouseholdResumeActionPlan? PlanHouseholdResume(string actor, HouseholdResumeChoice choice)
    {
        if (!agents.ContainsKey(actor) || HouseholdFor(actor) is not { } household) return null;
        var matches = PausedHouseholdWork(actor).Where(job => job.Id == choice.JobId).ToArray();
        if (matches.Length != 1) return null;
        var job = matches[0];
        var building = worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == job.BuildingId);
        if (building is null || !CanResumeHeldInputs(actor, job.BuildingId, job.Reservations) || !TryResumeKnife(actor, job.Id, out var knife)) return null;
        return new(actor, choice, household, building, job.Worker, job.Completion, job.PausedAt, job.Reservations.ToArray(), knife,
            worldSimulation.ProductionJobs.SingleOrDefault(item => item.JobId == job.Id),
            (worldSimulation.BuildingExpansions ?? []).SingleOrDefault(item => item.JobId == job.Id),
            Reach(actor, building.Position, ReachPurpose.HouseholdResume, 0, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckHouseholdResumePlan(HouseholdResumeActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? HouseholdCommunityCapabilities);
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId ||
            worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == plan.Building.InstanceId) != plan.Building)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household or selected workstation has changed");
        var jobs = PausedHouseholdWork(plan.Actor).Where(job => job.Id == plan.Choice.JobId).ToArray();
        if (jobs.Length != 1 || jobs[0].BuildingId != plan.Building.InstanceId || jobs[0].Worker != plan.WorkerId ||
            jobs[0].Completion != plan.CompletionTick || jobs[0].PausedAt != plan.PausedAtTick ||
            !jobs[0].Reservations.SequenceEqual(plan.ReservationIds))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected paused job or its committed inputs have changed");
        var production = worldSimulation.ProductionJobs.SingleOrDefault(item => item.JobId == plan.Choice.JobId);
        var expansion = (worldSimulation.BuildingExpansions ?? []).SingleOrDefault(item => item.JobId == plan.Choice.JobId);
        if (production is not null && plan.Production is not null &&
                production with { InputReservationIds = plan.Production.InputReservationIds } != plan.Production ||
            expansion is not null && plan.Expansion is not null &&
                expansion with { InputReservationIds = plan.Expansion.InputReservationIds } != plan.Expansion ||
            (production is null) != (plan.Production is null) || (expansion is null) != (plan.Expansion is null))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected job definition has changed");
        if (!CanResumeHeldInputs(plan.Actor, plan.Building.InstanceId, plan.ReservationIds) ||
            plan.KnifeId is { } knife && ToolProgressionRules.PlanWorkForLot(society.Checkpoint.Inventory, plan.Actor, ToolFamily.Knife, knife) is null)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected committed materials or knife are unavailable");
        // Native movement waits when no route is open. Expansion space is
        // rechecked on arrival, when the existing writer resumes the job.
        return new();
    }

    private void ApplyHouseholdResumePlan(HouseholdResumeActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteHouseholdResume(plan);
    }
}
