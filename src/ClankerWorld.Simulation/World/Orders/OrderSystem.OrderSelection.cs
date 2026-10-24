using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static bool OrdinaryProjectCanYieldToOrder(IOrderSelectionWorld world, AgentProject project)
    {
        if (project.OrderInstructionId is not null || project.ToolMakingRequestId is not null) return false;
        return project.JobId is null || world.Simulation.ProductionJobs.Concat(world.Simulation.CropBuilds ?? [])
            .FirstOrDefault(job => job.JobId == project.JobId) is not { State: WorldProductionJobState.Running or WorldProductionJobState.Paused };
    }


    internal static CognitionCandidate? OrderCandidateFor(IOrderSelectionWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var order = instruction.Task;
        if (order is null || !world.IsActiveOrder(order.Status) || !world.MayFollowOwnerOrder(instruction.TargetAgentId, order))
            return null;
        if (order.TargetPosition is { } requestedPosition && !world.Map.Contains(requestedPosition))
            return null;
        if (order.Action == "read_knowledge") return world.KnowledgeReadOrderCandidate(instruction);
        if (IsConversationOrder(order.Action)) return world.TalkOrderCandidateFor(instruction, person);
        if (world.IsCartOrder(order.Action)) return world.CartOrderCandidateFor(instruction, person);
        if (world.IsAnimalOrder(order.Action)) return world.AnimalOrderCandidate(instruction);
        if (order.Action == "travel_by_boat") return world.BoatOrderCandidate(instruction);
        if (IsTreePlantingOrder(order.Action)) return world.TreePlantingOrderCandidate(instruction, person);
        if (IsKnowledgeOrder(order.Action)) return world.KnowledgeOrderCandidateFor(instruction);

        if (IsShelterOrder(order.Action))
            return world.ShelterOrderCandidateFor(instruction, person);

        if (order.Action == "construct_building")
            return world.ConstructionOrderCandidateFor(instruction, person);
        if (order.Action == "expand_building")
            return world.ExpansionOrderCandidateFor(instruction, person);

        if (order.Action == "deliver_stock")
            return world.DeliveryOrderCandidateFor(instruction, person);

        if (order.Action == "produce_item")
            return world.ProductionOrderCandidateFor(instruction, person);

        if (IsFieldOrder(order.Action))
            return world.FieldOrderCandidateFor(instruction, person);

        if (order.Action == "repair_tool")
            return world.ToolRepairOrderCandidateFor(instruction);

        if (order.Action == "repair_equipment")
            return world.RepairOrderCandidateFor(instruction, person);

        if (order.Action is "collect_material" or "collect_food" or "collect_equipment" or "collect_goods")
            return world.CollectionOrderCandidateFor(instruction, person);

        if (order.Action is "store_material" or "store_equipment" or "store_goods")
            return world.StorageOrderCandidateFor(instruction, person);

        if (order.Action == "return_borrowed")
            return world.ReturnOrderCandidateFor(instruction, person);

        if (order.Action == "gather_material")
            return world.MaterialOrderCandidateFor(instruction, person);

        if (order.Action == "move_to" && order.TargetPosition is { } destination)
            return !world.MovementOrderNeedsHouseInvitation(instruction.TargetAgentId, destination) &&
                (person.Position == destination ||
                 (world.Map.IsPassable(destination) || world.CanSwim(instruction.TargetAgentId) && SwimmingRules.IsSwimmingWater(world.Map, destination)) &&
                 world.Reach(instruction.TargetAgentId, destination, ReachPurpose.Movement, 0, 0, SwimmingPolicy.WhenCapable).Count > 0)
                    ? new CognitionCandidate("move_to", $"Travel to tile ({destination.X}, {destination.Y}).", 0)
                    : null;

        if (order.Action == "accept_guardianship" && order.TargetAgentId is { } child)
            return world.GuardianOrderCandidate(instruction.TargetAgentId, child);

        if (order.Action == "consume_food")
        {
            if (person.FullnessBasisPoints >= world.ComfortableFullness)
                return null;

            var carried = world.PreferredFood(instruction.TargetAgentId, instruction.TargetAgentId)
                .FirstOrDefault(lot => order.TargetFoodKind is null || lot.ItemKind == order.TargetFoodKind);
            if (carried is not null)
                return new CognitionCandidate("consume_food", "Eat the requested carried food item.", 0);
            if (world.AvailableSharedFood(instruction.TargetAgentId, order.TargetFoodKind) is { } shared &&
                world.FreeCarryCapacity(instruction.TargetAgentId) > 0)
                return new CognitionCandidate("collect_shared_food", "Collect food that is available to this household.", 0, shared.Id);
            return null;
        }

        if (order.Action is not ("seek_food" or "harvest_food"))
            return null;

        var source = world.KnownFoodSourceForOrder(instruction, person);
        if (source is not null)
        {
            if (order.Action == "harvest_food" &&
                world.FreeCarryCapacity(instruction.TargetAgentId) < world.FoodHarvestCarryUnits(source))
                return null;
            if (order.Action == "seek_food")
                return new CognitionCandidate("seek_food", "Travel to the food site named by this order.", 0, source.Id);
            return world.IsWithinInteractionRange(person.Position, source.Position, world.ResourceInteractionRange)
                ? new CognitionCandidate("harvest_food", "Gather food from the requested known site.", 0, source.Id)
                : new CognitionCandidate("seek_food", "Travel to the requested known food site.", 0, source.Id);
        }

        if (world.KnownTargetIsUnavailable(instruction) || !world.CanExploreForOrder(person))
            return null;
        return new CognitionCandidate("explore", "Explore nearby terrain to discover a suitable food site.", 0);
    }


    internal static bool MatchesFoodArrivalReceipt(IOrderSelectionWorld world, string? receipt, MapResource resource)
    {
        if (receipt is null) return false;
        // A receipt records the arrival tile. Recompute the small interaction area so a
        // hashed identity still excludes the same source after the person moves away.
        for (var x = resource.Position.X - world.ResourceInteractionRange; x <= resource.Position.X + world.ResourceInteractionRange; x++)
            for (var y = resource.Position.Y - world.ResourceInteractionRange; y <= resource.Position.Y + world.ResourceInteractionRange; y++)
            {
                var position = new GridPoint(x, y);
                if (world.IsWithinInteractionRange(position, resource.Position, world.ResourceInteractionRange) &&
                    receipt == world.FoodArrivalReceipt(resource.Id, position)) return true;
            }
        return false;
    }


    internal static MapResource? KnownFoodSourceForOrder(IOrderSelectionWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var order = instruction.Task!;
        return world.Map.Resources
            .Where(resource => resource.Kind is "food" or "fruit" &&
                world.Resources.GetValueOrDefault(resource.Id) == ResourceState.Available &&
                (order.TargetResourceId is null || resource.Id == order.TargetResourceId) &&
                !(order.RepeatUntilCancelled && order.Action == "seek_food" &&
                    world.MatchesFoodArrivalReceipt(order.LastEffectId, resource)) &&
                (order.TargetFoodKind is null || world.FoodKnowledgeKind(resource) == order.TargetFoodKind) &&
                (order.TargetPosition is null || resource.Position == order.TargetPosition.Value) &&
                (world.IsWithinInteractionRange(person.Position, resource.Position, world.ResourceInteractionRange) ||
                 world.Knowledge.Facts.Any(fact => fact.OwnerId == instruction.TargetAgentId &&
                    fact.Position == resource.Position &&
                    fact.ResourceKinds.Contains(world.FoodKnowledgeKind(resource), StringComparer.Ordinal))) &&
                world.CanReachTerrain(instruction.TargetAgentId, resource.Position, ReachPurpose.FoodSource, 0, SwimmingPolicy.WhenCapable))
            .OrderBy(resource => world.Map.FootDistance(person.Position, resource.Position))
            .ThenBy(resource => resource.Id, StringComparer.Ordinal)
            .FirstOrDefault(resource => world.IsWithinInteractionRange(person.Position, resource.Position, world.ResourceInteractionRange) ||
                world.Reach(instruction.TargetAgentId, resource.Position, ReachPurpose.FoodSource, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0);
    }


    internal static bool KnownTargetIsUnavailable(IOrderSelectionWorld world, OwnerQueuedInstruction instruction)
    {
        var order = instruction.Task!;
        if (order.TargetResourceId is { } resourceId)
        {
            var resource = world.Map.Resources.SingleOrDefault(item => item.Id == resourceId);
            return resource is null || world.Knowledge.Facts.Any(fact => fact.OwnerId == instruction.TargetAgentId &&
                fact.Position == resource.Position &&
                fact.ResourceKinds.Contains(world.FoodKnowledgeKind(resource), StringComparer.Ordinal));
        }
        if (order.TargetPosition is { } position)
            return !world.Map.Contains(position) || world.Knowledge.Facts.Any(fact =>
                fact.OwnerId == instruction.TargetAgentId && fact.Position == position);
        return false;
    }


    internal static bool CanExploreForOrder(IOrderSelectionWorld world, AgentState person)
    {
        if (world.NeedsUrgentFood(person) || world.NeedsUrgentWarmth(person)) return false;
        if (person.Exploration?.OutingPath.Count > 0)
            return world.HasWarmthForOuting(person);
        return person.FullnessBasisPoints >= world.OutingFullnessReserve && world.HasWarmthForOuting(person) &&
            world.Map.FootNeighbors(person.Position).Any(world.Map.IsPassable);
    }
    internal static string OrderBlockedReason(IOrderBlockersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        if (instruction.Task?.Action == "travel_by_boat") return world.BoatOrderBlockedReason(instruction);
        if (instruction.Task?.Action == "read_knowledge")
            return world.KnowledgeReadOrderBlocker(instruction) ?? "Waiting to read the written item.";
        if (instruction.Task is { } conversationOrder && IsConversationOrder(conversationOrder.Action)) return world.TalkOrderBlocker(instruction, person) ?? "Waiting for the conversation outcome.";
        if (instruction.Task is { } treeOrder && IsTreePlantingOrder(treeOrder.Action))
            return world.TreePlantingOrderBlockedReason(instruction, person);
        if (instruction.Task is { } knowledgeOrder && IsKnowledgeOrder(knowledgeOrder.Action)) return world.KnowledgeOrderBlockedReason(instruction);
        if (instruction.Task is { } cartOrder && world.IsCartOrder(cartOrder.Action))
            return world.CartOrderBlocker(instruction, person) ?? "Waiting for the selected cart action.";
        if (instruction.Task is { } animalOrder && world.IsAnimalOrder(animalOrder.Action))
            return world.AnimalOrderBlockedReason(instruction);
        if (instruction.Task is { } protective && IsShelterOrder(protective.Action))
            return world.ShelterOrderBlockedReason(instruction, person);
        if (instruction.Task?.Action == "construct_building")
            return world.ConstructionOrderBlockedReason(instruction, person);
        if (instruction.Task?.Action == "expand_building")
            return world.ExpansionOrderBlockedReason(instruction);
        if (instruction.Task?.Action == "deliver_stock")
            return world.DeliveryOrderBlockedReason(instruction, person);
        if (instruction.Task?.Action == "produce_item")
            return world.ProductionOrderBlockedReason(instruction);
        if (instruction.Task is { } fieldOrder && IsFieldOrder(fieldOrder.Action))
            return world.FieldOrderBlockedReason(instruction);
        if (instruction.Task?.Action == "repair_tool")
            return world.ToolRepairOrderBlockedReason(instruction);
        if (instruction.Task?.Action == "repair_equipment")
            return world.RepairOrderBlockedReason(instruction);
        if (instruction.Task?.Action is "collect_material" or "collect_food" or "collect_equipment" or "collect_goods")
            return world.CollectionOrderBlockedReason(instruction, person);
        if (instruction.Task?.Action is "store_material" or "store_equipment" or "store_goods")
            return world.StorageOrderBlockedReason(instruction, person);
        if (instruction.Task?.Action == "return_borrowed")
            return world.ReturnOrderBlockedReason(instruction, person);
        if (instruction.Task?.Action == "gather_material")
            return world.MaterialOrderBlockedReason(instruction, person);

        if (instruction.Task?.Action == "move_to")
            return instruction.Task.TargetPosition is not { } destination || !world.Map.Contains(destination)
                ? "The requested tile is outside this world."
                : world.MovementOrderNeedsHouseInvitation(instruction.TargetAgentId, destination)
                    ? "Waiting for an invitation to enter another household's House."
                : "No open walking or safe swimming route reaches the requested tile right now.";
        if (instruction.Task is { Action: "accept_guardianship", TargetAgentId: { } child })
            return world.GuardianOrderBlockedReason(instruction.TargetAgentId, child);
        if (instruction.Task?.Action == "consume_food")
        {
            if (person.FullnessBasisPoints >= world.ComfortableFullness)
                return "Waiting until hungry enough to eat.";
            if (world.FreeCarryCapacity(instruction.TargetAgentId) == 0 &&
                world.AvailableSharedFood(instruction.TargetAgentId, instruction.Task.TargetFoodKind) is not null)
                return "Carrying space is full; make room before collecting food from the household store.";
            return "No matching food is available to eat right now.";
        }
        if (instruction.Task?.Action == "harvest_food" &&
            world.KnownFoodSourceForOrder(instruction, person) is { } source &&
            world.FreeCarryCapacity(instruction.TargetAgentId) < world.FoodHarvestCarryUnits(source))
            return "Carrying space is full; make room before gathering from the requested food site.";
        if (instruction.Task?.TargetPosition is not null || instruction.Task?.TargetResourceId is not null)
            return "The requested food site isn't known or reachable right now.";
        return !world.CanExploreForOrder(person)
            ? "The agent needs to be fed and warm before searching for food."
            : "No suitable food source is known yet.";
    }


    internal static bool MovementOrderNeedsHouseInvitation(IOrderBlockersWorld world, string actor, GridPoint destination) => world.BuildingsWithTag("house").Any(house => house.HouseholdId is not null &&
            house.HouseholdId != world.Society.GetAgent(actor).HouseholdId &&
            !world.HasHouseGuestInvitation(actor, house.InstanceId) &&
            WorldContentSimulationRules.Footprint(
                world.Content.Buildings.Single(definition => definition.CanonicalId == house.DefinitionId), house).Contains(destination));
    internal static bool MayFollowOwnerOrder(IOrderCapabilitiesWorld world, string actor, OrderTask order) => ActorCapabilities.Check(world.CapabilitiesFor(actor), order.Action, OwnerOrderCapabilities.For(order.Action)).Allowed;
    internal static void CreditOrderEffect(IOrderLifecycleWorld world, OwnerQueuedInstruction instruction, Identity effectId, int units)
    {
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        var order = current.Task!;
        if (units <= 0 || order.LastEffectId == effectId || !world.IsActiveOrder(order.Status)) return;
        var completed = Math.Min(1_000_000, order.CompletedUnits + units);
        var finished = !order.RepeatUntilCancelled && completed >= order.RequestedUnits;
        world.Orders.RecordProgress(current.IdempotencyKey, OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(current, (OrderStore.PrepareCreditOrderEffectOrderTask(order, completed, effectId)).Transition(finished ? OrderStatus.Finished : OrderStatus.Doing)));
        if (finished)
        {
            world.AppendEvent(new InstructionAppliedEventPayload(current.TargetAgentId, current.InstructionId, order.Action));
            world.AppendEvent(new InstructionOrderFinishedEventPayload(current.TargetAgentId, current.InstructionId, effectId));
            world.PruneClosedOrderHistory();
        }
        else
        {
            world.AppendEvent(new InstructionOrderProgressEventPayload(current.TargetAgentId, current.InstructionId, completed));
        }
        world.MarkCheckpointCurrent();
    }


    internal static void SetOrderStatus(IOrderLifecycleWorld world,
        OwnerQueuedInstruction instruction,
        OrderStatus status,
        string? reason,
        bool waitForDecision = false)
    {
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        if (current.Task is not { } order || !world.IsActiveOrder(order.Status)) return;
        if (status == OrderStatus.Interrupted && world.BoundProductionJob(current) is { } production)
            world.PauseProductionOrderJob(current, production);
        if (status == OrderStatus.Interrupted)
        {
            world.PauseConstructionForOrder(current);
            world.PauseExpansionForOrder(current);
        }
        if (order.LifecycleStatus == status && order.BlockedReason == reason &&
            order.WaitForDecisionAfterFailure == waitForDecision) return;
        world.Orders.RecordStatus(current.IdempotencyKey, OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(current, order.Transition(status, reason, waitForDecision)));
        world.MarkCheckpointCurrent();
        world.AppendEvent(new InstructionOrderStatusEventPayload(current.TargetAgentId, current.InstructionId, status.ToWireValue()));
    }
    internal static void ExecuteOrderStep(IOrderExecutionWorld world,
        OwnerQueuedInstruction instruction,
        AgentState person,
        CognitionCandidate candidate)
    {
        var order = instruction.Task!;
        world.SetOrderStatus(instruction, OrderStatus.Doing, null, waitForDecision: false);
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
        if (IsTreePlantingOrder(order.Action))
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
                // Only the tile reached joins the agent's small map memory. Recording
                // every step of a long walk would fill it and leave no room for what
                // exploration finds later.
                if (world.Agents[actor].Position == destination)
                {
                    world.RecordKnowledgeFact(actor, destination);
                    world.CreditOrderEffect(instruction, Identity.Parse(world.MovementOrderEffectId(destination)), 1);
                }
                return;
            case "consume_food":
                if (world.ConsumeFood(actor, person, order.TargetFoodKind) is { } consumedLotId)
                {
                    var receipt = world.ConsumptionOrderReceipt(world.WorldTick, actor, consumedLotId);
                    world.CreditOrderEffect(instruction, Identity.Parse(receipt), 1);
                }
                else
                    world.SetOrderStatus(instruction, OrderStatus.Blocked, "No matching food is available to eat.");
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
                        world.SetOrderStatus(instruction, OrderStatus.Blocked, world.OrderBlockedReason(instruction, world.Agents[actor]));
                        return;
                    }
                    var current = world.Agents[actor];
                    if (world.IsWithinInteractionRange(current.Position, target.Position, world.ResourceInteractionRange))
                    {
                        if (order.Action == "seek_food")
                            world.CreditOrderEffect(instruction,
                                Identity.Parse(world.FoodArrivalReceipt(target.Id, current.Position)), 1);
                        return;
                    }
                    world.MoveToward(actor, current, target.Position, "owner_order_food", world.ResourceInteractionRange);
                    current = world.Agents[actor];
                    if (order.Action == "seek_food" &&
                        world.IsWithinInteractionRange(current.Position, target.Position, world.ResourceInteractionRange))
                        world.CreditOrderEffect(instruction,
                            Identity.Parse(world.FoodArrivalReceipt(target.Id, current.Position)), 1);
                    return;
                }
            case "harvest_food":
                {
                    var source = world.KnownFoodSourceForOrder(instruction, world.Agents[actor]);
                    if (source is null)
                    {
                        world.SetOrderStatus(instruction, OrderStatus.Blocked, world.OrderBlockedReason(instruction, world.Agents[actor]));
                        return;
                    }
                    if (!world.IsWithinInteractionRange(world.Agents[actor].Position, source.Position, world.ResourceInteractionRange))
                    {
                        world.MoveToward(actor, world.Agents[actor], source.Position, "owner_order_harvest", world.ResourceInteractionRange);
                        return;
                    }
                    if (world.HarvestFood(actor, world.Agents[actor], source) is { } effect &&
                        (order.TargetFoodKind is null || effect.ItemKind == order.TargetFoodKind))
                        world.CreditOrderEffect(instruction, Identity.Parse(world.FoodHarvestReceipt(effect.LotId)),
                            order.ProgressUnit == "harvests" ? 1 : effect.Quantity);
                    else
                        world.SetOrderStatus(instruction, OrderStatus.Blocked, "The requested food site did not yield the requested food.");
                    return;
                }
            default:
                world.SetOrderStatus(instruction, OrderStatus.Blocked, "The requested task is not available right now.");
                return;
        }
    }
    internal static bool ShouldInterruptOrder(IOrderSurvivalWorld world,
        AgentState person,
        OwnerQueuedInstruction instruction,
        CognitionCandidate? orderCandidate,
        CognitionCandidate? urgentCandidate)
    {
        if (IsShelterOrder(instruction.Task!.Action))
            return world.NeedsUrgentFood(person) && urgentCandidate is not null && world.IsFoodSurvivalCandidate(person.AgentId, urgentCandidate.Id) ||
                orderCandidate is null && urgentCandidate is not null;
        if (world.NeedsUrgentWarmth(person))
            return urgentCandidate is not null &&
                orderCandidate?.Id is not ("wear_clothing" or "tend_fire" or "seek_warmth");
        if (!world.NeedsUrgentFood(person)) return false;
        if (urgentCandidate is null) return false;
        return orderCandidate is null || orderCandidate.Id != urgentCandidate.Id ||
            orderCandidate.DestinationId != urgentCandidate.DestinationId;
    }


    internal static CognitionCandidate? UrgentSurvivalCandidateFor(IOrderSurvivalWorld world,
        string agentId, AgentState person, OwnerQueuedInstruction? instruction = null) => world.SelectOrderSurvivalCandidate(world.CreateCandidates(agentId, person, restrictForOrder: false), person, instruction);

    internal static CognitionCandidate? SelectOrderSurvivalCandidate(IOrderSurvivalWorld world,
        IEnumerable<CognitionCandidate> candidates, AgentState person, OwnerQueuedInstruction? instruction)
    {
        var available = candidates.Where(candidate => world.IsSurvivalCandidate(person.AgentId, candidate.Id))
            .OrderBy(candidate => candidate.DeterministicPriority)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal).ToArray();
        if (instruction?.Task is { } order && IsShelterOrder(order.Action) && world.NeedsUrgentFood(person) &&
            available.FirstOrDefault(candidate => world.IsFoodSurvivalCandidate(person.AgentId, candidate.Id)) is { } food)
            return food;
        return available.FirstOrDefault();
    }


    internal static bool IsFoodSurvivalCandidate(IOrderSurvivalWorld world, string actor, string candidateId) => candidateId is
        "consume_food" or "drink_milk" or "collect_shared_food" or "take_food_from_pot" or "make_room_for_food" or
        "harvest_food" or "seek_food" || candidateId.StartsWith(world.TownProjectReturnPrefix, StringComparison.Ordinal) ||
        world.NeedsUrgentFood(world.Agents[actor]) &&
        (world.IsShopFoodCandidate(actor, candidateId) || world.IsMarketFoodCandidate(actor, candidateId));

    internal static bool IsSurvivalCandidate(IOrderSurvivalWorld world, string actor, string candidateId) => world.IsFoodSurvivalCandidate(actor, candidateId) || candidateId is
        "recover_household_delivery" or "wear_clothing" or "tend_fire" or "seek_warmth";

}
