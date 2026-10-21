using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private CognitionCandidate? RepairOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.RepairOrderCandidateFor(RepairOrdersWorld, instruction, person);

    private void ExecuteRepairOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteRepairOrderStep(RepairOrdersWorld, instruction, person);

    private string RepairOrderBlockedReason(OwnerQueuedInstruction instruction) =>
        OrderSystem.RepairOrderBlockedReason(RepairOrdersWorld, instruction);

    private void CancelRepairForOrder(OwnerQueuedInstruction instruction) =>
        OrderSystem.CancelRepairForOrder(RepairOrdersWorld, instruction);

    private static void ValidateRepairOrderBindings(IEnumerable<AgentState> people,
        InventoryCheckpoint inventory, IEnumerable<OwnerQueuedInstruction> instructions)
    {
        foreach (var person in people)
        {
            if (person.Equipment?.Repair is not { OrderInstructionId: { } orderId } repair) continue;
            var instruction = FirstActiveOrderFor(instructions, person.AgentId);
            var target = inventory.Lots.FirstOrDefault(lot => lot.Id == repair.LotId);
            if (target?.OwnerId != person.AgentId || instruction is null || instruction.InstructionId != orderId || instruction.Task is not { Action: "repair_equipment" } task ||
                task.Status == "queued" || repair.StartedTick < instruction.SubmittedTick ||
                target.ItemKind != task.TargetEquipmentKind)
                throw new InvalidDataException("The saved equipment repair does not belong to its active order.");
        }
    }
}
