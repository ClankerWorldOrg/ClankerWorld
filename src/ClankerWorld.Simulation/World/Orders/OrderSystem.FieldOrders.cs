using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static GridPoint? FieldOrderSite(IFieldOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var household = world.HouseholdFor(actor);
        if (!world.IsAdult(actor) || world.FarmhouseForHousehold(household) is not { } farmhouse) return null;
        var order = instruction.Task!;
        var kind = world.FieldOrderKind(order.Action);
        if (kind is FarmWorkKind.Till or FarmWorkKind.Tend &&
            ToolProgressionRules.PlanWork(world.Society.Inventory, actor, ToolFamily.Hoe) is null) return null;
        if (kind == FarmWorkKind.Till)
        {
            var queryFertility = world.Fertility;
            var heldByOthers = world.HouseholdLandHeldByOthers(household);
            return world.NearbyFarmTiles(farmhouse.Position).Where(point => !heldByOthers.Contains(point) &&
                    world.FarmableFreeTile(point, queryFertility))
                .Where(point => order.TargetPosition is null || point == order.TargetPosition)
                .OrderBy(point => world.Map.FootDistance(person.Position, point))
                .ThenByDescending(point => queryFertility.At(point))
                .ThenBy(point => point.Y).ThenBy(point => point.X)
                .Cast<GridPoint?>().FirstOrDefault(point => world.CanReachLeg(actor, person.Position, point!.Value, ReachPurpose.FieldWork, 0, 0, SwimmingPolicy.WhenCapable));
        }
        return world.Fields.Where(field => field.HouseholdId == household && field.Work is null &&
                (order.TargetPosition is null || field.Position == order.TargetPosition) &&
                (kind == FarmWorkKind.Plant ? field.Stage is FarmFieldStage.Prepared or FarmFieldStage.Harvested :
                    (order.TargetCropKind is null || field.Crop == order.TargetCropKind) &&
                    (kind == FarmWorkKind.Tend ? field.Stage == FarmFieldStage.Growing && !field.Tended : field.Stage == FarmFieldStage.Ready)))
            .OrderBy(field => world.Map.FootDistance(person.Position, field.Position))
            .ThenBy(field => field.Position.Y).ThenBy(field => field.Position.X)
            .Where(field => world.CanReachLeg(actor, person.Position, field.Position, ReachPurpose.FieldWork, 0, 0, SwimmingPolicy.WhenCapable) &&
                (kind != FarmWorkKind.Plant || !world.HasOtherAgentClaimedPlanting(field, actor) &&
                    world.PlantingStock(actor, field, order.TargetCropKind!, requirePickupRoute: true) is not null))
            .Select(field => (GridPoint?)field.Position).FirstOrDefault();
    }


    internal static CognitionCandidate? FieldOrderCandidateFor(IFieldOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        if (world.FarmWorkFor(instruction.TargetAgentId) is { Work: { } work })
            return new("work_field", work.OrderInstructionId == instruction.InstructionId
                ? "Finish the requested field work with its real tools and planting stock."
                : "End the previous field work and release unused planting stock before starting this task.", 0);
        return world.FieldOrderSite(instruction, person) is null ? null :
            new("work_field", "Walk to a suitable household field and complete the requested work.", 0);
    }


    internal static void ExecuteFieldOrderStep(IFieldOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (world.FarmWorkFor(actor) is { Work: { } work } current)
        {
            if (work.OrderInstructionId == instruction.InstructionId)
            {
                world.ContinueFarmWork(actor, out var completed);
                if (completed is not null)
                {
                    var receipt = world.FieldWorkOrderReceipt(actor, world.WorldTick, completed.Position, work.Kind, completed.Cycle);
                    world.CreditOrderEffect(instruction, Identity.Parse(receipt), 1);
                }
                return;
            }
            world.CancelFarmWork(current);
        }
        if (world.FieldOrderSite(instruction, person) is not { } site)
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.FieldOrderBlockedReason(instruction));
            return;
        }
        world.ApplyFieldCandidate(actor, person, world.FarmCandidate(world.FieldOrderKind(instruction.Task!.Action), site,
            instruction.Task.TargetCropKind), instruction.InstructionId);
    }


    internal static string FieldOrderBlockedReason(IFieldOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        if (!world.MayFollowOwnerOrder(actor, order)) return "This agent is too young to work on fields.";
        if (order.TargetPosition is { } target && !world.Map.Contains(target))
            return "The requested tile is outside this world.";
        if (world.FarmhouseForHousehold(world.HouseholdFor(actor)) is null) return "The agent needs a household with a Farmhouse.";
        var kind = world.FieldOrderKind(order.Action);
        if (kind is FarmWorkKind.Till or FarmWorkKind.Tend &&
            ToolProgressionRules.PlanWork(world.Society.Inventory, actor, ToolFamily.Hoe) is null)
            return "Carry a usable hoe for this field work.";
        if (order.TargetPosition is not null)
            return kind switch
            {
                FarmWorkKind.Till => "The requested tile needs free farmable land near the household's Farmhouse and an open walking route.",
                FarmWorkKind.Plant => "Planting at the requested tile needs a prepared household field, usable planting stock, carrying space and open walking routes.",
                FarmWorkKind.Tend => "No available household field at the requested tile has the requested crop ready for tending along an open walking route.",
                _ => "No available household field at the requested tile has the requested crop ready to harvest along an open walking route.",
            };
        return kind switch
        {
            FarmWorkKind.Till => "No open walking route reaches free farmable land near the household's Farmhouse.",
            FarmWorkKind.Plant => "Planting needs an available prepared household field, usable planting stock and carrying space, with open walking routes.",
            FarmWorkKind.Tend => "No available household field with the requested crop needs tending along an open walking route.",
            _ => "No available household field with the requested crop is ready to harvest along an open walking route.",
        };
    }


    internal static void CancelFieldWorkForOrder(IFieldOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        if (world.FarmWorkFor(instruction.TargetAgentId) is { Work: { } work } field &&
            work.OrderInstructionId == instruction.InstructionId)
            world.CancelFarmWork(field);
    }

}
