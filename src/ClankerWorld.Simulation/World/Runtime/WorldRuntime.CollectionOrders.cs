using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private CognitionCandidate? CollectionOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (!ReadyForBriefInteraction(actor) || FreeCarryCapacity(actor) <= 0) return null;
        var lot = CollectionOrderGoods(instruction, GoodsUse.Recover)
            .OrderBy(item => map.FootDistance(person.Position, HouseholdStockPosition(item)))
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        return lot is null ? null : new(instruction.Task!.Action,
            $"Walk to your own stored or dropped {CollectionOrderGoodsName(instruction.Task)} and pick it up within your carrying limit.", 0, lot.Id);
    }

    private IEnumerable<InventoryLot> CollectionOrderGoods(OwnerQueuedInstruction instruction, GoodsUse use = GoodsUse.RecoveryHoldings) =>
        (use == GoodsUse.Recover ? RecoverablePersonalGoods(instruction.TargetAgentId)
            : PersonalGoodsAwaitingCollection(instruction.TargetAgentId)).Where(lot =>
            (instruction.Task!.Action switch
            {
                "collect_food" => IsEdibleFood(lot.ItemKind) &&
                    (instruction.Task.TargetFoodKind is null || lot.ItemKind == instruction.Task.TargetFoodKind),
                "collect_equipment" => lot.ItemKind == instruction.Task.TargetEquipmentKind,
                "collect_goods" => lot.ItemKind == instruction.Task.TargetItemKind,
                _ => lot.ItemKind == instruction.Task.TargetMaterialKind,
            }) &&
            (instruction.Task.TargetPosition is null || HouseholdStockPosition(lot) == instruction.Task.TargetPosition));

    private void ExecuteCollectionOrderStep(OwnerQueuedInstruction instruction, AgentState person)
    {
        var candidate = CollectionOrderCandidateFor(instruction, person);
        if (candidate?.DestinationId is not { } lotId)
        {
            SetOrderStatus(instruction, "blocked", CollectionOrderBlockedReason(instruction, person));
            return;
        }
        var order = instruction.Task!;
        var remaining = order.QuantityIsExplicit && !order.RepeatUntilCancelled
            ? order.RequestedUnits - order.CompletedUnits : int.MaxValue;
        if (CollectPersonalGoods(instruction.TargetAgentId, lotId, remaining) is { } effect)
        {
            var receipt = PersonalCollectionReceipt(effect.MoveId);
            CreditOrderEffect(instruction, receipt,
                order.ProgressUnit == "collection_loads" ? 1 : effect.Quantity);
        }
    }

    private static string CollectionOrderGoodsName(OrderTask order) =>
        order.Action switch
        {
            "collect_food" => "food",
            "collect_equipment" => "equipment",
            "collect_goods" => "goods",
            _ => "material",
        };

    private string CollectionOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var goods = CollectionOrderGoodsName(instruction.Task!);
        var pluralGoods = instruction.Task!.Action == "collect_material" ? "materials" : goods;
        if (!AgePermitsCandidate(actor, instruction.Task.Action))
            return $"This agent is too young to collect stored {pluralGoods}.";
        if (instruction.Task!.TargetPosition is { } target && !map.Contains(target))
            return "The requested tile is outside this world.";
        if (FreeCarryCapacity(actor) <= 0)
            return $"Carrying space is full; make room before collecting more {pluralGoods}.";
        if (!CollectionOrderGoods(instruction).Any())
            return instruction.Task.TargetPosition is not null
                ? $"No matching personal {goods} is available at the requested tile; goods must be your own and not reserved or promised for delivery."
                : $"No matching personal {goods} is available to collect; goods must be your own and not reserved or promised for delivery.";
        if (!CollectionOrderGoods(instruction).Any(lot => VesselFits(lot, FreeCarryCapacity(actor))))
            return "The vessel and all its contents must fit together; make more carrying space first.";
        if (!ReadyForBriefInteraction(actor))
            return $"The agent needs warmth before collecting {pluralGoods}.";
        return $"No open walking route reaches the agent's personal {goods} right now.";
    }
}
