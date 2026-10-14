using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void CancelUnavailableWorkers()
    {
        var active = society.Checkpoint.Agents.Where(person => person.Status == SocietyAgentStatus.Active)
            .Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        var cancelled = worldSimulation.ProductionJobs.Concat(worldSimulation.CropBuilds ?? [])
            .Where(job => job.State == WorldProductionJobState.Running && !active.Contains(job.WorkerId))
            .OrderBy(job => job.JobId, StringComparer.Ordinal).ToArray();
        CancelProductionJobs(cancelled, "production_worker_unavailable");
    }

    private void CancelProductionJobs(WorldProductionJob[] cancelled, string reason)
    {
        if (cancelled.Length == 0) return;
        ApplyInventoryTransition(inventory =>
        {
            foreach (var job in cancelled)
            {
                foreach (var id in job.InputReservationIds.Order())
                {
                    if (inventory.GetReservation(id).State is InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed)
                        inventory = InventoryAuthority.ReleaseReservation(inventory, id, reason);
                }
            }
            return inventory;
        });
        var ids = cancelled.Select(job => job.JobId).ToHashSet(StringComparer.Ordinal);
        worldSimulation = worldSimulation with
        {
            ProductionJobs = worldSimulation.ProductionJobs.Select(job => ids.Contains(job.JobId)
                ? job with { State = WorldProductionJobState.Cancelled } : job).ToArray(),
            CropBuilds = worldSimulation.CropBuilds?.Select(job => ids.Contains(job.JobId)
                ? job with { State = WorldProductionJobState.Cancelled } : job).ToArray(),
        };
        foreach (var job in cancelled) AppendEvent(WorldEvents.ProductionFailure(reason, job.WorkerId, job.JobId));
    }
}
