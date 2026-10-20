namespace ClankerWorld.Simulation.World;

public static partial class WorldHistory
{
    public const int RecentProductionJobLimit = 32;

    private static WorldProductionJob[] ClosedProductionJobs(WorldContentSimulationState simulation) =>
        simulation.ProductionJobs.Where(job => job.State is WorldProductionJobState.Completed or WorldProductionJobState.Cancelled)
            .OrderBy(job => job.StartedTick).ThenBy(job => job.JobId, StringComparer.Ordinal).ToArray();

    /// <summary>Only current work and the recent terminal window belong in each owner response.</summary>
    public static WorldProductionJob[] ProductionJobsForInspection(WorldContentSimulationState simulation)
    {
        var recent = ClosedProductionJobs(simulation).TakeLast(RecentProductionJobLimit)
            .Select(job => job.JobId).ToHashSet(StringComparer.Ordinal);
        return simulation.ProductionJobs.Where(job => job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused ||
            recent.Contains(job.JobId)).ToArray();
    }

    private static WorldContentSimulationState CompactProduction(WorldRuntimeState state, List<ArchivedEventBatch> archived)
    {
        var simulation = state.WorldSimulation!;
        var knowledge = state.Knowledge!;
        var required = knowledge.Recipes.Concat(knowledge.Artifacts.SelectMany(artifact => artifact.Recipes))
            .Concat(knowledge.WritingProjects.SelectMany(project => project.Recipes))
            .Select(recipe => recipe.SourceProductionJobId).ToHashSet(StringComparer.Ordinal);
        required.UnionWith(state.Agents.Concat((state.DeceasedAgents ?? []).Select(person => person.LastPhysical))
            .Where(person => person.Project is { Stage: not ("completed" or "cancelled") })
            .Select(person => person.Project!.JobId).OfType<string>());
        required.UnionWith((state.ToolMakingRequests ?? []).Select(request => request.JobId).OfType<string>());
        var instructions = state.Instructions ?? [];
        required.UnionWith(instructions.Select(instruction => instruction.Task?.ProductionJobId).OfType<string>());
        // Saved order progress counts every real completed batch, not just its
        // latest job. Order retention owns when these links can be retired.
        var orderIds = instructions.Select(instruction => instruction.InstructionId).ToHashSet(StringComparer.Ordinal);
        required.UnionWith(simulation.ProductionJobs.Where(job => job.OrderInstructionId is { } id && orderIds.Contains(id))
            .Select(job => job.JobId));
        required.UnionWith(GoodsInventoryQuery.FindFoodProductionSources(state.Society.Society.Inventory, simulation.ProductionJobs)
            .Select(job => job.JobId));
        var retired = Retire(ClosedProductionJobs(simulation), RecentProductionJobLimit, job => required.Contains(job.JobId));
        if (retired.Length == 0) return simulation;
        var retiredIds = retired.Select(job => job.JobId).ToHashSet(StringComparer.Ordinal);
        return ProductionSystem.ArchiveJobs(simulation, retiredIds,
            ArchiveRecords("production-jobs", retired, simulation.ArchivedProductionJobCount, archived));
    }
}
