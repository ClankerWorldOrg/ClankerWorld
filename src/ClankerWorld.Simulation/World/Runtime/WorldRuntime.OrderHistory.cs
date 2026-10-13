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
            RetireClosedOrderProjectReferences(retired);
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
}
