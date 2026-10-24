using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void InterruptOrdinaryFieldWorkForCustody(string actor, string lotId) =>
        OrderSystem.InterruptOrdinaryFieldWorkForCustody(CustodyOrdersWorld, actor, lotId);

    private CognitionCandidate? ReturnOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ReturnOrderCandidateFor(CustodyOrdersWorld, instruction, person);

    private void ExecuteReturnOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteReturnOrderStep(CustodyOrdersWorld, instruction, person);

    private string ReturnOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ReturnOrderBlockedReason(CustodyOrdersWorld, instruction, person);

    private static void ValidateCustodyOrderBindings(SocietyCheckpoint society,
        IEnumerable<OwnerQueuedInstruction> instructions)
    {
        // Removal, reassignment and expansion are legitimate world changes. They
        // block the saved destination at execution time rather than corrupting it.
        foreach (var instruction in instructions)
            if (instruction.Task is
                {
                    Action: "store_material" or "store_equipment" or "store_goods" or "return_borrowed",
                    TargetStorageOwnerId: { } owner
                } &&
                !society.Households.Any(household => household.Id == owner))
                throw new InvalidDataException("A custody order references an unknown owning household.");
    }
}
