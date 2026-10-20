using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private InventoryLot? CartForRepairOrder(string actor, AgentState person) =>
        society.Checkpoint.Inventory.Lots.Where(lot => lot.ItemKind == InventoryContainerRules.Handcart &&
                lot.OwnerId == actor && lot.ConditionBasisPoints < 10_000 && IdentityRules.Reference(lot.Id, OwnerQueuedInstruction.MaximumIdentifierLength) &&
                !HasActiveContainerReservation(society.Checkpoint.Inventory, lot.Id))
            .OrderBy(lot => map.FootDistance(person.Position, CartPosition(lot))).ThenBy(lot => lot.Id, StringComparer.Ordinal)
            .FirstOrDefault(lot => person.Position == CartPosition(lot) || FindUnoccupiedRoute(actor, person.Position, CartPosition(lot), 0).Count > 0);

    private CognitionCandidate CartRepairOrderCandidateFor(OwnerQueuedInstruction instruction, InventoryLot cart)
    {
        var actor = instruction.TargetAgentId;
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

    private string? CartRepairOrderBlocker(string actor, AgentState person, InventoryLot cart)
    {
        if (cart.ConditionBasisPoints >= 10_000) return "The selected cart is healthy; no repair is needed.";
        if (HasActiveContainerReservation(society.Checkpoint.Inventory, cart.Id))
            return "The selected cart or its cargo is reserved for another task.";
        if (person.Position != CartPosition(cart) && FindUnoccupiedRoute(actor, person.Position, CartPosition(cart), 0).Count == 0)
            return "Waiting for a clear route to the selected cart.";
        var missing = CartRepairKinds.Where(kind => !HasCarriedMaterial(actor, kind, 1)).ToArray();
        if (missing.Length > 0 && missing.Length > FreeCarryCapacity(actor))
            return "Make carrying space for the cart's repair supplies.";
        if (missing.Any(kind => SharedItem(kind, actor, CartPosition(cart)) is null))
            return "The cart repair needs one available wood, iron fitting and rope in your hands or permitted household or Town stock.";
        if (CartRepairCollectionPlan(actor, cart) is null)
            return "Waiting for an open route to collect the repair supplies and return to the cart.";
        return null;
    }

    private void ExecuteCartRepairOrderStep(OwnerQueuedInstruction instruction, AgentState person, InventoryLot cart)
    {
        var collection = CartRepairCollectionPlan(instruction.TargetAgentId, cart);
        if (collection is null) return;
        if (collection.Count > 0)
        {
            var input = collection[0];
            CollectEquipment(instruction.TargetAgentId, person,
                input.Kind, CartPosition(cart), plannedLotId: input.Stock!.Id);
            return;
        }
        var candidate = CartRepairOrderCandidateFor(instruction, cart);
        ApplyHandcartCandidate(instruction.TargetAgentId, person, candidate.Id);
        if (candidate.Id != RepairCartPrefix + cart.Id ||
            society.Checkpoint.Inventory.GetLot(cart.Id).ConditionBasisPoints <= cart.ConditionBasisPoints) return;
        // This receipt identifies this instruction's native repair, not a pre-existing healthy cart.
        CreditOrderEffect(instruction, Identity.Parse(CartRepairOrderReceipt(instruction, WorldTick)), 1);
    }

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
