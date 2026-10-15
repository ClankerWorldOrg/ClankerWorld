using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private InventoryLot? CartForRepairOrder(string actor, AgentState person) =>
        OrderSystem.CartForRepairOrder(CartRepairOrdersWorld, actor, person);

    private CognitionCandidate CartRepairOrderCandidateFor(OwnerQueuedInstruction instruction, InventoryLot cart) =>
        OrderSystem.CartRepairOrderCandidateFor(CartRepairOrdersWorld, instruction, cart);

    private List<InputCollectionStep>? CartRepairCollectionPlan(string actor, InventoryLot cart) =>
        OrderSystem.CartRepairCollectionPlan(CartRepairOrdersWorld, actor, cart);

    private string? CartRepairOrderBlocker(string actor, AgentState person, InventoryLot cart) =>
        OrderSystem.CartRepairOrderBlocker(CartRepairOrdersWorld, actor, person, cart);

    private void ExecuteCartRepairOrderStep(OwnerQueuedInstruction instruction, AgentState person, InventoryLot cart) =>
        OrderSystem.ExecuteCartRepairOrderStep(CartRepairOrdersWorld, instruction, person, cart);

    private const string CartRepairOrderReceiptPrefix = "cart-repair-order:";

    private static string CartRepairOrderReceipt(OwnerQueuedInstruction instruction, long tick) =>
        DerivedId.Hash(
            $"{CartOrderEffectId(instruction)}|repair-handcart:{tick}:{instruction.TargetAgentId}:{instruction.Task!.TargetCartLotId}",
            CartRepairOrderReceiptPrefix + tick.ToString(CultureInfo.InvariantCulture) + ":");

    private static long? CartRepairOrderReceiptTick(OwnerQueuedInstruction instruction, long worldTick)
    {
        var receipt = instruction.Task!.LastEffectId;
        if (receipt is null || receipt.Length > DerivedId.MaximumLength ||
            !receipt.StartsWith(CartRepairOrderReceiptPrefix, StringComparison.Ordinal)) return null;
        var separator = receipt.IndexOf(':', CartRepairOrderReceiptPrefix.Length);
        return separator > CartRepairOrderReceiptPrefix.Length &&
            long.TryParse(receipt.AsSpan(CartRepairOrderReceiptPrefix.Length, separator - CartRepairOrderReceiptPrefix.Length),
                NumberStyles.None, CultureInfo.InvariantCulture, out var tick) &&
            tick >= instruction.SubmittedTick && tick <= worldTick &&
            receipt == CartRepairOrderReceipt(instruction, tick) ? tick : null;
    }
}
