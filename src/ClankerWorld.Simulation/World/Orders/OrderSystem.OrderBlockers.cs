
namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
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
}
