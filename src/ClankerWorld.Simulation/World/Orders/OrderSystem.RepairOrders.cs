using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static CognitionCandidate? RepairOrderCandidateFor(IRepairOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (person.Equipment?.Repair is { } repair)
            return new("repair_equipment", repair.OrderInstructionId == instruction.InstructionId
                ? "Finish repairing the requested personal item with the reserved materials."
                : "End the previous repair and release its unused materials before starting this task.", 0, repair.LotId);
        var target = world.WornEquipmentItems(actor)
            .Where(lot => lot.ItemKind == instruction.Task!.TargetEquipmentKind)
            .FirstOrDefault(lot => world.CanPrepareEquipmentRepair(actor, person, lot));
        return target is null ? null : new("repair_equipment", "Collect the materials and repair the requested personal item at your household's work site.", 0, target.Id);
    }


    internal static void ExecuteRepairOrderStep(IRepairOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (person.Equipment?.Repair is { } repair)
        {
            if (repair.OrderInstructionId == instruction.InstructionId)
            {
                if (world.ContinueEquipmentRepair(actor) is { } effect)
                {
                    var receipt = world.EquipmentRepairReceipt(actor, effect.StartedTick, effect.LotId);
                    world.CreditOrderEffect(instruction, Identity.Parse(receipt), 1);
                }
                return;
            }
            world.CancelEquipmentRepair(actor);
            person = world.Agents[actor];
        }
        if (world.RepairOrderCandidateFor(instruction, person)?.DestinationId is not { } lotId)
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.RepairOrderBlockedReason(instruction));
            return;
        }
        world.RepairEquipment(actor, person, lotId, instruction.InstructionId);
    }


    internal static string RepairOrderBlockedReason(IRepairOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        if (!world.MayFollowOwnerOrder(actor, instruction.Task!))
            return "This agent is too young to repair equipment.";
        var kind = instruction.Task!.TargetEquipmentKind!;
        var targets = world.WornEquipmentItems(actor).Where(lot => lot.ItemKind == kind).ToArray();
        if (targets.Length == 0)
            return "No matching worn personal item is available; carry an unreserved single item that needs repair.";
        if (targets.All(lot => world.EquipmentRepairSite(actor, lot) is null))
            return kind == "basket" ? "The agent needs their household's House to repair a basket." :
                "The agent needs their household's Tailor Shop to repair this item.";
        if (targets.All(lot => world.MissingRepairInputUnits(actor, lot) > world.FreeCarryCapacity(actor)))
            return "Make carrying space for the repair materials.";
        if (targets.All(lot => PersonalEquipmentRules.RepairMaterials(lot.ItemKind).Any(input =>
                !world.HasCarriedOwnItem(actor, input.ResourceId) && world.SharedItem(input.ResourceId, actor) is null)))
            return "The repair needs available materials in the agent's hands or household stock.";
        return "The agent and repair materials need an open route to the work site; make carrying space if the trip needs swimming.";
    }


    internal static void CancelRepairForOrder(IRepairOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        if (world.Agents.TryGetValue(instruction.TargetAgentId, out var person) &&
            person.Equipment?.Repair?.OrderInstructionId == instruction.InstructionId)
            world.CancelEquipmentRepair(instruction.TargetAgentId);
    }

}
