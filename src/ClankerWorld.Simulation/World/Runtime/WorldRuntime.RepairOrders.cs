using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private CognitionCandidate? RepairOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (person.Equipment?.Repair is { } repair)
            return new("repair_equipment", repair.OrderInstructionId == instruction.InstructionId
                ? "Finish repairing the requested personal item with the reserved materials."
                : "End the previous repair and release its unused materials before starting this task.", 0, repair.LotId);
        var target = WornEquipmentItems(actor)
            .Where(lot => lot.ItemKind == instruction.Task!.TargetEquipmentKind)
            .FirstOrDefault(lot => CanPrepareEquipmentRepair(actor, person, lot));
        return target is null ? null : new("repair_equipment", "Collect the materials and repair the requested personal item at your household's work site.", 0, target.Id);
    }

    private void ExecuteRepairOrderStep(OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (person.Equipment?.Repair is { } repair)
        {
            if (repair.OrderInstructionId == instruction.InstructionId)
            {
                if (ContinueEquipmentRepair(actor) is { } effect)
                {
                    var receipt = EquipmentRepairReceipt(actor, effect.StartedTick, effect.LotId);
                    CreditOrderEffect(instruction, receipt, 1);
                }
                return;
            }
            CancelEquipmentRepair(actor);
            person = agents[actor];
        }
        if (RepairOrderCandidateFor(instruction, person)?.DestinationId is not { } lotId)
        {
            SetOrderStatus(instruction, "blocked", RepairOrderBlockedReason(instruction));
            return;
        }
        RepairEquipment(actor, person, lotId, instruction.InstructionId);
    }

    private string RepairOrderBlockedReason(OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        if (!MayFollowOwnerOrder(actor, instruction.Task!))
            return "This agent is too young to repair equipment.";
        var kind = instruction.Task!.TargetEquipmentKind!;
        var targets = WornEquipmentItems(actor).Where(lot => lot.ItemKind == kind).ToArray();
        if (targets.Length == 0)
            return "No matching worn personal item is available; carry an unreserved single item that needs repair.";
        if (targets.All(lot => EquipmentRepairSite(actor, lot) is null))
            return kind == "basket" ? "The agent needs their household's House to repair a basket." :
                "The agent needs their household's Tailor Shop to repair this item.";
        if (targets.All(lot => MissingRepairInputUnits(actor, lot) > FreeCarryCapacity(actor)))
            return "Make carrying space for the repair materials.";
        if (targets.All(lot => PersonalEquipmentRules.RepairMaterials(lot.ItemKind).Any(input =>
                !HasCarriedOwnItem(actor, input.ResourceId) && SharedItem(input.ResourceId, actor) is null)))
            return "The repair needs available materials in the agent's hands or household stock.";
        return "The agent and repair materials need an open route to the work site; make carrying space if the trip needs swimming.";
    }

    private void CancelRepairForOrder(OwnerQueuedInstruction instruction)
    {
        if (agents.TryGetValue(instruction.TargetAgentId, out var person) &&
            person.Equipment?.Repair?.OrderInstructionId == instruction.InstructionId)
            CancelEquipmentRepair(instruction.TargetAgentId);
    }

    private static void ValidateRepairOrderBindings(IEnumerable<AgentState> people,
        InventoryCheckpoint inventory, IEnumerable<OwnerQueuedInstruction> instructions)
    {
        foreach (var person in people)
        {
            if (person.Equipment?.Repair is not { OrderInstructionId: { } orderId } repair) continue;
            var instruction = instructions.Where(item => item.TargetAgentId == person.AgentId &&
                    item.Kind == OwnerInstructionKind.Order && item.Task is { } order && IsActiveOrder(order.Status))
                .OrderBy(item => item.SubmissionSequence).FirstOrDefault();
            var target = inventory.Lots.FirstOrDefault(lot => lot.Id == repair.LotId);
            if (target?.OwnerId != person.AgentId || instruction is null || instruction.InstructionId != orderId || instruction.Task is not { Action: "repair_equipment" } task ||
                task.Status == "queued" || repair.StartedTick < instruction.SubmittedTick ||
                target.ItemKind != task.TargetEquipmentKind)
                throw new InvalidDataException("The saved equipment repair does not belong to its active order.");
        }
    }
}
