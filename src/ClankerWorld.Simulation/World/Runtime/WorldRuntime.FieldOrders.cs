using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static bool IsFieldOrder(string action) => action is "till_field" or "plant_field" or "tend_field" or "harvest_field";

    private static FarmWorkKind FieldOrderKind(string action) => action switch
    {
        "till_field" => FarmWorkKind.Till,
        "plant_field" => FarmWorkKind.Plant,
        "tend_field" => FarmWorkKind.Tend,
        "harvest_field" => FarmWorkKind.Harvest,
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    private GridPoint? FieldOrderSite(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.FieldOrderSite(FieldOrdersWorld, instruction, person);

    private CognitionCandidate? FieldOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.FieldOrderCandidateFor(FieldOrdersWorld, instruction, person);

    private void ExecuteFieldOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteFieldOrderStep(FieldOrdersWorld, instruction, person);

    private string FieldOrderBlockedReason(OwnerQueuedInstruction instruction) =>
        OrderSystem.FieldOrderBlockedReason(FieldOrdersWorld, instruction);

    private void CancelFieldWorkForOrder(OwnerQueuedInstruction instruction) =>
        OrderSystem.CancelFieldWorkForOrder(FieldOrdersWorld, instruction);

    private static void ValidateFieldOrderBindings(IEnumerable<FarmFieldState> savedFields,
        IEnumerable<OwnerQueuedInstruction> instructions)
    {
        foreach (var field in savedFields)
        {
            if (field.Work is not { OrderInstructionId: { } orderId } work) continue;
            var instruction = instructions.Where(item => item.TargetAgentId == work.WorkerId &&
                    item.Kind == OwnerInstructionKind.Order && item.Task is { } order && IsActiveOrder(order.Status))
                .OrderBy(item => item.SubmissionSequence).FirstOrDefault();
            if (instruction is null || instruction.InstructionId != orderId || instruction.Task is not { } task ||
                !IsFieldOrder(task.Action) || task.Status == "queued" || FieldOrderKind(task.Action) != work.Kind ||
                work.LastWorkedTick < instruction.SubmittedTick ||
                task.TargetPosition is { } target && target != field.Position ||
                task.TargetCropKind is { } crop && crop != (work.Kind == FarmWorkKind.Plant ? work.Crop : field.Crop))
                throw new InvalidDataException("The saved field work does not belong to its active order.");
        }
    }
}
