using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
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

    private static string CartOrderEffectId(OwnerQueuedInstruction instruction) =>
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
            repairReservations ??= inventory.Reservations.ToDictionary(item => item.Id, StringComparer.Ordinal);
            if (CartRepairOrderReceiptTick(instruction, inventory.WorldTick) is not { } tick ||
                !CartRepairKinds.All(kind => repairReservations.TryGetValue(CartRepairReservationId(tick, instruction.TargetAgentId, kind), out var input) &&
                    input.OwnerId == instruction.TargetAgentId && input.Quantity == 1 && input.Purpose == "equipment_repair" &&
                    input.ExpiryTick == tick + 1 && input.State == InventoryReservationState.Completed))
                throw new InvalidDataException("A cart repair order lacks its actual consumed repair inputs.");
        }
    }
}
