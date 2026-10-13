using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private CognitionCandidate? ToolRepairOrderCandidateFor(OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        var tool = RepairableTools(actor, ToolRepairOrderTargets(instruction)).FirstOrDefault(lot =>
            lot.OwnerId == actor && lot.ConditionBasisPoints > 0 && lot.ItemKind == instruction.Task!.TargetEquipmentKind);
        return tool is null ? null : new("repair_tool",
            "Collect the materials and repair the requested personal tool at your household's Blacksmith.", 0, tool.Id);
    }

    private void ExecuteToolRepairOrderStep(OwnerQueuedInstruction instruction, AgentState person)
    {
        if (ToolRepairOrderCandidateFor(instruction)?.DestinationId is not { } lotId)
        {
            SetOrderStatus(instruction, OrderStatus.Blocked, ToolRepairOrderBlockedReason(instruction));
            return;
        }
        var actor = instruction.TargetAgentId;
        if (RepairTool(actor, person, lotId, ToolRepairOrderTargets(instruction)) is not { } repairedLotId) return;
        var receipt = ToolRepairReceipt(actor, WorldTick, repairedLotId);
        CreditOrderEffect(instruction, Identity.Parse(receipt), 1);
    }

    private string ToolRepairOrderBlockedReason(OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        if (!IsAdult(actor)) return "This agent is too young to repair tools.";
        var inventory = society.Checkpoint.Inventory;
        if (!inventory.Lots.Any(lot => lot.OwnerId == actor &&
                lot.ItemKind == instruction.Task!.TargetEquipmentKind &&
                ToolProgressionRules.IsTopLevelCarriedLot(lot, actor) &&
                lot.ConditionBasisPoints is > 0 and < 10_000 && UnreservedQuantity(inventory, lot) > 0))
            return "Carry a matching worn personal tool that is available for repair.";
        var household = society.Checkpoint.GetAgent(actor).HouseholdId;
        if (household is null || HouseholdBuildingWithTag(household, "blacksmith") is null)
            return "The agent needs their household's Blacksmith to repair this tool.";
        return "The repair needs accessible materials, carrying space, a gathering tool that will not break the requested tools, and an open route to the household Blacksmith.";
    }

    private string[] ToolRepairOrderTargets(OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        var inventory = society.Checkpoint.Inventory;
        return inventory.Lots.Where(lot => lot.OwnerId == actor &&
                lot.ItemKind == instruction.Task!.TargetEquipmentKind &&
                ToolProgressionRules.IsTopLevelCarriedLot(lot, actor) &&
                lot.ConditionBasisPoints is > 0 and < 10_000 && UnreservedQuantity(inventory, lot) > 0)
            .Select(lot => lot.Id).ToArray();
    }
}
