namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void PruneClosedOrderHistory()
    {
        var retiredInstructions = instructionsByIdempotency.PruneClosedOrders();
        if (retiredInstructions.Length != 0)
        {
            var retired = retiredInstructions.Select(item => item.InstructionId).ToHashSet(StringComparer.Ordinal);
            foreach (var key in orderCancellations.Where(item => retired.Contains(item.Value.OrderId)).Select(item => item.Key).ToArray())
                orderCancellations.Remove(key);

            // Retire order-only links and construction evidence. Consumed
            // reservations, native work outcomes and physical state stay intact.
            worldSimulation = worldSimulation with
            {
                ProductionJobs = worldSimulation.ProductionJobs.Select(job => retired.Contains(job.OrderInstructionId ?? "")
                    ? job with { OrderInstructionId = null } : job).ToArray(),
                BuildingExpansions = worldSimulation.BuildingExpansions?.Select(job => retired.Contains(job.OrderInstructionId ?? "")
                    ? job with { OrderInstructionId = null } : job).ToArray(),
                ConstructionReceipts = worldSimulation.ConstructionReceipts?.Where(receipt => !retired.Contains(receipt.InstructionId)).ToArray(),
            };
            RetireClosedOrderAgentReferences(retired);
            boatTransport = boatTransport with
            {
                Requests = boatTransport.Requests.Select(request => retired.Contains(request.OrderInstructionId ?? "")
                    ? request with { OrderInstructionId = null } : request).ToArray(),
            };
            knowledge = knowledge with
            {
                WritingProjects = knowledge.WritingProjects.Select(project => retired.Contains(project.OrderInstructionId ?? "")
                    ? project with { OrderInstructionId = null } : project).ToArray(),
                Artifacts = knowledge.Artifacts.Select(artifact => retired.Contains(artifact.OrderInstructionId ?? "")
                    ? artifact with { OrderInstructionId = null } : artifact).ToArray(),
            };
        }

        var expiredRetries = orderCancellations.Values.GroupBy(item => item.TargetAgentId, StringComparer.Ordinal)
            .SelectMany(group => group.OrderByDescending(item => item.Receipt.WorldTick)
                .ThenByDescending(item => item.Receipt.LatestEventId)
                .ThenByDescending(item => item.IdempotencyKey, StringComparer.Ordinal)
                .Skip(OwnerQueuedInstruction.RecentClosedOrderLimitPerAgent))
            .Select(item => item.IdempotencyKey).ToArray();
        foreach (var key in expiredRetries) orderCancellations.Remove(key);
    }

    private void RetireClosedOrderAgentReferences(HashSet<string> retired)
    {
        foreach (var person in agents.Values.Where(person =>
                     retired.Contains(person.Project?.OrderInstructionId ?? "") ||
                     retired.Contains(person.Exploration?.Goal?.OrderInstructionId ?? "")).ToArray())
            agents[person.AgentId] = person with
            {
                Project = retired.Contains(person.Project?.OrderInstructionId ?? "")
                    ? person.Project! with { OrderInstructionId = null } : person.Project,
                Exploration = RetireClosedOrderExplorationGoal(person.Exploration, retired),
            };
        foreach (var person in deceasedAgents.Values.Where(person =>
                     retired.Contains(person.LastPhysical.Exploration?.Goal?.OrderInstructionId ?? "")).ToArray())
            deceasedAgents[person.AgentId] = person with
            {
                LastPhysical = person.LastPhysical with
                { Exploration = RetireClosedOrderExplorationGoal(person.LastPhysical.Exploration, retired) },
            };
    }

    private static ExplorationState? RetireClosedOrderExplorationGoal(ExplorationState? exploration, HashSet<string> retired) =>
        retired.Contains(exploration?.Goal?.OrderInstructionId ?? "") ? exploration! with { Goal = null } : exploration;
}
