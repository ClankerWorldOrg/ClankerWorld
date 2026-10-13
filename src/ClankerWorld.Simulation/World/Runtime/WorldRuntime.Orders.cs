using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private sealed record FoodHarvestEffect(string LotId, string ItemKind, int Quantity, string ResourceId);

    // Unpaid ordinary work may yield. Keep bound work and live paid jobs intact;
    // completed/cancelled jobs already retain their own payment and output history.
    private bool OrdinaryProjectCanYieldToOrder(AgentProject project)
    {
        if (project.OrderInstructionId is not null || project.ToolMakingRequestId is not null) return false;
        return project.JobId is null || worldSimulation.ProductionJobs.Concat(worldSimulation.CropBuilds ?? [])
            .FirstOrDefault(job => job.JobId == project.JobId) is not { State: WorldProductionJobState.Running or WorldProductionJobState.Paused };
    }

    private CognitionCandidate? OrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person)
    {
        var order = instruction.Task;
        if (order is null || !IsActiveOrder(order.Status) || !AgePermitsCandidate(instruction.TargetAgentId, order.Action))
            return null;
        if (order.TargetPosition is { } requestedPosition && !map.Contains(requestedPosition))
            return null;
        if (order.Action == "read_knowledge") return KnowledgeReadOrderCandidate(instruction);
        if (IsConversationOrder(order.Action)) return TalkOrderCandidateFor(instruction, person);
        if (IsCartOrder(order.Action)) return CartOrderCandidateFor(instruction, person);
        if (IsAnimalOrder(order.Action)) return AnimalOrderCandidate(instruction);
        if (order.Action == "travel_by_boat") return BoatOrderCandidate(instruction);
        if (IsTreePlantingOrder(order.Action)) return TreePlantingOrderCandidate(instruction, person);
        if (IsKnowledgeOrder(order.Action)) return KnowledgeOrderCandidateFor(instruction);

        if (IsShelterOrder(order.Action))
            return ShelterOrderCandidateFor(instruction, person);

        if (order.Action == "construct_building")
            return ConstructionOrderCandidateFor(instruction, person);
        if (order.Action == "expand_building")
            return ExpansionOrderCandidateFor(instruction, person);

        if (order.Action == "deliver_stock")
            return DeliveryOrderCandidateFor(instruction, person);

        if (order.Action == "produce_item")
            return ProductionOrderCandidateFor(instruction, person);

        if (IsFieldOrder(order.Action))
            return FieldOrderCandidateFor(instruction, person);

        if (order.Action == "repair_tool")
            return ToolRepairOrderCandidateFor(instruction);

        if (order.Action == "repair_equipment")
            return RepairOrderCandidateFor(instruction, person);

        if (order.Action is "collect_material" or "collect_food" or "collect_equipment" or "collect_goods")
            return CollectionOrderCandidateFor(instruction, person);

        if (order.Action is "store_material" or "store_equipment" or "store_goods")
            return StorageOrderCandidateFor(instruction, person);

        if (order.Action == "return_borrowed")
            return ReturnOrderCandidateFor(instruction, person);

        if (order.Action == "gather_material")
            return MaterialOrderCandidateFor(instruction, person);

        if (order.Action == "move_to" && order.TargetPosition is { } destination)
            return !MovementOrderNeedsHouseInvitation(instruction.TargetAgentId, destination) &&
                (person.Position == destination ||
                 (map.IsPassable(destination) || CanSwim(instruction.TargetAgentId) && SwimmingRules.IsSwimmingWater(map, destination)) &&
                 FindUnoccupiedRoute(instruction.TargetAgentId, person.Position, destination, 0).Count > 0)
                    ? new CognitionCandidate("move_to", $"Travel to tile ({destination.X}, {destination.Y}).", 0)
                    : null;

        if (order.Action == "accept_guardianship" && order.TargetAgentId is { } child)
            return GuardianOrderCandidate(instruction.TargetAgentId, child);

        if (order.Action == "consume_food")
        {
            if (person.FullnessBasisPoints >= ComfortableFullness)
                return null;

            var carried = PreferredFood(instruction.TargetAgentId, instruction.TargetAgentId)
                .FirstOrDefault(lot => order.TargetFoodKind is null || lot.ItemKind == order.TargetFoodKind);
            if (carried is not null)
                return new CognitionCandidate("consume_food", "Eat the requested carried food item.", 0);
            if (AvailableSharedFood(instruction.TargetAgentId, order.TargetFoodKind) is { } shared &&
                FreeCarryCapacity(instruction.TargetAgentId) > 0)
                return new CognitionCandidate("collect_shared_food", "Collect food that is available to this household.", 0, shared.Id);
            return null;
        }

        if (order.Action is not ("seek_food" or "harvest_food"))
            return null;

        var source = KnownFoodSourceForOrder(instruction, person);
        if (source is not null)
        {
            if (order.Action == "harvest_food" &&
                FreeCarryCapacity(instruction.TargetAgentId) < FoodHarvestCarryUnits(source))
                return null;
            if (order.Action == "seek_food")
                return new CognitionCandidate("seek_food", "Travel to the food site named by this order.", 0, source.Id);
            return IsWithinInteractionRange(person.Position, source.Position, ResourceInteractionRange)
                ? new CognitionCandidate("harvest_food", "Gather food from the requested known site.", 0, source.Id)
                : new CognitionCandidate("seek_food", "Travel to the requested known food site.", 0, source.Id);
        }

        if (KnownTargetIsUnavailable(instruction) || !CanExploreForOrder(person))
            return null;
        return new CognitionCandidate("explore", "Explore nearby terrain to discover a suitable food site.", 0);
    }

    private static string FoodArrivalReceipt(string resourceId, GridPoint position) =>
        DerivedId.Create($"arrival:{resourceId}:{position.X}:{position.Y}");

    private bool MatchesFoodArrivalReceipt(string? receipt, MapResource resource)
    {
        if (receipt is null) return false;
        // A receipt records the arrival tile. Recompute the small interaction area so a
        // hashed identity still excludes the same source after the person moves away.
        for (var x = resource.Position.X - ResourceInteractionRange; x <= resource.Position.X + ResourceInteractionRange; x++)
            for (var y = resource.Position.Y - ResourceInteractionRange; y <= resource.Position.Y + ResourceInteractionRange; y++)
            {
                var position = new GridPoint(x, y);
                if (IsWithinInteractionRange(position, resource.Position, ResourceInteractionRange) &&
                    receipt == FoodArrivalReceipt(resource.Id, position)) return true;
            }
        return false;
    }

    private MapResource? KnownFoodSourceForOrder(OwnerQueuedInstruction instruction, AgentState person)
    {
        var order = instruction.Task!;
        return map.Resources
            .Where(resource => resource.Kind is "food" or "fruit" &&
                resources.GetValueOrDefault(resource.Id) == ResourceState.Available &&
                (order.TargetResourceId is null || resource.Id == order.TargetResourceId) &&
                !(order.RepeatUntilCancelled && order.Action == "seek_food" &&
                    MatchesFoodArrivalReceipt(order.LastEffectId, resource)) &&
                (order.TargetFoodKind is null || FoodKnowledgeKind(resource) == order.TargetFoodKind) &&
                (order.TargetPosition is null || resource.Position == order.TargetPosition.Value) &&
                (IsWithinInteractionRange(person.Position, resource.Position, ResourceInteractionRange) ||
                 knowledge.Facts.Any(fact => fact.OwnerId == instruction.TargetAgentId &&
                    fact.Position == resource.Position &&
                    fact.ResourceKinds.Contains(FoodKnowledgeKind(resource), StringComparer.Ordinal))) &&
                CanReachByFootOrSwimming(instruction.TargetAgentId, person.Position, resource.Position))
            .OrderBy(resource => map.FootDistance(person.Position, resource.Position))
            .ThenBy(resource => resource.Id, StringComparer.Ordinal)
            .FirstOrDefault(resource => IsWithinInteractionRange(person.Position, resource.Position, ResourceInteractionRange) ||
                FindUnoccupiedRoute(instruction.TargetAgentId, person.Position, resource.Position, ResourceInteractionRange).Count > 0);
    }

    private bool KnownTargetIsUnavailable(OwnerQueuedInstruction instruction)
    {
        var order = instruction.Task!;
        if (order.TargetResourceId is { } resourceId)
        {
            var resource = map.Resources.SingleOrDefault(item => item.Id == resourceId);
            return resource is null || knowledge.Facts.Any(fact => fact.OwnerId == instruction.TargetAgentId &&
                fact.Position == resource.Position &&
                fact.ResourceKinds.Contains(FoodKnowledgeKind(resource), StringComparer.Ordinal));
        }
        if (order.TargetPosition is { } position)
            return !map.Contains(position) || knowledge.Facts.Any(fact =>
                fact.OwnerId == instruction.TargetAgentId && fact.Position == position);
        return false;
    }

    private bool CanExploreForOrder(AgentState person)
    {
        if (NeedsUrgentFood(person) || NeedsUrgentWarmth(person)) return false;
        if (person.Exploration?.OutingPath.Count > 0)
            return HasWarmthForOuting(person);
        return person.FullnessBasisPoints >= OutingFullnessReserve && HasWarmthForOuting(person) &&
            map.FootNeighbors(person.Position).Any(map.IsPassable);
    }

    private bool ShouldInterruptOrder(
        AgentState person,
        OwnerQueuedInstruction instruction,
        CognitionCandidate? orderCandidate,
        CognitionCandidate? urgentCandidate)
    {
        if (IsShelterOrder(instruction.Task!.Action))
            return NeedsUrgentFood(person) && urgentCandidate is not null && IsFoodSurvivalCandidate(person.AgentId, urgentCandidate.Id) ||
                orderCandidate is null && urgentCandidate is not null;
        if (NeedsUrgentWarmth(person))
            return urgentCandidate is not null &&
                orderCandidate?.Id is not ("wear_clothing" or "tend_fire" or "seek_warmth");
        if (!NeedsUrgentFood(person)) return false;
        if (urgentCandidate is null) return false;
        return orderCandidate is null || orderCandidate.Id != urgentCandidate.Id ||
            orderCandidate.DestinationId != urgentCandidate.DestinationId;
    }

    private CognitionCandidate? UrgentSurvivalCandidateFor(
        string agentId, AgentState person, OwnerQueuedInstruction? instruction = null) =>
        SelectOrderSurvivalCandidate(CreateCandidates(agentId, person, restrictForOrder: false), person, instruction);

    private CognitionCandidate? SelectOrderSurvivalCandidate(
        IEnumerable<CognitionCandidate> candidates, AgentState person, OwnerQueuedInstruction? instruction)
    {
        var available = candidates.Where(candidate => IsSurvivalCandidate(person.AgentId, candidate.Id))
            .OrderBy(candidate => candidate.DeterministicPriority)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal).ToArray();
        if (instruction?.Task is { } order && IsShelterOrder(order.Action) && NeedsUrgentFood(person) &&
            available.FirstOrDefault(candidate => IsFoodSurvivalCandidate(person.AgentId, candidate.Id)) is { } food)
            return food;
        return available.FirstOrDefault();
    }

    private bool IsFoodSurvivalCandidate(string actor, string candidateId) => candidateId is
        "consume_food" or "drink_milk" or "collect_shared_food" or "take_food_from_pot" or "make_room_for_food" or
        "harvest_food" or "seek_food" || candidateId.StartsWith(TownProjectReturnPrefix, StringComparison.Ordinal) ||
        NeedsUrgentFood(agents[actor]) &&
        (IsShopFoodCandidate(actor, candidateId) || IsMarketFoodCandidate(actor, candidateId));

    private bool IsSurvivalCandidate(string actor, string candidateId) =>
        IsFoodSurvivalCandidate(actor, candidateId) || candidateId is
        "recover_household_delivery" or "wear_clothing" or "tend_fire" or "seek_warmth";

    private void ExecuteOrderStep(
        OwnerQueuedInstruction instruction,
        AgentState person,
        CognitionCandidate candidate)
    {
        var order = instruction.Task!;
        SetOrderStatus(instruction, OrderStatus.Doing, null, waitForDecision: false);
        var actor = instruction.TargetAgentId;
        RecordAttemptedAction(actor, candidate.Id);
        if (candidate.Id != "explore" && person.Exploration?.Goal?.OrderInstructionId == instruction.InstructionId)
            person = CompleteExplorationGoal(actor, person);
        // An order step interrupts timed repair work, as any other chosen action does.
        if (order.Action != "repair_equipment" && agents[actor].Equipment?.Repair is not null)
        {
            CancelEquipmentRepair(actor);
            person = agents[actor];
        }
        if (order.Action == "accept_guardianship")
        {
            ExecuteGuardianOrder(instruction, candidate);
            return;
        }
        if (IsCartOrder(order.Action))
        {
            ExecuteCartOrderStep(instruction, person);
            return;
        }
        if (IsAnimalOrder(order.Action))
        {
            ExecuteAnimalOrder(instruction);
            return;
        }
        if (order.Action == "travel_by_boat")
        {
            ExecuteBoatOrder(instruction);
            return;
        }
        if (IsTreePlantingOrder(order.Action))
        {
            ExecuteTreePlantingOrder(instruction, person);
            return;
        }
        switch (candidate.Id)
        {
            case "read_knowledge":
                ExecuteKnowledgeReadOrder(instruction);
                return;
            case "talk_to":
            case "propose_marriage":
                ExecuteTalkOrderStep(instruction, person);
                return;
            case "write_knowledge":
            case "copy_knowledge":
                ExecuteKnowledgeOrderStep(instruction, person);
                return;
            case "seek_shelter":
            case "tend_fire":
            case "inspect_shelter_site":
                ExecuteShelterOrderStep(instruction, person);
                return;
            case "construct_building":
                ExecuteConstructionOrderStep(instruction, person);
                return;
            case "expand_building":
                ExecuteExpansionOrderStep(instruction, person);
                return;
            case "deliver_stock":
                ExecuteDeliveryOrderStep(instruction, person);
                return;
            case "produce_item":
                ExecuteProductionOrderStep(instruction, person);
                return;
            case "work_field":
                ExecuteFieldOrderStep(instruction, person);
                return;
            case "repair_tool":
                ExecuteToolRepairOrderStep(instruction, person);
                return;
            case "repair_equipment":
                ExecuteRepairOrderStep(instruction, person);
                return;
            case "collect_material":
            case "collect_food":
            case "collect_equipment":
            case "collect_goods":
                ExecuteCollectionOrderStep(instruction, person);
                return;
            case "store_material":
            case "store_equipment":
            case "store_goods":
                ExecuteStorageOrderStep(instruction, person);
                return;
            case "return_borrowed":
                ExecuteReturnOrderStep(instruction, person);
                return;
            case "gather_material":
            case "inspect_material_site":
                ExecuteMaterialOrderStep(instruction, person, candidate.Id);
                return;
            case "move_to" when order.Action == "move_to" && order.TargetPosition is { } destination:
                MoveToward(actor, person, destination, "owner_order_move");
                // Only the tile reached joins the agent's small map memory. Recording
                // every step of a long walk would fill it and leave no room for what
                // exploration finds later.
                if (agents[actor].Position == destination)
                {
                    RecordKnowledgeFact(actor, destination);
                    CreditOrderEffect(instruction, Identity.Parse(MovementOrderEffectId(destination)), 1);
                }
                return;
            case "consume_food":
                if (ConsumeFood(actor, person, order.TargetFoodKind) is { } consumedLotId)
                {
                    var receipt = ConsumptionOrderReceipt(WorldTick, actor, consumedLotId);
                    CreditOrderEffect(instruction, Identity.Parse(receipt), 1);
                }
                else
                    SetOrderStatus(instruction, OrderStatus.Blocked, "No matching food is available to eat.");
                return;
            case "collect_shared_food":
                CollectSharedFood(actor, person, order.TargetFoodKind);
                return;
            case "explore":
                agents[actor] = person;
                Explore(actor, person, ExplorationGoalForOrder(instruction), replaceGoal: true);
                return;
            case "seek_food":
                {
                    var target = KnownFoodSourceForOrder(instruction, agents[actor]);
                    if (target is null)
                    {
                        SetOrderStatus(instruction, OrderStatus.Blocked, OrderBlockedReason(instruction, agents[actor]));
                        return;
                    }
                    var current = agents[actor];
                    if (IsWithinInteractionRange(current.Position, target.Position, ResourceInteractionRange))
                    {
                        if (order.Action == "seek_food")
                            CreditOrderEffect(instruction,
                                Identity.Parse(FoodArrivalReceipt(target.Id, current.Position)), 1);
                        return;
                    }
                    MoveToward(actor, current, target.Position, "owner_order_food", ResourceInteractionRange);
                    current = agents[actor];
                    if (order.Action == "seek_food" &&
                        IsWithinInteractionRange(current.Position, target.Position, ResourceInteractionRange))
                        CreditOrderEffect(instruction,
                            Identity.Parse(FoodArrivalReceipt(target.Id, current.Position)), 1);
                    return;
                }
            case "harvest_food":
                {
                    var source = KnownFoodSourceForOrder(instruction, agents[actor]);
                    if (source is null)
                    {
                        SetOrderStatus(instruction, OrderStatus.Blocked, OrderBlockedReason(instruction, agents[actor]));
                        return;
                    }
                    if (!IsWithinInteractionRange(agents[actor].Position, source.Position, ResourceInteractionRange))
                    {
                        MoveToward(actor, agents[actor], source.Position, "owner_order_harvest", ResourceInteractionRange);
                        return;
                    }
                    if (HarvestFood(actor, agents[actor], source) is { } effect &&
                        (order.TargetFoodKind is null || effect.ItemKind == order.TargetFoodKind))
                        CreditOrderEffect(instruction, Identity.Parse(FoodHarvestReceipt(effect.LotId)),
                            order.ProgressUnit == "harvests" ? 1 : effect.Quantity);
                    else
                        SetOrderStatus(instruction, OrderStatus.Blocked, "The requested food site did not yield the requested food.");
                    return;
                }
            default:
                SetOrderStatus(instruction, OrderStatus.Blocked, "The requested task is not available right now.");
                return;
        }
    }

    private void CreditOrderEffect(OwnerQueuedInstruction instruction, Identity effectId, int units)
    {
        var current = instructionsByIdempotency[instruction.IdempotencyKey];
        var order = current.Task!;
        if (units <= 0 || order.LastEffectId == effectId || !IsActiveOrder(order.Status)) return;
        var completed = Math.Min(1_000_000, order.CompletedUnits + units);
        var finished = !order.RepeatUntilCancelled && completed >= order.RequestedUnits;
        instructionsByIdempotency[current.IdempotencyKey] = current with
        {
            Task = (order with
            {
                CompletedUnits = completed,
                LastEffectId = effectId,
            }).Transition(finished ? OrderStatus.Finished : OrderStatus.Doing),
        };
        if (finished)
        {
            AppendEvent("instruction_applied", $"{current.InstructionId}:{order.Action}");
            AppendEvent("instruction_order_finished", $"{current.TargetAgentId}:{current.InstructionId}:{effectId}");
        }
        else
        {
            AppendEvent("instruction_order_progress", $"{current.TargetAgentId}:{current.InstructionId}:{completed}");
        }
        checkpointSchemaVersion = StateSchemaVersion;
    }

    private void SetOrderStatus(
        OwnerQueuedInstruction instruction,
        OrderStatus status,
        string? reason,
        bool waitForDecision = false)
    {
        var current = instructionsByIdempotency[instruction.IdempotencyKey];
        if (current.Task is not { } order || !IsActiveOrder(order.Status)) return;
        if (status == OrderStatus.Interrupted && BoundProductionJob(current) is { } production)
            PauseProductionOrderJob(current, production);
        if (status == OrderStatus.Interrupted)
        {
            PauseConstructionForOrder(current);
            PauseExpansionForOrder(current);
        }
        if (order.LifecycleStatus == status && order.BlockedReason == reason &&
            order.WaitForDecisionAfterFailure == waitForDecision) return;
        instructionsByIdempotency[current.IdempotencyKey] = current with
        {
            Task = order.Transition(status, reason, waitForDecision),
        };
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent("instruction_order_status", $"{current.TargetAgentId}:{current.InstructionId}:{status.ToWireValue()}");
    }

    private string OrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person)
    {
        if (instruction.Task?.Action == "travel_by_boat") return BoatOrderBlockedReason(instruction);
        if (instruction.Task?.Action == "read_knowledge")
            return KnowledgeReadOrderBlocker(instruction) ?? "Waiting to read the written item.";
        if (instruction.Task is { } conversationOrder && IsConversationOrder(conversationOrder.Action)) return TalkOrderBlocker(instruction, person) ?? "Waiting for the conversation outcome.";
        if (instruction.Task is { } treeOrder && IsTreePlantingOrder(treeOrder.Action))
            return TreePlantingOrderBlockedReason(instruction, person);
        if (instruction.Task is { } knowledgeOrder && IsKnowledgeOrder(knowledgeOrder.Action)) return KnowledgeOrderBlockedReason(instruction);
        if (instruction.Task is { } cartOrder && IsCartOrder(cartOrder.Action))
            return CartOrderBlocker(instruction, person) ?? "Waiting for the selected cart action.";
        if (instruction.Task is { } animalOrder && IsAnimalOrder(animalOrder.Action))
            return AnimalOrderBlockedReason(instruction);
        if (instruction.Task is { } protective && IsShelterOrder(protective.Action))
            return ShelterOrderBlockedReason(instruction, person);
        if (instruction.Task?.Action == "construct_building")
            return ConstructionOrderBlockedReason(instruction, person);
        if (instruction.Task?.Action == "expand_building")
            return ExpansionOrderBlockedReason(instruction);
        if (instruction.Task?.Action == "deliver_stock")
            return DeliveryOrderBlockedReason(instruction, person);
        if (instruction.Task?.Action == "produce_item")
            return ProductionOrderBlockedReason(instruction);
        if (instruction.Task is { } fieldOrder && IsFieldOrder(fieldOrder.Action))
            return FieldOrderBlockedReason(instruction);
        if (instruction.Task?.Action == "repair_tool")
            return ToolRepairOrderBlockedReason(instruction);
        if (instruction.Task?.Action == "repair_equipment")
            return RepairOrderBlockedReason(instruction);
        if (instruction.Task?.Action is "collect_material" or "collect_food" or "collect_equipment" or "collect_goods")
            return CollectionOrderBlockedReason(instruction, person);
        if (instruction.Task?.Action is "store_material" or "store_equipment" or "store_goods")
            return StorageOrderBlockedReason(instruction, person);
        if (instruction.Task?.Action == "return_borrowed")
            return ReturnOrderBlockedReason(instruction, person);
        if (instruction.Task?.Action == "gather_material")
            return MaterialOrderBlockedReason(instruction, person);

        if (instruction.Task?.Action == "move_to")
            return instruction.Task.TargetPosition is not { } destination || !map.Contains(destination)
                ? "The requested tile is outside this world."
                : MovementOrderNeedsHouseInvitation(instruction.TargetAgentId, destination)
                    ? "Waiting for an invitation to enter another household's House."
                : "No open walking or safe swimming route reaches the requested tile right now.";
        if (instruction.Task is { Action: "accept_guardianship", TargetAgentId: { } child })
            return GuardianOrderBlockedReason(instruction.TargetAgentId, child);
        if (instruction.Task?.Action == "consume_food")
        {
            if (person.FullnessBasisPoints >= ComfortableFullness)
                return "Waiting until hungry enough to eat.";
            if (FreeCarryCapacity(instruction.TargetAgentId) == 0 &&
                AvailableSharedFood(instruction.TargetAgentId, instruction.Task.TargetFoodKind) is not null)
                return "Carrying space is full; make room before collecting food from the household store.";
            return "No matching food is available to eat right now.";
        }
        if (instruction.Task?.Action == "harvest_food" &&
            KnownFoodSourceForOrder(instruction, person) is { } source &&
            FreeCarryCapacity(instruction.TargetAgentId) < FoodHarvestCarryUnits(source))
            return "Carrying space is full; make room before gathering from the requested food site.";
        if (instruction.Task?.TargetPosition is not null || instruction.Task?.TargetResourceId is not null)
            return "The requested food site isn't known or reachable right now.";
        return !CanExploreForOrder(person)
            ? "The agent needs to be fed and warm before searching for food."
            : "No suitable food source is known yet.";
    }

    private static string MovementOrderEffectId(GridPoint destination) =>
        FormattableString.Invariant($"arrival:tile:{destination.X}:{destination.Y}");

    private bool MovementOrderNeedsHouseInvitation(string actor, GridPoint destination) =>
        BuildingsWithTag("house").Any(house => house.HouseholdId is not null &&
            house.HouseholdId != society.Checkpoint.GetAgent(actor).HouseholdId &&
            !HasHouseGuestInvitation(actor, house.InstanceId) &&
            WorldContentSimulationRules.Footprint(
                worldContent.Buildings.Single(definition => definition.CanonicalId == house.DefinitionId), house).Contains(destination));
}
