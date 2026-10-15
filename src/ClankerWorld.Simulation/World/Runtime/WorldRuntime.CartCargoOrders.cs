using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static bool IsCartCargoOrder(string action) => action is "load_handcart" or "unload_handcart" or "unload_handcart_ground";

    private static bool IsCartCargoKind(string? kind) => kind is not null &&
        (WorldCustodyOrderCatalog.IsReturnKind(kind) || IsEdibleFood(kind)) &&
        InventoryContainerRules.Allows(InventoryContainerRules.Handcart, kind);

    private OrderTask? ParseCartCargoOrder(string text, string actor)
    {
        if (!IsAdult(actor)) return null;
        text = text.Trim();
        if (text.StartsWith("please ", StringComparison.OrdinalIgnoreCase)) text = text[7..].TrimStart();
        var loading = text.StartsWith("load ", StringComparison.OrdinalIgnoreCase);
        if (!loading && !text.StartsWith("unload ", StringComparison.OrdinalIgnoreCase)) return null;
        text = text[(loading ? 5 : 7)..];
        var separator = loading ? " into " : " from ";
        var split = text.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
        if (split < 0) return null;
        var goods = OrderTaskParser.Parse("collect " + text[..split], map.Resources, FoodKnowledgeKind);
        if (goods is null || goods.Action is not ("collect_material" or "collect_equipment" or "collect_food" or "collect_goods") ||
            !goods.QuantityIsExplicit || goods.RepeatUntilCancelled || goods.TargetPosition is not null) return null;
        var kind = goods.TargetItemKind ?? goods.TargetMaterialKind ?? goods.TargetEquipmentKind ?? goods.TargetFoodKind;
        if (!IsCartCargoKind(kind)) return null;
        var target = text[(split + separator.Length)..].Trim();
        var ontoGround = !loading && target.EndsWith(" onto the ground", StringComparison.OrdinalIgnoreCase);
        if (ontoGround) target = target[..^" onto the ground".Length].TrimEnd();
        var cart = ParseCartOrder("attach " + target, actor);
        if (cart is null) return null;
        return new(loading ? "load_handcart" : ontoGround ? "unload_handcart_ground" : "unload_handcart",
            "queued", goods.RequestedUnits, 0, "goods_items", false, QuantityIsExplicit: true, TargetItemKind: kind)
        { TargetCartLotId = cart.TargetCartLotId };
    }

    private InventoryLot? CartForCargoOrder(OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        return society.Checkpoint.Inventory.Lots.Where(lot => lot.ItemKind == InventoryContainerRules.Handcart &&
                lot.OwnerId == actor && IsValidInstructionIdentifier(lot.Id) &&
                !HasActiveContainerReservation(society.Checkpoint.Inventory, lot.Id) &&
                (order.Action == "load_handcart" ? CanPullHandcart(lot) && CargoQuantity(lot.Id) < InventoryContainerRules.HandcartCapacity :
                    CartCargoLots(actor, lot.Id).Any(cargo => cargo.ItemKind == order.TargetItemKind)))
            .OrderBy(lot => map.FootDistance(person.Position, CartPosition(lot))).ThenBy(lot => lot.Id, StringComparer.Ordinal)
            .FirstOrDefault(lot => person.Position == CartPosition(lot) || Reach(actor, CartPosition(lot), ReachPurpose.HandcartPull, 0, 0, SwimmingPolicy.WhenCapable).Count > 0);
    }

    private InventoryLot? CargoLotForOrder(OwnerQueuedInstruction instruction, AgentState person, InventoryLot cart)
    {
        var order = instruction.Task!;
        var inventory = society.Checkpoint.Inventory;
        if (order.TargetLotId is { } bound) return inventory.Lots.FirstOrDefault(lot => lot.Id == bound);
        var lots = order.Action == "load_handcart" ? inventory.Lots.Where(lot =>
            CanLoadCartLot(instruction.TargetAgentId, person, lot)) : CartCargoLots(instruction.TargetAgentId, cart.Id);
        return lots.Where(lot => lot.ItemKind == order.TargetItemKind)
            .OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();
    }

    private string? CartCargoOrderBlocker(OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        if (!IsAdult(actor)) return "Only an adult can load or unload a handcart.";
        var cart = CartForOrder(instruction, person);
        if (cart is null) return "Waiting for an accessible owned cart suitable for this task.";
        if (cart.OwnerId != actor) return "You do not own the selected cart.";
        if (HasActiveContainerReservation(society.Checkpoint.Inventory, cart.Id)) return "The selected cart or its cargo is reserved for another task.";
        if (order.Action == "load_handcart" && cart.ConditionBasisPoints == 0) return "The selected cart is broken; it needs repair before loading.";
        if (person.Position != CartPosition(cart))
            return Reach(actor, CartPosition(cart), ReachPurpose.HandcartPull, 0, 0, SwimmingPolicy.WhenCapable).Count > 0 ? null : "Waiting for a clear route to the selected cart.";
        if (order.Action == "load_handcart" && CargoQuantity(cart.Id) >= InventoryContainerRules.HandcartCapacity)
            return "Waiting for space in the selected cart.";
        if (order.Action == "unload_handcart" && FreeCarryCapacity(actor) <= 0) return "Waiting for personal carrying space to unload the selected cart.";
        var cargo = CargoLotForOrder(instruction, person, cart);
        if (cargo is null || cargo.ItemKind != order.TargetItemKind ||
            (order.Action == "load_handcart" ? !CanLoadCartLot(actor, person, cargo) : cargo.OwnerId != actor || cargo.ContainerLotId != cart.Id))
            return "Waiting for the requested loose goods at the cart; they must be reachable, allowed and not reserved.";
        return null;
    }

    private CognitionCandidate? CartCargoOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        CartCargoOrderBlocker(instruction, person) is null
            ? new(instruction.Task!.Action, "Reach the selected cart and move only the requested authorized loose goods through its native inventory action.", 0,
                CartForOrder(instruction, person)!.Id) : null;

    private int CargoQuantity(string cartId) => ContainerContentsQuantity(society.Checkpoint.Inventory, cartId);

    private void ExecuteCartCargoOrderStep(OwnerQueuedInstruction instruction, AgentState person)
    {
        if (CartCargoOrderBlocker(instruction, person) is { } blocker)
        { SetOrderStatus(instruction, "blocked", blocker); return; }
        var cart = CartForOrder(instruction, person)!;
        var order = instructionsByIdempotency[instruction.IdempotencyKey].Task! with { TargetCartLotId = cart.Id };
        instruction = instructionsByIdempotency[instruction.IdempotencyKey] with { Task = order };
        instructionsByIdempotency[instruction.IdempotencyKey] = instruction;
        if (person.Position != CartPosition(cart))
        { MoveToward(instruction.TargetAgentId, person, CartPosition(cart), "cart_cargo_order"); return; }
        var cargo = CargoLotForOrder(instruction, person, cart)!;
        order = order with { TargetLotId = cargo.Id };
        instruction = instruction with { Task = order };
        instructionsByIdempotency[instruction.IdempotencyKey] = instruction;
        var before = CargoQuantity(cart.Id);
        var loading = order.Action == "load_handcart";
        var prefix = loading ? LoadCartPrefix : order.Action == "unload_handcart_ground" ? UnloadCartGroundPrefix : UnloadCartPrefix;
        ApplyHandcartCandidate(instruction.TargetAgentId, person, prefix + cargo.Id, cart.Id, order.RequestedUnits - order.CompletedUnits);
        var moved = loading ? CargoQuantity(cart.Id) - before : before - CargoQuantity(cart.Id);
        if (moved <= 0) return;
        CreditOrderEffect(instruction, CartCargoOrderEffectId(instruction, order.CompletedUnits + moved), moved);
        // A fully moved stack is no longer a source. The next step can bind another eligible
        // stack of this same good; a partially moved or newly blocked source stays bound.
        var updated = instructionsByIdempotency[instruction.IdempotencyKey];
        var remainder = society.Checkpoint.Inventory.Lots.FirstOrDefault(lot => lot.Id == cargo.Id);
        if (updated.Task!.CompletedUnits < order.RequestedUnits &&
            (remainder is null || (loading ? remainder.ContainerLotId == cart.Id : remainder.ContainerLotId != cart.Id)))
            instructionsByIdempotency[instruction.IdempotencyKey] = updated with { Task = updated.Task with { TargetLotId = null } };
    }

    private static string CartCargoOrderEffectId(OwnerQueuedInstruction instruction, int completed) =>
        DerivedId.Hash(
            $"{instruction.InstructionId}|{instruction.TargetAgentId}|{instruction.Task!.Action}|{instruction.Task.TargetCartLotId}|{instruction.Task.TargetItemKind}|{completed}", "cart-cargo-order:");

    private static bool IsValidCartCargoOrder(OwnerQueuedInstruction instruction)
    {
        var order = instruction.Task!;
        return IsCartCargoKind(order.TargetItemKind) &&
            (order.TargetCartLotId is null || IsValidInstructionIdentifier(order.TargetCartLotId) && order.TargetCartLotId == order.TargetCartLotId.Trim()) &&
            (order.TargetLotId is null || order.TargetCartLotId is not null) &&
            order.RequestedUnits is >= 1 and <= 1000 && order.CompletedUnits >= 0 && order.CompletedUnits <= order.RequestedUnits &&
            !order.RepeatUntilCancelled && order.QuantityIsExplicit && order.ProgressUnit == "goods_items" &&
            order.TargetFoodKind is null && order.TargetAgentId is null && order.TargetResourceId is null && order.TargetPosition is null &&
            order.Status != "not_understood" && (order.Status == "finished") == (order.CompletedUnits == order.RequestedUnits) &&
            (order.CompletedUnits == 0 ? order.LastEffectId is null :
                order.TargetCartLotId is not null && order.LastEffectId == CartCargoOrderEffectId(instruction, order.CompletedUnits));
    }
}
