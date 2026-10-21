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
    internal static CognitionCandidate? ToolRepairOrderCandidateFor(IToolRepairOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        var tool = world.RepairableTools(actor, world.ToolRepairOrderTargets(instruction)).FirstOrDefault(lot =>
            lot.OwnerId == actor && lot.ConditionBasisPoints > 0 && lot.ItemKind == instruction.Task!.TargetEquipmentKind);
        return tool is null ? null : new("repair_tool",
            "Collect the materials and repair the requested personal tool at your household's Blacksmith.", 0, tool.Id);
    }


    internal static void ExecuteToolRepairOrderStep(IToolRepairOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        if (world.ToolRepairOrderCandidateFor(instruction)?.DestinationId is not { } lotId)
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.ToolRepairOrderBlockedReason(instruction));
            return;
        }
        var actor = instruction.TargetAgentId;
        if (world.RepairTool(actor, person, lotId, world.ToolRepairOrderTargets(instruction)) is not { } repairedLotId) return;
        var receipt = world.ToolRepairReceipt(actor, world.WorldTick, repairedLotId);
        world.CreditOrderEffect(instruction, Identity.Parse(receipt), 1);
    }


    internal static string ToolRepairOrderBlockedReason(IToolRepairOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        if (!world.IsAdult(actor)) return "This agent is too young to repair tools.";
        var inventory = world.Society.Inventory;
        if (!inventory.Lots.Any(lot => lot.OwnerId == actor &&
                lot.ItemKind == instruction.Task!.TargetEquipmentKind &&
                ToolProgressionRules.IsTopLevelCarriedLot(lot, actor) &&
                lot.ConditionBasisPoints is > 0 and < 10_000 && world.UnreservedQuantity(inventory, lot) > 0))
            return "Carry a matching worn personal tool that is available for repair.";
        var household = world.Society.GetAgent(actor).HouseholdId;
        if (household is null || world.HouseholdBuildingWithTag(household, "blacksmith") is null)
            return "The agent needs their household's Blacksmith to repair this tool.";
        return "The repair needs accessible materials, carrying space, a gathering tool that will not break the requested tools, and an open route to the household Blacksmith.";
    }


    internal static string[] ToolRepairOrderTargets(IToolRepairOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        var inventory = world.Society.Inventory;
        return inventory.Lots.Where(lot => lot.OwnerId == actor &&
                lot.ItemKind == instruction.Task!.TargetEquipmentKind &&
                ToolProgressionRules.IsTopLevelCarriedLot(lot, actor) &&
                lot.ConditionBasisPoints is > 0 and < 10_000 && world.UnreservedQuantity(inventory, lot) > 0)
            .Select(lot => lot.Id).ToArray();
    }

}
