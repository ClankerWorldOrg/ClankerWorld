using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static CognitionCandidate? CollectionOrderCandidateFor(ICollectionOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (!world.ReadyForBriefInteraction(actor) || world.FreeCarryCapacity(actor) <= 0) return null;
        var lot = world.CollectionOrderGoods(instruction, GoodsUse.Recover)
            .OrderBy(item => world.Map.FootDistance(person.Position, world.HouseholdStockPosition(item)))
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        return lot is null ? null : new(instruction.Task!.Action,
            $"Walk to your own stored or dropped {world.CollectionOrderGoodsName(instruction.Task)} and pick it up within your carrying limit.", 0, lot.Id);
    }


    internal static IEnumerable<InventoryLot> CollectionOrderGoods(ICollectionOrdersWorld world, OwnerQueuedInstruction instruction, GoodsUse use = GoodsUse.RecoveryHoldings) => (use == GoodsUse.Recover ? world.RecoverablePersonalGoods(instruction.TargetAgentId)
            : world.PersonalGoodsAwaitingCollection(instruction.TargetAgentId)).Where(lot =>
            (instruction.Task!.Action switch
            {
                "collect_food" => world.IsEdibleFood(lot.ItemKind) &&
                    (instruction.Task.TargetFoodKind is null || lot.ItemKind == instruction.Task.TargetFoodKind),
                "collect_equipment" => lot.ItemKind == instruction.Task.TargetEquipmentKind,
                "collect_goods" => lot.ItemKind == instruction.Task.TargetItemKind,
                _ => lot.ItemKind == instruction.Task.TargetMaterialKind,
            }) &&
            (instruction.Task.TargetPosition is null || world.HouseholdStockPosition(lot) == instruction.Task.TargetPosition));

    internal static void ExecuteCollectionOrderStep(ICollectionOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var candidate = world.CollectionOrderCandidateFor(instruction, person);
        if (candidate?.DestinationId is not { } lotId)
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.CollectionOrderBlockedReason(instruction, person));
            return;
        }
        var order = instruction.Task!;
        var remaining = order.QuantityIsExplicit && !order.RepeatUntilCancelled
            ? order.RequestedUnits - order.CompletedUnits : int.MaxValue;
        if (world.CollectPersonalGoods(instruction.TargetAgentId, lotId, remaining) is { } effect)
        {
            var receipt = world.PersonalCollectionReceipt(effect.MoveId);
            world.CreditOrderEffect(instruction, Identity.Parse(receipt),
                order.ProgressUnit == "collection_loads" ? 1 : effect.Quantity);
        }
    }


    internal static string CollectionOrderBlockedReason(ICollectionOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var goods = world.CollectionOrderGoodsName(instruction.Task!);
        var pluralGoods = instruction.Task!.Action == "collect_material" ? "materials" : goods;
        if (!world.MayFollowOwnerOrder(actor, instruction.Task!))
            return $"This agent is too young to collect stored {pluralGoods}.";
        if (instruction.Task!.TargetPosition is { } target && !world.Map.Contains(target))
            return "The requested tile is outside this world.";
        if (world.FreeCarryCapacity(actor) <= 0)
            return $"Carrying space is full; make room before collecting more {pluralGoods}.";
        if (!world.CollectionOrderGoods(instruction).Any())
            return instruction.Task.TargetPosition is not null
                ? $"No matching personal {goods} is available at the requested tile; goods must be your own and not reserved or promised for delivery."
                : $"No matching personal {goods} is available to collect; goods must be your own and not reserved or promised for delivery.";
        if (!world.CollectionOrderGoods(instruction).Any(lot => world.VesselFits(lot, world.FreeCarryCapacity(actor))))
            return "The vessel and all its contents must fit together; make more carrying space first.";
        if (!world.ReadyForBriefInteraction(actor))
            return $"The agent needs warmth before collecting {pluralGoods}.";
        return $"No open walking route reaches the agent's personal {goods} right now.";
    }

}
