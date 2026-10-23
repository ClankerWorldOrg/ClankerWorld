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

}
