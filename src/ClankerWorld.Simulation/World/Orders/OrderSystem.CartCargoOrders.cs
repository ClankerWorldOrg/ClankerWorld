using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static OrderTask? ParseCartCargoOrder(ICartCargoOrdersWorld world, string text, string actor)
    {
        if (!world.IsAdult(actor)) return null;
        text = text.Trim();
        if (text.StartsWith("please ", StringComparison.OrdinalIgnoreCase)) text = text[7..].TrimStart();
        var loading = text.StartsWith("load ", StringComparison.OrdinalIgnoreCase);
        if (!loading && !text.StartsWith("unload ", StringComparison.OrdinalIgnoreCase)) return null;
        text = text[(loading ? 5 : 7)..];
        var separator = loading ? " into " : " from ";
        var split = text.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
        if (split < 0) return null;
        var goods = OrderTaskParser.Parse("collect " + text[..split], world.Map.Resources, world.FoodKnowledgeKind);
        if (goods is null || goods.Action is not ("collect_material" or "collect_equipment" or "collect_food" or "collect_goods") ||
            !goods.QuantityIsExplicit || goods.RepeatUntilCancelled || goods.TargetPosition is not null) return null;
        var kind = goods.TargetItemKind ?? goods.TargetMaterialKind ?? goods.TargetEquipmentKind ?? goods.TargetFoodKind;
        if (!world.IsCartCargoKind(kind)) return null;
        var target = text[(split + separator.Length)..].Trim();
        var ontoGround = !loading && target.EndsWith(" onto the ground", StringComparison.OrdinalIgnoreCase);
        if (ontoGround) target = target[..^" onto the ground".Length].TrimEnd();
        var cart = world.ParseCartOrder("attach " + target, actor);
        if (cart is null) return null;
        return OrderStore.CreateOrderTask((loading ? "load_handcart" : ontoGround ? "unload_handcart_ground" : "unload_handcart") switch
        {
            "load_handcart" => OrderStore.CreateLoadHandcartOrderDefinition(TargetItemKind: kind, TargetCartLotId: cart.TargetCartLotId),
            "unload_handcart" => OrderStore.CreateUnloadHandcartOrderDefinition(TargetItemKind: kind, TargetCartLotId: cart.TargetCartLotId),
            "unload_handcart_ground" => OrderStore.CreateUnloadHandcartGroundOrderDefinition(TargetItemKind: kind, TargetCartLotId: cart.TargetCartLotId),
            _ => throw new InvalidOperationException("The parser returned an unsupported order kind."),
        }, "queued", goods.RequestedUnits, 0, "goods_items", false, true);
    }


    internal static InventoryLot? CartForCargoOrder(ICartCargoOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        return world.Society.Inventory.Lots.Where(lot => lot.ItemKind == InventoryContainerRules.Handcart &&
                lot.OwnerId == actor && IdentityRules.Reference(lot.Id, OwnerQueuedInstruction.MaximumIdentifierLength) &&
                !world.HasActiveContainerReservation(world.Society.Inventory, lot.Id) &&
                (order.Action == "load_handcart" ? world.CanPullHandcart(lot) && world.CargoQuantity(lot.Id) < InventoryContainerRules.HandcartCapacity :
                    world.CartCargoLots(actor, lot.Id).Any(cargo => cargo.ItemKind == order.TargetItemKind)))
            .OrderBy(lot => world.Map.FootDistance(person.Position, world.CartPosition(lot))).ThenBy(lot => lot.Id, StringComparer.Ordinal)
            .FirstOrDefault(lot => person.Position == world.CartPosition(lot) || world.Reach(actor, world.CartPosition(lot), ReachPurpose.HandcartPull, 0, 0, SwimmingPolicy.WhenCapable).Count > 0);
    }


    internal static InventoryLot? CargoLotForOrder(ICartCargoOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person, InventoryLot cart)
    {
        var order = instruction.Task!;
        var inventory = world.Society.Inventory;
        if (order.TargetLotId is { } bound) return inventory.Lots.FirstOrDefault(lot => lot.Id == bound);
        var lots = order.Action == "load_handcart" ? inventory.Lots.Where(lot =>
            world.CanLoadCartLot(instruction.TargetAgentId, person, lot)) : world.CartCargoLots(instruction.TargetAgentId, cart.Id);
        return lots.Where(lot => lot.ItemKind == order.TargetItemKind)
            .OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();
    }


    internal static string? CartCargoOrderBlocker(ICartCargoOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        if (!world.IsAdult(actor)) return "Only an adult can load or unload a handcart.";
        var cart = world.CartForOrder(instruction, person);
        if (cart is null) return "Waiting for an accessible owned cart suitable for this task.";
        if (cart.OwnerId != actor) return "You do not own the selected cart.";
        if (world.HasActiveContainerReservation(world.Society.Inventory, cart.Id)) return "The selected cart or its cargo is reserved for another task.";
        if (order.Action == "load_handcart" && cart.ConditionBasisPoints == 0) return "The selected cart is broken; it needs repair before loading.";
        if (person.Position != world.CartPosition(cart))
            return world.Reach(actor, world.CartPosition(cart), ReachPurpose.HandcartPull, 0, 0, SwimmingPolicy.WhenCapable).Count > 0 ? null : "Waiting for a clear route to the selected cart.";
        if (order.Action == "load_handcart" && world.CargoQuantity(cart.Id) >= InventoryContainerRules.HandcartCapacity)
            return "Waiting for space in the selected cart.";
        if (order.Action == "unload_handcart" && world.FreeCarryCapacity(actor) <= 0) return "Waiting for personal carrying space to unload the selected cart.";
        var cargo = world.CargoLotForOrder(instruction, person, cart);
        if (cargo is null || cargo.ItemKind != order.TargetItemKind ||
            (order.Action == "load_handcart" ? !world.CanLoadCartLot(actor, person, cargo) : cargo.OwnerId != actor || cargo.ContainerLotId != cart.Id))
            return "Waiting for the requested loose goods at the cart; they must be reachable, allowed and not reserved.";
        return null;
    }


    internal static CognitionCandidate? CartCargoOrderCandidateFor(ICartCargoOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person) => world.CartCargoOrderBlocker(instruction, person) is null
            ? new(instruction.Task!.Action, "Reach the selected cart and move only the requested authorized loose goods through its native inventory action.", 0,
                world.CartForOrder(instruction, person)!.Id) : null;

    internal static int CargoQuantity(ICartCargoOrdersWorld world, string cartId) => world.ContainerContentsQuantity(world.Society.Inventory, cartId);

    internal static void ExecuteCartCargoOrderStep(ICartCargoOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        if (world.CartCargoOrderBlocker(instruction, person) is { } blocker)
        { world.SetOrderStatus(instruction, OrderStatus.Blocked, blocker); return; }
        var cart = world.CartForOrder(instruction, person)!;
        var order = world.Orders.Instructions[instruction.IdempotencyKey].Task!.UpdateDefinition(definition => definition switch
{
    LoadHandcartOrderDefinition task => OrderStore.PrepareExecuteCartCargoOrderStepLoadHandcartOrderDefinition(task, cart.Id),
    UnloadHandcartOrderDefinition task => OrderStore.PrepareExecuteCartCargoOrderStepUnloadHandcartOrderDefinition(task, cart.Id),
    UnloadHandcartGroundOrderDefinition task => OrderStore.PrepareExecuteCartCargoOrderStepUnloadHandcartGroundOrderDefinition(task, cart.Id),
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
});
        instruction = OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(world.Orders.Instructions[instruction.IdempotencyKey], order);
        world.Orders.RecordCartCargoOrderStep(instruction.IdempotencyKey, instruction);
        if (person.Position != world.CartPosition(cart))
        { world.MoveToward(instruction.TargetAgentId, person, world.CartPosition(cart), "cart_cargo_order"); return; }
        var cargo = world.CargoLotForOrder(instruction, person, cart)!;
        order = order.UpdateDefinition(definition => definition switch
{
    LoadHandcartOrderDefinition task => OrderStore.PrepareExecuteCartCargoOrderStepLoadHandcartOrderDefinitionTargetLotId(task, cargo.Id),
    UnloadHandcartOrderDefinition task => OrderStore.PrepareExecuteCartCargoOrderStepUnloadHandcartOrderDefinitionTargetLotId(task, cargo.Id),
    UnloadHandcartGroundOrderDefinition task => OrderStore.PrepareExecuteCartCargoOrderStepUnloadHandcartGroundOrderDefinitionTargetLotId(task, cargo.Id),
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
});
        instruction = OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(instruction, order);
        world.Orders.RecordCartCargoOrderStep(instruction.IdempotencyKey, instruction);
        var before = world.CargoQuantity(cart.Id);
        var loading = order.Action == "load_handcart";
        var prefix = loading ? world.LoadCartPrefix : order.Action == "unload_handcart_ground" ? world.UnloadCartGroundPrefix : world.UnloadCartPrefix;
        world.ApplyHandcartCandidate(instruction.TargetAgentId, person, IdentityRules.PrefixedReference(prefix, cargo.Id), cart.Id, order.RequestedUnits - order.CompletedUnits);
        var moved = loading ? world.CargoQuantity(cart.Id) - before : before - world.CargoQuantity(cart.Id);
        if (moved <= 0) return;
        world.CreditOrderEffect(instruction, Identity.Parse(world.CartCargoOrderEffectId(instruction, order.CompletedUnits + moved)), moved);
        // A fully moved stack is no longer a source. The next step can bind another eligible
        // stack of this same good; a partially moved or newly blocked source stays bound.
        var updated = world.Orders.Instructions[instruction.IdempotencyKey];
        var remainder = world.Society.Inventory.Lots.FirstOrDefault(lot => lot.Id == cargo.Id);
        if (updated.Task!.CompletedUnits < order.RequestedUnits &&
            (remainder is null || (loading ? remainder.ContainerLotId == cart.Id : remainder.ContainerLotId != cart.Id)))
            world.Orders.RecordCartCargoOrderStep(instruction.IdempotencyKey, OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(updated, updated.Task.UpdateDefinition(definition => definition switch
{
    LoadHandcartOrderDefinition task => OrderStore.PrepareExecuteCartCargoOrderStepLoadHandcartOrderDefinitionTargetLotId(task, null),
    UnloadHandcartOrderDefinition task => OrderStore.PrepareExecuteCartCargoOrderStepUnloadHandcartOrderDefinitionTargetLotId(task, null),
    UnloadHandcartGroundOrderDefinition task => OrderStore.PrepareExecuteCartCargoOrderStepUnloadHandcartGroundOrderDefinitionTargetLotId(task, null),
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
})
));
    }

}
