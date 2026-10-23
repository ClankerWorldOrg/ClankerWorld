using DeliveryOrderPlan = ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan;
using DeliveryNativeEffect = ClankerWorld.Simulation.World.OrderSystem.DeliveryNativeEffect;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{


    private int DeliveryResourceQuantity(InventoryLot carrier, string kind) =>
        OrderSystem.DeliveryResourceQuantity(DeliveryOrderSelectionWorld, carrier, kind);

    private bool DeliveryDestinationMatches(OrderTask order, PlacedBuilding building) =>
        OrderSystem.DeliveryDestinationMatches(DeliveryOrderSelectionWorld, order, building);

    private static int DeliveryOrderRemaining(OrderTask order) =>
        order.QuantityIsExplicit && !order.RepeatUntilCancelled ? order.RequestedUnits - order.CompletedUnits : int.MaxValue;

    private DeliveryOrderPlan? HouseholdDeliveryOrderPlan(string actor, AgentState person,
        OrderTask order, int maximumQuantity) =>
        OrderSystem.HouseholdDeliveryOrderPlan(DeliveryOrderExecutionWorld, actor, person, order, maximumQuantity);

    private string? DeliveryRouteForCarried(OrderTask order, PlacedBuilding destination) =>
        OrderSystem.DeliveryRouteForCarried(DeliveryOrderExecutionWorld, order, destination);

    private DeliveryOrderPlan? CarriedDeliveryOrderPlan(string actor, OrderTask order) =>
        OrderSystem.CarriedDeliveryOrderPlan(DeliveryOrderExecutionWorld, actor, order);

    private DeliveryOrderPlan? DeliveryPlanFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.DeliveryPlanFor(DeliveryOrderSelectionWorld, instruction, person);

    private (PlacedBuilding Building, string Route, MapResource Source)? DeliveryGatherFor(
        OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.DeliveryGatherFor(DeliveryOrderSelectionWorld, instruction, person);

    private CognitionCandidate? DeliveryOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.DeliveryOrderCandidateFor(DeliveryOrderExecutionWorld, instruction, person);

    private OwnerQueuedInstruction BindDeliveryOrder(OwnerQueuedInstruction instruction, PlacedBuilding destination,
        string ownerId, string route, string? lotId = null, int? quantity = null) =>
        OrderSystem.BindDeliveryOrder(DeliveryOrderExecutionWorld, instruction, destination, ownerId, route, lotId, quantity);

    private DeliveryNativeEffect? ApplyDeliveryOrderPlan(string actor, AgentState person,
        DeliveryOrderPlan plan) =>
        OrderSystem.ApplyDeliveryOrderPlan(DeliveryOrderExecutionWorld, actor, person, plan);

    private void ExecuteDeliveryOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteDeliveryOrderStep(DeliveryOrderExecutionWorld, instruction, person);

    private string DeliveryOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.DeliveryOrderBlockedReason(DeliveryOrderExecutionWorld, instruction, person);

    private static void ValidateDeliveryOrderBindings(WorldContentSimulationState? simulation, SocietyCheckpoint society,
        IReadOnlyList<TownRuntimeState>? savedTowns, IEnumerable<OwnerQueuedInstruction> instructions)
    {
        foreach (var instruction in instructions.Where(instruction => instruction.Task?.Action == "deliver_stock"))
        {
            var order = instruction.Task!;
            if (order.TargetStorageOwnerId is { } owner &&
                !(order.DeliveryPurpose == "town_surplus" ? savedTowns?.Any(town => town.Id == owner) == true :
                    society.Households.Any(household => household.Id == owner)))
                throw new InvalidDataException("A delivery order references an unknown destination owner.");
            // A removed building or changed physical shipment is a loadable blocker,
            // including after departure or death. Never substitute a new destination.
            if (simulation is null && order.TargetStorageBuildingId is not null)
                throw new InvalidDataException("A delivery order requires a saved settlement.");
        }
    }
}
