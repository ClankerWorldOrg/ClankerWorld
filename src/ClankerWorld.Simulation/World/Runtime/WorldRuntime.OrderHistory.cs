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
            productionSystem.RetireOrderLinks(retired);
            constructionSystem.Buildings.RetireOrderLinks(retired);
            RetireClosedOrderProjectReferences(retired);
            RetireClosedOrderExplorationGoals(retired);
            boatTransport = boatTransport with
            {
                Requests = boatTransport.Requests.Select(request => retired.Contains(request.OrderInstructionId ?? "")
                    ? request with { OrderInstructionId = null } : request).ToArray(),
            };
            communitySystem.RetireOrderLinks(retired);
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
