using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private BoatTripRequest? BoundBoatRequest(OwnerQueuedInstruction instruction) =>
        OrderSystem.BoundBoatRequest(BoatOrdersWorld, instruction);

    private CognitionCandidate? BoatOrderCandidate(OwnerQueuedInstruction instruction) =>
        OrderSystem.BoatOrderCandidate(BoatOrdersWorld, instruction);

    private void ExecuteBoatOrder(OwnerQueuedInstruction instruction) =>
        OrderSystem.ExecuteBoatOrder(BoatOrdersWorld, instruction);

    private string BoatOrderBlockedReason(OwnerQueuedInstruction instruction) =>
        OrderSystem.BoatOrderBlockedReason(BoatOrdersWorld, instruction);

    private OwnerQueuedInstruction? ActiveBoatOrder(BoatTripRequest request) =>
        OrderSystem.ActiveBoatOrder(BoatOrdersWorld, request);

    private void SetBoatOrderTravelStatus(BoatTripRequest request, OrderStatus status, string? reason) =>
        OrderSystem.SetBoatOrderTravelStatus(BoatOrdersWorld, request, status, reason);

    private void CreditBoatOrderArrival(BoatTripRequest request) =>
        OrderSystem.CreditBoatOrderArrival(BoatOrdersWorld, request);

    private static string BoatOrderArrivalEffectId(string requestId) => DerivedId.Create("boat-arrival:" + requestId);

    internal static bool IsValidBoatOrderShape(OrderTask order) =>
        order.BoatTravel is { DestinationPortId.Length: > 0 and <= DerivedId.MaximumLength } binding &&
        !binding.DestinationPortId.Any(char.IsControl) &&
        (binding.RequestId is null || binding.RequestId.Length is > 0 and <= DerivedId.MaximumLength && !binding.RequestId.Any(char.IsControl)) &&
        order.TargetPosition is not null && order.TargetResourceId is null && order.TargetAgentId is null && order.TargetFoodKind is null &&
        order.RequestedUnits == 1 && order.CompletedUnits is 0 or 1 && order.ProgressUnit == "arrivals" &&
        !order.RepeatUntilCancelled && !order.QuantityIsExplicit && order.Status != "not_understood" &&
        (order.Status == "finished") == (order.CompletedUnits == 1) &&
        (order.CompletedUnits == 0 || binding.RequestId is not null) &&
        order.LastEffectId == (order.CompletedUnits == 1 && binding.RequestId is not null ? BoatOrderArrivalEffectId(binding.RequestId) : null);

    private static void ValidateBoatOrderBindings(WorldRuntimeState state)
    {
        if (!(state.Instructions ?? []).Any(instruction => instruction.Task?.Action == "travel_by_boat") &&
            !state.BoatTransport.Requests.Any(request => request.OrderInstructionId is not null)) return;
        var instructions = (state.Instructions ?? []).ToDictionary(instruction => instruction.InstructionId, StringComparer.Ordinal);
        var requests = state.BoatTransport.Requests.ToDictionary(request => request.Id, StringComparer.Ordinal);
        var requestsByInstruction = state.BoatTransport.Requests.Where(request => request.OrderInstructionId is not null)
            .ToLookup(request => request.OrderInstructionId!, StringComparer.Ordinal);
        foreach (var request in requests.Values.Where(request => request.OrderInstructionId is not null))
        {
            if (!instructions.TryGetValue(request.OrderInstructionId!, out var instruction) ||
                instruction.Task is not { Action: "travel_by_boat", BoatTravel: { } binding } ||
                instruction.TargetAgentId != request.PassengerId || binding.DestinationPortId != request.DestinationPortId ||
                request.RequestedTick < instruction.SubmittedTick)
                throw new InvalidDataException("An ordered boat request must retain its exact instruction, passenger and destination.");
        }
        foreach (var instruction in instructions.Values.Where(instruction => instruction.Task?.Action == "travel_by_boat"))
        {
            var order = instruction.Task!;
            if (!IsValidBoatOrderShape(order)) throw new InvalidDataException("A boat order must retain one stable destination and real arrival progress.");
            var binding = order.BoatTravel!;
            var port = state.WorldSimulation?.Buildings.FirstOrDefault(port => port.InstanceId == binding.DestinationPortId);
            if (port is null && !(state.Towns ?? []).SelectMany(town => town.Projects).Any(project =>
                    project.CompletedBuildingId == binding.DestinationPortId && project.RemovedTick is not null &&
                    project.Plan.Site == order.TargetPosition && state.WorldContent!.Buildings.Any(definition =>
                        definition.CanonicalId == project.Plan.DefinitionId && PortNavigationRules.IsPort(definition))) ||
                port is not null && (port.Position != order.TargetPosition || !state.WorldContent!.Buildings.Any(definition =>
                    definition.CanonicalId == port.DefinitionId && PortNavigationRules.IsPort(definition))))
                throw new InvalidDataException("A boat order's named Port must match its saved destination tile.");
            var owned = requestsByInstruction[instruction.InstructionId].ToArray();
            var latest = owned.LastOrDefault();
            if (binding.RequestId != latest?.Id || latest is not null &&
                (order.Status == "finished" && latest.Status != "arrived" ||
                 IsActiveOrder(order.Status) && latest.Status == "arrived" ||
                 order.Status == "cancelled" && latest.Status == "waiting"))
                throw new InvalidDataException("A boat order must bind its latest real request; only destination arrival completes it.");
            for (var index = 1; index < owned.Length; index++)
                if (owned[index - 1].SettledTick is not { } settled || owned[index].RequestedTick < settled)
                    throw new InvalidDataException("A boat order cannot start another request while its earlier journey is active.");
        }
    }
}
