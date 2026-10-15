using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private CognitionCandidate? ToolRepairOrderCandidateFor(OwnerQueuedInstruction instruction) =>
        OrderSystem.ToolRepairOrderCandidateFor(ToolRepairOrdersWorld, instruction);

    private void ExecuteToolRepairOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteToolRepairOrderStep(ToolRepairOrdersWorld, instruction, person);

    private string ToolRepairOrderBlockedReason(OwnerQueuedInstruction instruction) =>
        OrderSystem.ToolRepairOrderBlockedReason(ToolRepairOrdersWorld, instruction);

    private string[] ToolRepairOrderTargets(OwnerQueuedInstruction instruction) =>
        OrderSystem.ToolRepairOrderTargets(ToolRepairOrdersWorld, instruction);
}
