using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static void CreditOrderEffect(IOrderLifecycleWorld world, OwnerQueuedInstruction instruction, string effectId, int units)
    {
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        var order = current.Task!;
        if (units <= 0 || order.LastEffectId == effectId || !world.IsActiveOrder(order.Status)) return;
        var completed = Math.Min(1_000_000, order.CompletedUnits + units);
        var finished = !order.RepeatUntilCancelled && completed >= order.RequestedUnits;
        world.Orders.RecordProgress(current.IdempotencyKey, OrderStore.PrepareTaskBinding(current, OrderStore.PrepareProgressCredit(order, completed, effectId, finished ? "finished" : "doing", null)));
        if (finished)
        {
            world.Orders.CompleteInstruction(current.InstructionId);
            world.AppendEvent("instruction_applied", $"{current.InstructionId}:{order.Action}");
            world.AppendEvent("instruction_order_finished", $"{current.TargetAgentId}:{current.InstructionId}:{effectId}");
        }
        else
        {
            world.AppendEvent("instruction_order_progress", $"{current.TargetAgentId}:{current.InstructionId}:{completed}");
        }
        world.MarkCheckpointCurrent();
    }

    internal static void SetOrderStatus(IOrderLifecycleWorld world,
        OwnerQueuedInstruction instruction,
        string status,
        string? reason,
        bool waitForDecision = false)
    {
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        if (current.Task is not { } order || !world.IsActiveOrder(order.Status)) return;
        if (status == "interrupted" && world.BoundProductionJob(current) is { } production)
            world.PauseProductionOrderJob(current, production);
        if (status == "interrupted")
        {
            world.PauseConstructionForOrder(current);
            world.PauseExpansionForOrder(current);
        }
        if (order.Status == status && order.BlockedReason == reason &&
            order.WaitForDecisionAfterFailure == waitForDecision) return;
        world.Orders.RecordStatus(current.IdempotencyKey, OrderStore.PrepareTaskBinding(current, OrderStore.PrepareOrderStatus(order, status, reason, waitForDecision)));
        world.MarkCheckpointCurrent();
        world.AppendEvent("instruction_order_status", $"{current.TargetAgentId}:{current.InstructionId}:{status}");
    }
}
