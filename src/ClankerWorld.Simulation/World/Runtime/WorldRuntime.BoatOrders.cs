using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

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

    private static bool IsCartOrder(string action) => action is "attach_handcart" or "park_handcart" or "repair_handcart" || IsCartCargoOrder(action);

    private OrderTask? ParseCartOrder(string text, string actor) =>
        OrderSystem.ParseCartOrder(CartOrdersWorld, text, actor);

    private InventoryLot? CartForOrder(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.CartForOrder(CartOrdersWorld, instruction, person);

    private static GridPoint CartPosition(InventoryLot cart) => new(cart.GroundPosition!.Value.X, cart.GroundPosition.Value.Y);

    private CognitionCandidate? CartOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.CartOrderCandidateFor(CartOrdersWorld, instruction, person);

    private string? CartOrderBlocker(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.CartOrderBlocker(CartOrdersWorld, instruction, person);

    private void ExecuteCartOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteCartOrderStep(CartOrdersWorld, instruction, person);

    internal static string CartOrderEffectId(OwnerQueuedInstruction instruction) =>
        DerivedId.Hash(
            $"{instruction.InstructionId}|{instruction.TargetAgentId}|{instruction.Task!.Action}|{instruction.Task.TargetCartLotId}", "cart-order:");

    private static void ValidateCartOrderBindings(InventoryCheckpoint inventory, IEnumerable<OwnerQueuedInstruction> instructions)
    {
        var carts = inventory.Lots.Where(lot => lot.ItemKind == InventoryContainerRules.Handcart).Select(lot => lot.Id).ToHashSet(StringComparer.Ordinal);
        Dictionary<string, InventoryReservation>? repairReservations = null;
        foreach (var instruction in instructions)
        {
            if (instruction.Task is not { } order || !IsCartOrder(order.Action)) continue;
            if (order.TargetCartLotId is { } id && !carts.Contains(id))
                throw new InvalidDataException("A cart order references a missing handcart.");
            if (order.Action != "repair_handcart" || order.CompletedUnits == 0) continue;
            repairReservations ??= inventory.Reservations.ToDictionary(item => item.Id.Value, StringComparer.Ordinal);
            if (CartRepairOrderReceiptTick(instruction, inventory.WorldTick) is not { } tick ||
                !CartRepairKinds.All(kind => repairReservations.TryGetValue(CartRepairReservationId(tick, instruction.TargetAgentId, kind), out var input) &&
                    input.OwnerId == instruction.TargetAgentId && input.Quantity == 1 && input.Purpose == "equipment_repair" &&
                    input.ExpiryTick == tick + 1 && input.State == InventoryReservationState.Completed))
                throw new InvalidDataException("A cart repair order lacks its actual consumed repair inputs.");
        }
    }

    private static bool IsCartCargoOrder(string action) => action is "load_handcart" or "unload_handcart" or "unload_handcart_ground";

    private static bool IsCartCargoKind(string? kind) => kind is not null &&
        (WorldCustodyOrderCatalog.IsReturnKind(kind) || IsEdibleFood(kind)) &&
        InventoryContainerRules.Allows(InventoryContainerRules.Handcart, kind);

    private InventoryLot? CartForCargoOrder(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.CartForCargoOrder(CartCargoOrdersWorld, instruction, person);

    private InventoryLot? CargoLotForOrder(OwnerQueuedInstruction instruction, AgentState person, InventoryLot cart) =>
        OrderSystem.CargoLotForOrder(CartCargoOrdersWorld, instruction, person, cart);

    private string? CartCargoOrderBlocker(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.CartCargoOrderBlocker(CartCargoOrdersWorld, instruction, person);

    private CognitionCandidate? CartCargoOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.CartCargoOrderCandidateFor(CartCargoOrdersWorld, instruction, person);

    private int CargoQuantity(string cartId) =>
        OrderSystem.CargoQuantity(CartCargoOrdersWorld, cartId);

    private void ExecuteCartCargoOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteCartCargoOrderStep(CartCargoOrdersWorld, instruction, person);

    private static string CartCargoOrderEffectId(OwnerQueuedInstruction instruction, int completed) =>
        DerivedId.Hash(
            $"{instruction.InstructionId}|{instruction.TargetAgentId}|{instruction.Task!.Action}|{instruction.Task.TargetCartLotId}|{instruction.Task.TargetItemKind}|{completed}", "cart-cargo-order:");

    internal static bool IsValidCartCargoOrder(OwnerQueuedInstruction instruction)
    {
        var order = instruction.Task!;
        return IsCartCargoKind(order.TargetItemKind) &&
            (order.TargetCartLotId is null || IdentityRules.Reference(order.TargetCartLotId, OwnerQueuedInstruction.MaximumIdentifierLength) && order.TargetCartLotId == order.TargetCartLotId.Trim()) &&
            (order.TargetLotId is null || order.TargetCartLotId is not null) &&
            order.RequestedUnits is >= 1 and <= 1000 && order.CompletedUnits >= 0 && order.CompletedUnits <= order.RequestedUnits &&
            !order.RepeatUntilCancelled && order.QuantityIsExplicit && order.ProgressUnit == "goods_items" &&
            order.TargetFoodKind is null && order.TargetAgentId is null && order.TargetResourceId is null && order.TargetPosition is null &&
            order.Status != "not_understood" && (order.Status == "finished") == (order.CompletedUnits == order.RequestedUnits) &&
            (order.CompletedUnits == 0 ? order.LastEffectId is null :
                order.TargetCartLotId is not null && order.LastEffectId == CartCargoOrderEffectId(instruction, order.CompletedUnits));
    }

    private InventoryLot? CartForRepairOrder(string actor, AgentState person) =>
        OrderSystem.CartForRepairOrder(CartRepairOrdersWorld, actor, person);

    private CognitionCandidate CartRepairOrderCandidateFor(OwnerQueuedInstruction instruction, InventoryLot cart) =>
        OrderSystem.CartRepairOrderCandidateFor(CartRepairOrdersWorld, instruction, cart);

    private string? CartRepairOrderBlocker(string actor, AgentState person, InventoryLot cart) =>
        OrderSystem.CartRepairOrderBlocker(CartRepairOrdersWorld, actor, person, cart);

    private void ExecuteCartRepairOrderStep(OwnerQueuedInstruction instruction, AgentState person, InventoryLot cart) =>
        OrderSystem.ExecuteCartRepairOrderStep(CartRepairOrdersWorld, instruction, person, cart);

    private const string CartRepairOrderReceiptPrefix = "cart-repair-order:";

    private static string CartRepairOrderReceipt(OwnerQueuedInstruction instruction, long tick) =>
        DerivedId.Hash(
            $"{CartOrderEffectId(instruction)}|repair-handcart:{tick}:{instruction.TargetAgentId}:{instruction.Task!.TargetCartLotId}",
            CartRepairOrderReceiptPrefix + tick.ToString(CultureInfo.InvariantCulture) + ":");

    internal static long? CartRepairOrderReceiptTick(OwnerQueuedInstruction instruction, long worldTick)
    {
        var receipt = instruction.Task!.LastEffectId;
        if (receipt is null || receipt.Value.Length > DerivedId.MaximumLength ||
            !receipt.Value.StartsWith(CartRepairOrderReceiptPrefix, StringComparison.Ordinal)) return null;
        var separator = receipt.Value.IndexOf(':', CartRepairOrderReceiptPrefix.Length);
        return separator > CartRepairOrderReceiptPrefix.Length &&
            long.TryParse(receipt.Value.AsSpan(CartRepairOrderReceiptPrefix.Length, separator - CartRepairOrderReceiptPrefix.Length),
                NumberStyles.None, CultureInfo.InvariantCulture, out var tick) &&
            tick >= instruction.SubmittedTick && tick <= worldTick &&
            receipt == CartRepairOrderReceipt(instruction, tick) ? tick : null;
    }

    internal static IEnumerable<string> RequiredCartRepairReservations(IEnumerable<OwnerQueuedInstruction> instructions, long tick) =>
        instructions.Where(instruction => instruction.Task is { Action: "repair_handcart", CompletedUnits: > 0 })
            .SelectMany(instruction => CartRepairOrderReceiptTick(instruction, tick) is { } repairedTick
                ? CartRepairKinds.Select(kind => CartRepairReservationId(repairedTick, instruction.TargetAgentId, kind)) : []);
}
