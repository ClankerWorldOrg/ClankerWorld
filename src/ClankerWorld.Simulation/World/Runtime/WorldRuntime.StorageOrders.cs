using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private CognitionCandidate? StorageOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.StorageOrderCandidateFor(StorageOrdersWorld, instruction, person);

    private void ExecuteStorageOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteStorageOrderStep(StorageOrdersWorld, instruction, person);

    private string StorageOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.StorageOrderBlockedReason(StorageOrdersWorld, instruction, person);

    private static string? StorageOrderItemKind(OrderTask order) =>
        order.TargetItemKind ?? order.TargetEquipmentKind ?? order.TargetMaterialKind;

    private static string StorageOrderSubject(OrderTask order) =>
        order.Action == "store_equipment" ? "equipment" : order.Action == "store_goods" ? "goods" : "materials";
}
