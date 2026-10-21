using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private CognitionCandidate? MaterialOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.MaterialOrderCandidateFor(MaterialOrdersWorld, instruction, person);

    private IEnumerable<MapResource> KnownMaterialOrderSources(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.KnownMaterialOrderSources(MaterialOrdersWorld, instruction, person);

    private GridPoint? MaterialOrderTarget(OwnerQueuedInstruction instruction) =>
        OrderSystem.MaterialOrderTarget(MaterialOrdersWorld, instruction);

    private void ExecuteMaterialOrderStep(OwnerQueuedInstruction instruction, AgentState person, string candidate) =>
        OrderSystem.ExecuteMaterialOrderStep(MaterialOrdersWorld, instruction, person, candidate);

    private string MaterialOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.MaterialOrderBlockedReason(MaterialOrdersWorld, instruction, person);
}
