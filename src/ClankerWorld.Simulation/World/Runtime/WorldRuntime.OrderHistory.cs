namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void PruneClosedOrderHistory()
    {
        var retiredInstructions = orderStore.PruneClosedOrders();
        if (retiredInstructions.Length != 0)
        {
            var retired = retiredInstructions.Select(item => item.InstructionId).ToHashSet(StringComparer.Ordinal);

            // Retire order-only links and construction evidence. Consumed
            // reservations, native work outcomes and physical state stay intact.
            productionSystem.RetireOrderLinks(retired);
            constructionSystem.Buildings.RetireOrderLinks(retired);
            RetireClosedOrderProjectReferences(retired);
            RetireClosedOrderExplorationGoals(retired);
            transportSystem.RetireOrderLinks(retired);
            communitySystem.RetireOrderLinks(retired);
        }
        orderStore.PruneCancellationRetries();

    }

}
