using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static bool IsCartCargoOrder(string action) => action is "load_handcart" or "unload_handcart" or "unload_handcart_ground";

    private static bool IsCartCargoKind(string? kind) => kind is not null &&
        (WorldCustodyOrderCatalog.IsReturnKind(kind) || IsEdibleFood(kind)) &&
        InventoryContainerRules.Allows(InventoryContainerRules.Handcart, kind);

    private OrderTask? ParseCartCargoOrder(string text, string actor) =>
        OrderSystem.ParseCartCargoOrder(CartCargoOrdersWorld, text, actor);

    private InventoryLot? CartForCargoOrder(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.CartForCargoOrder(CartCargoOrdersWorld, instruction, person);

    private InventoryLot? CargoLotForOrder(OwnerQueuedInstruction instruction, AgentState person, InventoryLot cart) =>
        OrderSystem.CargoLotForOrder(CartCargoOrdersWorld, instruction, person, cart);

    private string? CartCargoOrderBlocker(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.CartCargoOrderBlocker(CartCargoOrdersWorld, instruction, person);

    private CognitionCandidate? CartCargoOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.CartCargoOrderCandidateFor(CartCargoOrdersWorld, instruction, person);

    private int CargoQuantity(string cartId) =>
        OrderSystem.CargoQuantity(CartCargoOrdersWorld, cartId);

    private void ExecuteCartCargoOrderStep(OwnerQueuedInstruction instruction, AgentState person) =>
        OrderSystem.ExecuteCartCargoOrderStep(CartCargoOrdersWorld, instruction, person);

    private static string CartCargoOrderEffectId(OwnerQueuedInstruction instruction, int completed) =>
        DerivedId.Hash(
            $"{instruction.InstructionId}|{instruction.TargetAgentId}|{instruction.Task!.Action}|{instruction.Task.TargetCartLotId}|{instruction.Task.TargetItemKind}|{completed}", "cart-cargo-order:");

    internal static bool IsValidCartCargoOrder(OwnerQueuedInstruction instruction)
    {
        var order = instruction.Task!;
        return IsCartCargoKind(order.TargetItemKind) &&
            (order.TargetCartLotId is null || IdentityRules.Reference(order.TargetCartLotId, OwnerQueuedInstruction.MaximumIdentifierLength) && order.TargetCartLotId == order.TargetCartLotId.Trim()) &&
            (order.TargetLotId is null || order.TargetCartLotId is not null) &&
            order.RequestedUnits is >= 1 and <= 1000 && order.CompletedUnits >= 0 && order.CompletedUnits <= order.RequestedUnits &&
            !order.RepeatUntilCancelled && order.QuantityIsExplicit && order.ProgressUnit == "goods_items" &&
            order.TargetFoodKind is null && order.TargetAgentId is null && order.TargetResourceId is null && order.TargetPosition is null &&
            order.Status != "not_understood" && (order.Status == "finished") == (order.CompletedUnits == order.RequestedUnits) &&
            (order.CompletedUnits == 0 ? order.LastEffectId is null :
                order.TargetCartLotId is not null && order.LastEffectId == CartCargoOrderEffectId(instruction, order.CompletedUnits));
    }
}
