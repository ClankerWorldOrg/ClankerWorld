using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static void ExecuteOrderStep(IOrderExecutionWorld world,
        OwnerQueuedInstruction instruction,
        AgentState person,
        CognitionCandidate candidate)
    {
        var order = instruction.Task!;
        world.SetOrderStatus(instruction, "doing", null, waitForDecision: false);
        var actor = instruction.TargetAgentId;
        world.RecordAttemptedAction(actor, candidate.Id);
        if (candidate.Id != "explore" && person.Exploration?.Goal?.OrderInstructionId == instruction.InstructionId)
            person = world.CompleteExplorationGoal(actor, person);
        // An order step interrupts timed repair work, as any other chosen action does.
        if (order.Action != "repair_equipment" && world.Agents[actor].Equipment?.Repair is not null)
        {
            world.CancelEquipmentRepair(actor);
            person = world.Agents[actor];
        }
        if (order.Action == "accept_guardianship")
        {
            world.ExecuteGuardianOrder(instruction, candidate);
            return;
        }
        if (world.IsCartOrder(order.Action))
        {
            world.ExecuteCartOrderStep(instruction, person);
            return;
        }
        if (world.IsAnimalOrder(order.Action))
        {
            world.ExecuteAnimalOrder(instruction);
            return;
        }
        if (order.Action == "travel_by_boat")
        {
            world.ExecuteBoatOrder(instruction);
            return;
        }
        if (world.IsTreePlantingOrder(order.Action))
        {
            world.ExecuteTreePlantingOrder(instruction, person);
            return;
        }
        switch (candidate.Id)
        {
            case "read_knowledge":
                world.ExecuteKnowledgeReadOrder(instruction);
                return;
            case "talk_to":
            case "propose_marriage":
                world.ExecuteTalkOrderStep(instruction, person);
                return;
            case "write_knowledge":
            case "copy_knowledge":
                world.ExecuteKnowledgeOrderStep(instruction, person);
                return;
            case "seek_shelter":
            case "tend_fire":
            case "inspect_shelter_site":
                world.ExecuteShelterOrderStep(instruction, person);
                return;
            case "construct_building":
                world.ExecuteConstructionOrderStep(instruction, person);
                return;
            case "expand_building":
                world.ExecuteExpansionOrderStep(instruction, person);
                return;
            case "deliver_stock":
                world.ExecuteDeliveryOrderStep(instruction, person);
                return;
            case "produce_item":
                world.ExecuteProductionOrderStep(instruction, person);
                return;
            case "work_field":
                world.ExecuteFieldOrderStep(instruction, person);
                return;
            case "repair_tool":
                world.ExecuteToolRepairOrderStep(instruction, person);
                return;
            case "repair_equipment":
                world.ExecuteRepairOrderStep(instruction, person);
                return;
            case "collect_material":
            case "collect_food":
            case "collect_equipment":
            case "collect_goods":
                world.ExecuteCollectionOrderStep(instruction, person);
                return;
            case "store_material":
            case "store_equipment":
            case "store_goods":
                world.ExecuteStorageOrderStep(instruction, person);
                return;
            case "return_borrowed":
                world.ExecuteReturnOrderStep(instruction, person);
                return;
            case "gather_material":
            case "inspect_material_site":
                world.ExecuteMaterialOrderStep(instruction, person, candidate.Id);
                return;
            case "move_to" when order.Action == "move_to" && order.TargetPosition is { } destination:
                world.MoveToward(actor, person, destination, "owner_order_move");
                // Only the tile reached joins the agent's small world.Map memory. Recording
                // every step of a long walk would fill it and leave no room for what
                // exploration finds later.
                if (world.Agents[actor].Position == destination)
                {
                    world.RecordKnowledgeFact(actor, destination);
                    world.CreditOrderEffect(instruction, world.MovementOrderEffectId(destination), 1);
                }
                return;
            case "consume_food":
                if (world.ConsumeFood(actor, person, order.TargetFoodKind) is { } consumedLotId)
                {
                    var receipt = world.ConsumptionOrderReceipt(world.WorldTick, actor, consumedLotId);
                    world.CreditOrderEffect(instruction, receipt, 1);
                }
                else
                    world.SetOrderStatus(instruction, "blocked", "No matching food is available to eat.");
                return;
            case "collect_shared_food":
                world.CollectSharedFood(actor, person, order.TargetFoodKind);
                return;
            case "explore":
                world.PhysicalAgents.ResumeExploration(actor, person);
                world.Explore(actor, person, world.ExplorationGoalForOrder(instruction), replaceGoal: true);
                return;
            case "seek_food":
                {
                    var target = world.KnownFoodSourceForOrder(instruction, world.Agents[actor]);
                    if (target is null)
                    {
                        world.SetOrderStatus(instruction, "blocked", world.OrderBlockedReason(instruction, world.Agents[actor]));
                        return;
                    }
                    var current = world.Agents[actor];
                    if (world.IsWithinInteractionRange(current.Position, target.Position, world.ResourceInteractionRange))
                    {
                        if (order.Action == "seek_food")
                            world.CreditOrderEffect(instruction,
                                world.FoodArrivalReceipt(target.Id, current.Position), 1);
                        return;
                    }
                    world.MoveToward(actor, current, target.Position, "owner_order_food", world.ResourceInteractionRange);
                    current = world.Agents[actor];
                    if (order.Action == "seek_food" &&
                        world.IsWithinInteractionRange(current.Position, target.Position, world.ResourceInteractionRange))
                        world.CreditOrderEffect(instruction,
                            world.FoodArrivalReceipt(target.Id, current.Position), 1);
                    return;
                }
            case "harvest_food":
                {
                    var source = world.KnownFoodSourceForOrder(instruction, world.Agents[actor]);
                    if (source is null)
                    {
                        world.SetOrderStatus(instruction, "blocked", world.OrderBlockedReason(instruction, world.Agents[actor]));
                        return;
                    }
                    if (!world.IsWithinInteractionRange(world.Agents[actor].Position, source.Position, world.ResourceInteractionRange))
                    {
                        world.MoveToward(actor, world.Agents[actor], source.Position, "owner_order_harvest", world.ResourceInteractionRange);
                        return;
                    }
                    if (world.HarvestFood(actor, world.Agents[actor], source) is { } effect &&
                        (order.TargetFoodKind is null || effect.ItemKind == order.TargetFoodKind))
                        world.CreditOrderEffect(instruction, world.FoodHarvestReceipt(effect.LotId),
                            order.ProgressUnit == "harvests" ? 1 : effect.Quantity);
                    else
                        world.SetOrderStatus(instruction, "blocked", "The requested food site did not yield the requested food.");
                    return;
                }
            default:
                world.SetOrderStatus(instruction, "blocked", "The requested task is not available right now.");
                return;
        }
    }
}
