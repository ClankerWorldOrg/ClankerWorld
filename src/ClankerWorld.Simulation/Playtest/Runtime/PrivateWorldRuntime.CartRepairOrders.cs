using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private InventoryLot? CartForRepairOrder(string actor, PlaytestInhabitantState person) =>
        society.Checkpoint.Inventory.Lots.Where(lot => lot.ItemKind == InventoryContainerRules.Handcart &&
                lot.OwnerId == actor && lot.ConditionBasisPoints < 10_000 && IsValidInstructionIdentifier(lot.Id) &&
                !HasActiveContainerReservation(society.Checkpoint.Inventory, lot.Id))
            .OrderBy(lot => map.FootDistance(person.Position, CartPosition(lot))).ThenBy(lot => lot.Id, StringComparer.Ordinal)
            .FirstOrDefault(lot => person.Position == CartPosition(lot) || FindUnoccupiedRoute(actor, person.Position, CartPosition(lot), 0).Count > 0);

    private CognitionCandidate CartRepairOrderCandidateFor(OwnerQueuedInstruction instruction, InventoryLot cart)
    {
        var actor = instruction.TargetInhabitantId;
        var collection = CartRepairCollectionPlan(actor, cart);
        var missing = collection is { Count: > 0 } ? collection[0].Kind :
            CartRepairKinds.FirstOrDefault(kind => !HasCarriedMaterial(actor, kind, 1));
        return missing is null
            ? new(RepairCartPrefix + cart.Id, "Reach and repair the selected cart with your carried wood, iron fitting and rope.", 0, cart.Id)
            : new(CollectCartRepairPrefix + missing, "Collect the permitted repair supplies for the selected cart.", 0, cart.Id);
    }

    private List<InputCollectionStep>? CartRepairCollectionPlan(string actor, InventoryLot cart)
    {
        var inputs = new List<InputCollectionStep>();
        foreach (var kind in CartRepairKinds)
        {
            if (HasCarriedMaterial(actor, kind, 1)) continue;
            if (SharedItem(kind, actor, CartPosition(cart)) is not { } stock) return null;
            inputs.Add(StockCollectionStep(stock, 1));
        }
        return PlanInputCollection(actor, inputs, CartPosition(cart));
    }

    private string? CartRepairOrderBlocker(string actor, PlaytestInhabitantState person, InventoryLot cart)
    {
        if (cart.ConditionBasisPoints >= 10_000) return "The selected cart is healthy; no repair is needed.";
        if (HasActiveContainerReservation(society.Checkpoint.Inventory, cart.Id))
            return "The selected cart or its cargo is reserved for another task.";
        if (person.Position != CartPosition(cart) && FindUnoccupiedRoute(actor, person.Position, CartPosition(cart), 0).Count == 0)
            return "Waiting for a clear legal route to the selected cart.";
        var missing = CartRepairKinds.Where(kind => !HasCarriedMaterial(actor, kind, 1)).ToArray();
        if (missing.Length > 0 && missing.Length > FreeCarryCapacity(actor))
            return "Make carrying space for the cart's repair supplies.";
        if (missing.Any(kind => SharedItem(kind, actor, CartPosition(cart)) is null))
            return "The cart repair needs one available wood, iron fitting and rope in your hands or permitted household or Town stock.";
        if (CartRepairCollectionPlan(actor, cart) is null)
            return "Waiting for a legal route to collect the repair supplies and return to the selected cart.";
        return null;
    }

    private void ExecuteCartRepairOrderStep(OwnerQueuedInstruction instruction, PlaytestInhabitantState person, InventoryLot cart)
    {
        var candidate = CartRepairOrderCandidateFor(instruction, cart);
        if (candidate.Id.StartsWith(CollectCartRepairPrefix, StringComparison.Ordinal))
        {
            var collection = CartRepairCollectionPlan(instruction.TargetInhabitantId, cart);
            if (collection is not { Count: > 0 }) return;
            var input = collection[0];
            CollectEquipment(instruction.TargetInhabitantId, person,
                input.Kind, CartPosition(cart), plannedLotId: input.Stock!.Id);
            return;
        }
        ApplyHandcartCandidate(instruction.TargetInhabitantId, person, candidate.Id);
        if (candidate.Id != RepairCartPrefix + cart.Id ||
            society.Checkpoint.Inventory.GetLot(cart.Id).ConditionBasisPoints <= cart.ConditionBasisPoints) return;
        // This receipt identifies this instruction's native repair, not a pre-existing healthy cart.
        CreditOrderEffect(instruction, CartRepairOrderReceipt(instruction, WorldTick), 1);
    }

    private const string CartRepairOrderReceiptPrefix = "cart-repair-order:";

    private static string CartRepairOrderReceipt(OwnerQueuedInstruction instruction, long tick) =>
        CartRepairOrderReceiptPrefix + tick.ToString(CultureInfo.InvariantCulture) + ":" +
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{CartOrderEffectId(instruction)}|repair-handcart:{tick}:{instruction.TargetInhabitantId}:{instruction.Order!.TargetCartLotId}")));

    private static long? CartRepairOrderReceiptTick(OwnerQueuedInstruction instruction, long worldTick)
    {
        var receipt = instruction.Order!.LastEffectId;
        if (receipt is null || receipt.Length > CartRepairOrderReceiptPrefix.Length + 20 + 1 + 64 ||
            !receipt.StartsWith(CartRepairOrderReceiptPrefix, StringComparison.Ordinal)) return null;
        var separator = receipt.IndexOf(':', CartRepairOrderReceiptPrefix.Length);
        return separator > CartRepairOrderReceiptPrefix.Length &&
            long.TryParse(receipt.AsSpan(CartRepairOrderReceiptPrefix.Length, separator - CartRepairOrderReceiptPrefix.Length),
                NumberStyles.None, CultureInfo.InvariantCulture, out var tick) &&
            tick >= instruction.SubmittedTick && tick <= worldTick &&
            receipt == CartRepairOrderReceipt(instruction, tick) ? tick : null;
    }
}
