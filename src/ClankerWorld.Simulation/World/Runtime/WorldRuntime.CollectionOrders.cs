using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private CognitionCandidate? CollectionOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.CollectionOrderCandidateFor(CollectionOrdersWorld, instruction, person);

    private IEnumerable<InventoryLot> CollectionOrderGoods(OwnerQueuedInstruction instruction, GoodsUse use = GoodsUse.RecoveryHoldings) =>
        OrderSystem.CollectionOrderGoods(CollectionOrdersWorld, instruction, use);

    private void ExecuteCollectionOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteCollectionOrderStep(CollectionOrdersWorld, instruction, person);

    private static string CollectionOrderGoodsName(OrderTask order) =>
        order.Action switch
        {
            "collect_food" => "food",
            "collect_equipment" => "equipment",
            "collect_goods" => "goods",
            _ => "material",
        };

    private string CollectionOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.CollectionOrderBlockedReason(CollectionOrdersWorld, instruction, person);
}
