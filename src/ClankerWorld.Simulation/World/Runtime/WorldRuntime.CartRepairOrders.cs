using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
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
