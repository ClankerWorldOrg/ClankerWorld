using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static OrderTask? ParseCartOrder(ICartOrdersWorld world, string text, string actor)
    {
        if (!world.IsAdult(actor)) return null;
        text = text.Trim();
        if (text.StartsWith("please ", StringComparison.OrdinalIgnoreCase)) text = text[7..].TrimStart();
        foreach (var (verb, action) in new[] { ("attach", "attach_handcart"), ("pull", "attach_handcart"),
                     ("park", "park_handcart"), ("unhitch", "park_handcart"), ("repair", "repair_handcart") })
        {
            if (!text.StartsWith(verb + " ", StringComparison.OrdinalIgnoreCase)) continue;
            var subject = text[(verb.Length + 1)..].Trim();
            if (subject.StartsWith("my ", StringComparison.OrdinalIgnoreCase)) subject = subject[3..].TrimStart();
            foreach (var noun in new[] { "handcart", "cart" })
            {
                if (subject.Equals(noun, StringComparison.OrdinalIgnoreCase))
                    return OrderStore.CreateOrderTask((action) switch
                    {
                        "attach_handcart" => OrderStore.CreateAttachHandcartOrderDefinition(),
                        "park_handcart" => OrderStore.CreateParkHandcartOrderDefinition(),
                        "repair_handcart" => OrderStore.CreateRepairHandcartOrderDefinition(),
                        _ => throw new InvalidOperationException("The parser returned an unsupported order kind."),
                    }, "queued", 1, 0, "cart_tasks", false);
                if (!subject.StartsWith(noun + " ", StringComparison.OrdinalIgnoreCase)) continue;
                var id = subject[(noun.Length + 1)..].Trim();
                // Consume the whole target. Extra words and nonexistent IDs are not guessed away.
                if (IdentityRules.Reference(id, OwnerQueuedInstruction.MaximumIdentifierLength) && world.Society.Inventory.Lots.Any(lot => lot.Id == id && lot.ItemKind == InventoryContainerRules.Handcart))
                    return OrderStore.CreateOrderTask((action) switch
                    {
                        "attach_handcart" => OrderStore.CreateAttachHandcartOrderDefinition(TargetCartLotId: id),
                        "park_handcart" => OrderStore.CreateParkHandcartOrderDefinition(TargetCartLotId: id),
                        "repair_handcart" => OrderStore.CreateRepairHandcartOrderDefinition(TargetCartLotId: id),
                        _ => throw new InvalidOperationException("The parser returned an unsupported order kind."),
                    }, "queued", 1, 0, "cart_tasks", false);
                if (id.StartsWith("at ", StringComparison.OrdinalIgnoreCase) &&
                    OrderTaskParser.Parse("move to " + id[3..], world.Map.Resources, world.FoodKnowledgeKind) is
                    { Action: "move_to", TargetPosition: { } position })
                {
                    var carts = world.Society.Inventory.Lots.Where(lot => lot.ItemKind == InventoryContainerRules.Handcart &&
                        lot.OwnerId == actor && world.CartPosition(lot) == position).ToArray();
                    if (carts.Length == 1 && IdentityRules.Reference(carts[0].Id, OwnerQueuedInstruction.MaximumIdentifierLength))
                        return OrderStore.CreateOrderTask((action) switch
                        {
                            "attach_handcart" => OrderStore.CreateAttachHandcartOrderDefinition(TargetCartLotId: carts[0].Id),
                            "park_handcart" => OrderStore.CreateParkHandcartOrderDefinition(TargetCartLotId: carts[0].Id),
                            "repair_handcart" => OrderStore.CreateRepairHandcartOrderDefinition(TargetCartLotId: carts[0].Id),
                            _ => throw new InvalidOperationException("The parser returned an unsupported order kind."),
                        }, "queued", 1, 0, "cart_tasks", false);
                }
            }
        }
        return null;
    }


    internal static InventoryLot? CartForOrder(ICartOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        if (order.TargetCartLotId is { } target)
            return world.Society.Inventory.Lots.FirstOrDefault(lot => lot.Id == target && lot.ItemKind == InventoryContainerRules.Handcart);
        if (world.IsCartCargoOrder(order.Action)) return world.CartForCargoOrder(instruction, person);
        if (order.Action == "park_handcart") return world.AttachedHandcart(actor);
        if (order.Action == "repair_handcart") return world.CartForRepairOrder(actor, person);
        return world.Society.Inventory.Lots.Where(lot => lot.ItemKind == InventoryContainerRules.Handcart &&
                lot.OwnerId == actor && IdentityRules.Reference(lot.Id, OwnerQueuedInstruction.MaximumIdentifierLength) && world.CanPullHandcart(lot) && !world.HandcartHitches.Any(hitch => hitch.CartLotId == lot.Id))
            .OrderBy(lot => world.Map.FootDistance(person.Position, world.CartPosition(lot))).ThenBy(lot => lot.Id, StringComparer.Ordinal)
            .FirstOrDefault(lot => person.Position == world.CartPosition(lot) || world.Reach(actor, world.CartPosition(lot), ReachPurpose.HandcartControl, 0, 0, SwimmingPolicy.WhenCapable).Count > 0);
    }


    internal static CognitionCandidate? CartOrderCandidateFor(ICartOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        if (world.IsCartCargoOrder(instruction.Task!.Action)) return world.CartCargoOrderCandidateFor(instruction, person);
        if (world.CartOrderBlocker(instruction, person) is not null) return null;
        var cart = world.CartForOrder(instruction, person)!;
        if (instruction.Task!.Action == "repair_handcart") return world.CartRepairOrderCandidateFor(instruction, cart);
        return new(instruction.Task!.Action == "park_handcart" ? "park_handcart" : IdentityRules.PrefixedReference(world.AttachCartPrefix, cart.Id),
            instruction.Task.Action == "park_handcart" ? "Park the selected attached cart here, retaining its cargo." :
                "Reach and attach the selected owned cart through its normal physical action.", 0, cart.Id);
    }


    internal static string? CartOrderBlocker(ICartOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        if (world.IsCartCargoOrder(instruction.Task!.Action)) return world.CartCargoOrderBlocker(instruction, person);
        var actor = instruction.TargetAgentId;
        if (!world.IsAdult(actor)) return "Only an adult can pull, park or repair a handcart.";
        var cart = world.CartForOrder(instruction, person);
        if (cart is null) return "Waiting for an accessible owned cart suitable for this task.";
        if (cart.OwnerId != actor) return "You do not own the selected cart.";
        if (instruction.Task!.Action == "repair_handcart") return world.CartRepairOrderBlocker(actor, person, cart);
        if (instruction.Task!.Action == "park_handcart")
            return world.AttachedHandcart(actor)?.Id == cart.Id ? null : "The selected cart is not attached to you.";
        if (cart.ConditionBasisPoints == 0) return "The selected cart is broken; it needs repair before pulling.";
        if (!world.CanPullHandcart(cart)) return "The selected cart or its cargo is reserved for another task.";
        if (world.AnimalWorld.Animals.Any(animal => animal.RiderId == actor || animal.LeaderId == actor))
            return "Dismount or stop leading the animal before attaching a cart.";
        if (world.AttachedHandcart(actor) is not null) return "Park your attached cart before attaching another cart.";
        if (world.HandcartHitches.Any(hitch => hitch.CartLotId == cart.Id)) return "The selected cart is already attached.";
        return person.Position == world.CartPosition(cart) || world.Reach(actor, world.CartPosition(cart), ReachPurpose.HandcartControl, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? null : "Waiting for a clear route to the selected cart.";
    }


    internal static void ExecuteCartOrderStep(ICartOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        if (world.IsCartCargoOrder(instruction.Task!.Action))
        { world.ExecuteCartCargoOrderStep(instruction, person); return; }
        if (world.CartOrderBlocker(instruction, person) is { } blocker)
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, blocker);
            return;
        }
        var cart = world.CartForOrder(instruction, person)!;
        if (instruction.Task!.TargetCartLotId is null)
        {
            instruction = OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(world.Orders.Instructions[instruction.IdempotencyKey], world.Orders.Instructions[instruction.IdempotencyKey].Task!.UpdateDefinition(definition => definition switch
{
    AttachHandcartOrderDefinition task => OrderStore.PrepareExecuteCartOrderStepAttachHandcartOrderDefinition(task, cart.Id),
    ParkHandcartOrderDefinition task => OrderStore.PrepareExecuteCartOrderStepParkHandcartOrderDefinition(task, cart.Id),
    RepairHandcartOrderDefinition task => OrderStore.PrepareExecuteCartOrderStepRepairHandcartOrderDefinition(task, cart.Id),
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
})
);
            world.Orders.RecordCartOrderStep(instruction.IdempotencyKey, instruction);
        }
        if (instruction.Task!.Action == "repair_handcart")
        {
            world.ExecuteCartRepairOrderStep(instruction, person, cart);
            return;
        }
        var wasAttached = world.AttachedHandcart(instruction.TargetAgentId)?.Id == cart.Id;
        var parking = instruction.Task!.Action == "park_handcart";
        world.ApplyHandcartCandidate(instruction.TargetAgentId, person, parking ? "park_handcart" : IdentityRules.PrefixedReference(world.AttachCartPrefix, cart.Id));
        var isAttached = world.AttachedHandcart(instruction.TargetAgentId)?.Id == cart.Id;
        if (parking ? wasAttached && !isAttached : !wasAttached && isAttached)
            world.CreditOrderEffect(instruction, Identity.Parse(world.CartOrderEffectId(instruction)), 1);
    }

}
