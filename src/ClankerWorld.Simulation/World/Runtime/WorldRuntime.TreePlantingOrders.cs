using TreeOrderPlan = ClankerWorld.Simulation.World.OrderSystem.TreeOrderPlan;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private static bool IsTreePlantingOrder(string action) => OrderSystem.IsTreePlantingOrder(action);

    private GridPoint? TreeOrderSite(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.TreeOrderSite(TreePlantingOrdersWorld, instruction, person);

    private string TreeOrderSpecies(OrderTask order, GridPoint site) =>
        OrderSystem.TreeOrderSpecies(TreePlantingOrdersWorld, order, site);

    private InventoryLot? CarriedTreeOrderSeed(string actor, string kind) =>
        OrderSystem.CarriedTreeOrderSeed(TreePlantingOrdersWorld, actor, kind);

    private InventoryLot? CollectableTreeOrderSeed(string actor, string kind, GridPoint site) =>
        OrderSystem.CollectableTreeOrderSeed(TreePlantingOrdersWorld, actor, kind, site);

    private TreeOrderPlan? TreePlantingOrderPlan(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.TreePlantingOrderPlan(TreePlantingOrdersWorld, instruction, person);

    private CognitionCandidate? TreePlantingOrderCandidate(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.TreePlantingOrderCandidate(TreePlantingOrdersWorld, instruction, person);

    private void ExecuteTreePlantingOrder(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteTreePlantingOrder(TreePlantingOrdersWorld, instruction, person);

    private string TreePlantingOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.TreePlantingOrderBlockedReason(TreePlantingOrdersWorld, instruction, person);
}
