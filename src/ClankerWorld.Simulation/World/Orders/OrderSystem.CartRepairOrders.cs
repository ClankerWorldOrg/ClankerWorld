using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using InputCollectionStep = ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static InventoryLot? CartForRepairOrder(ICartRepairOrdersWorld world, string actor, AgentState person) => world.Society.Inventory.Lots.Where(lot => lot.ItemKind == InventoryContainerRules.Handcart &&
                lot.OwnerId == actor && lot.ConditionBasisPoints < 10_000 && IdentityRules.Reference(lot.Id, OwnerQueuedInstruction.MaximumIdentifierLength) &&
                !world.HasActiveContainerReservation(world.Society.Inventory, lot.Id))
            .OrderBy(lot => world.Map.FootDistance(person.Position, world.CartPosition(lot))).ThenBy(lot => lot.Id, StringComparer.Ordinal)
            .FirstOrDefault(lot => person.Position == world.CartPosition(lot) || world.Reach(actor, world.CartPosition(lot), ReachPurpose.HandcartRepair, 0, 0, SwimmingPolicy.WhenCapable).Count > 0);

    internal static CognitionCandidate CartRepairOrderCandidateFor(ICartRepairOrdersWorld world, OwnerQueuedInstruction instruction, InventoryLot cart)
    {
        var actor = instruction.TargetAgentId;
        var collection = world.CartRepairCollectionPlan(actor, cart);
        var missing = collection is { Count: > 0 } ? collection[0].Kind :
            world.CartRepairKinds.FirstOrDefault(kind => !world.HasCarriedMaterial(actor, kind, 1));
        return missing is null
            ? new(IdentityRules.PrefixedReference(world.RepairCartPrefix, cart.Id), "Reach and repair the selected cart with your carried wood, iron fitting and rope.", 0, cart.Id)
            : new(IdentityRules.PrefixedReference(world.CollectCartRepairPrefix, missing), "Collect the permitted repair supplies for the selected cart.", 0, cart.Id);
    }


    internal static List<InputCollectionStep>? CartRepairCollectionPlan(ICartRepairOrdersWorld world, string actor, InventoryLot cart)
    {
        var inputs = new List<InputCollectionStep>();
        foreach (var kind in world.CartRepairKinds)
        {
            if (world.HasCarriedMaterial(actor, kind, 1)) continue;
            if (world.SharedItem(kind, actor, world.CartPosition(cart)) is not { } stock) return null;
            inputs.Add(world.StockCollectionStep(stock, 1));
        }
        return world.PlanInputCollection(actor, inputs, world.CartPosition(cart));
    }


    internal static string? CartRepairOrderBlocker(ICartRepairOrdersWorld world, string actor, AgentState person, InventoryLot cart)
    {
        if (cart.ConditionBasisPoints >= 10_000) return "The selected cart is healthy; no repair is needed.";
        if (world.HasActiveContainerReservation(world.Society.Inventory, cart.Id))
            return "The selected cart or its cargo is reserved for another task.";
        if (person.Position != world.CartPosition(cart) && world.Reach(actor, world.CartPosition(cart), ReachPurpose.HandcartRepair, 0, 0, SwimmingPolicy.WhenCapable).Count == 0)
            return "Waiting for a clear route to the selected cart.";
        var missing = world.CartRepairKinds.Where(kind => !world.HasCarriedMaterial(actor, kind, 1)).ToArray();
        if (missing.Length != 0 && missing.Length > world.FreeCarryCapacity(actor))
            return "Make carrying space for the cart's repair supplies.";
        if (missing.Any(kind => world.SharedItem(kind, actor, world.CartPosition(cart)) is null))
            return "The cart repair needs one available wood, iron fitting and rope in your hands or permitted household or Town stock.";
        if (world.CartRepairCollectionPlan(actor, cart) is null)
            return "Waiting for an open route to collect the repair supplies and return to the cart.";
        return null;
    }


    internal static void ExecuteCartRepairOrderStep(ICartRepairOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person, InventoryLot cart)
    {
        var collection = world.CartRepairCollectionPlan(instruction.TargetAgentId, cart);
        if (collection is null) return;
        if (collection.Count > 0)
        {
            var input = collection[0];
            world.CollectEquipment(instruction.TargetAgentId, person,
                input.Kind, world.CartPosition(cart), plannedLotId: input.Stock!.Id);
            return;
        }
        var candidate = world.CartRepairOrderCandidateFor(instruction, cart);
        world.ApplyHandcartCandidate(instruction.TargetAgentId, person, candidate.Id);
        if (candidate.Id != IdentityRules.PrefixedReference(world.RepairCartPrefix, cart.Id) ||
            world.Society.Inventory.GetLot(cart.Id).ConditionBasisPoints <= cart.ConditionBasisPoints) return;
        // This receipt identifies this instruction's native repair, not a pre-existing healthy cart.
        world.CreditOrderEffect(instruction, Identity.Parse(world.CartRepairOrderReceipt(instruction, world.WorldTick)), 1);
    }

}
