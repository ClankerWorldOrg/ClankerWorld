using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static bool BuildingIdentityIsReserved(IConstructionOrdersWorld world, string instanceId, string? constructionInstructionId = null) => instanceId.StartsWith("ordered-building-", StringComparison.Ordinal) &&
            (constructionInstructionId is null || world.ConstructionInstanceId(constructionInstructionId) != instanceId) ||
        (world.Simulation.ConstructionReceipts ?? []).Any(receipt => receipt.BuildingInstanceId == instanceId) ||
        world.Orders.Instructions.Values.Any(instruction => instruction.Task is { } order &&
            (order.ExpansionBinding?.BuildingInstanceId == instanceId ||
             order.ShelterBinding is { } shelter && shelter.BuildingInstanceId == instanceId &&
                 shelter.BuildingPlacedTick == world.WorldTick ||
             order.ConstructionInstanceId == instanceId && instruction.InstructionId != constructionInstructionId));

    internal static bool TryConstructionOrderSite(IConstructionOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person,
        out BuildingDefinition? definition, out string? household, out GridPoint position, out string failure)
    {
        var order = instruction.Task!;
        var actor = instruction.TargetAgentId;
        var catalog = WorldBuildingOrderCatalog.Find(world.Content, order.TargetDefinitionId);
        definition = catalog?.Definition;
        household = world.Society.GetAgent(actor).HouseholdId;
        position = default;
        failure = "The requested building design is no longer active.";
        if (definition is null || !WorldBuildingOrderCatalog.Supports("construct_building", catalog!.BuildingKind)) return false;
        failure = "Only an adult household member can construct this building.";
        if (!world.IsAdult(actor) || household is null) return false;
        failure = "The household this building was ordered for has changed.";
        if (order.ConstructionOwnerId is { } owner && owner != household) return false;
        failure = "Another project is already in progress; finish or stop it before starting this construction.";
        if (person.Project is { Stage: not ("completed" or "cancelled") } project &&
            !world.IsConstructionOrderProject(instruction, project) && !world.OrdinaryProjectCanYieldToOrder(project)) return false;
        var kind = world.HouseholdBuildingKind(definition);
        failure = "The Town already has enough shops of this kind for its residents.";
        if (!(world.IsConstructionOrderProject(instruction, person.Project) &&
              person.Project is { Stage: not ("completed" or "cancelled") }) &&
            !world.TownHasRoomForSharedShop(actor, kind)) return false;
        failure = "This household already holds the requested building.";
        if (world.HouseholdBuildingWithTag(household, kind!) is not null) return false;
        failure = "Only a household holding a Farmhouse can build a Silo.";
        if (kind == "silo" && world.FarmhouseForHousehold(household) is null) return false;
        failure = "The household needs a completed House before it can build an animal yard.";
        if (kind == AnimalContent.YardTag && world.HouseForHousehold(household) is null) return false;
        failure = "This building has no reachable inputs for its work.";
        if (!world.WorkBuildingHasInputs(actor, household, definition)) return false;
        failure = "Another household member is already planning this building.";
        var householdId = household;
        if (world.Agents.Values.Any(other => other.AgentId != actor &&
                other.Project is { Stage: not ("completed" or "cancelled") } otherProject &&
                world.Society.GetAgent(other.AgentId).HouseholdId == householdId &&
                TownConstructionCandidateIds.TryParse(otherProject.CandidateId, out var selection) && selection.IsBuilding &&
                world.Content.Buildings.Any(item => item.CanonicalId == selection.DefinitionId && world.HouseholdBuildingKind(item) == kind)))
            return false;
        var target = order.ConstructionPosition ?? order.TargetPosition;
        var layout = world.CreateTownLayoutContext(actor, target, definition);
        failure = target is null ? "No site where this building is allowed is free right now."
            : "The requested site isn't allowed for this building or can't be reached.";
        if (target is { } requested)
        {
            if (!TownLayoutService.TryEvaluateConstructionSite(layout, definition, requested, out _)) return false;
            position = requested;
        }
        else
        {
            var choices = TownLayoutService.RankConstructionSites(layout, definition);
            if (choices.Count == 0) return false;
            position = choices[0].Position;
        }
        failure = string.Empty;
        return true;
    }


    internal static CognitionCandidate? ConstructionOrderCandidateFor(IConstructionOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person) => world.ReadyForBriefInteraction(instruction.TargetAgentId) &&
        world.TryConstructionOrderSite(instruction, person, out _, out _, out var position, out _)
            ? new("construct_building", "Prepare and complete the requested household building at its selected site.",
                0, IdentityRules.PrefixedReference("build-site:", $"{position.X},{position.Y}")) : null;

    internal static string ConstructionOrderBlockedReason(IConstructionOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        if (!world.TryConstructionOrderSite(instruction, person, out _, out _, out _, out var failure)) return failure;
        return world.IsConstructionOrderProject(instruction, person.Project) && person.Project?.Blocker is { } blocker
            ? blocker : "The ordered construction is waiting for usable materials or access to its site.";
    }


    internal static void ExecuteConstructionOrderStep(IConstructionOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        instruction = world.Orders.Instructions[instruction.IdempotencyKey];
        var actor = instruction.TargetAgentId;
        if (world.NeedsUrgentFood(person) || world.NeedsUrgentWarmth(person))
        {
            world.SetOrderStatus(instruction, OrderStatus.Interrupted, "Food or warmth needs come first.");
            return;
        }
        if (!world.TryConstructionOrderSite(instruction, person, out var definition, out var household, out var position, out var failure))
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, failure);
            return;
        }
        if (!world.IsConstructionOrderProject(instruction, person.Project) || person.Project!.Stage == "cancelled")
        {
            var order = instruction.Task!;
            var started = order.ConstructionStartedTick ?? world.WorldTick;
            order = order.UpdateDefinition(definition => definition switch
{
    ConstructBuildingOrderDefinition task => OrderStore.PrepareExecuteConstructionOrderStepConstructBuildingOrderDefinition(task, household, position, started, world.ConstructionInstanceId(instruction.InstructionId)),
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
});
            instruction = OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(instruction, order);
            world.Orders.RecordConstructionOrderStep(instruction.IdempotencyKey, instruction);
            var project = new AgentProject(TownConstructionCandidateIds.Building(definition!.CanonicalId, position),
                definition.DisplayName, started, "acquiring", LastTransitionTick: world.WorldTick,
                OrderInstructionId: instruction.InstructionId);
            world.PhysicalAgents.RecordProject(actor, person, project);
            world.MarkCheckpointCurrent();
            world.AppendEvent(new ProjectChosenEventPayload(actor, project.CandidateId));
        }
        if (world.Agents[actor].Project is { Stage: "paused" } paused)
            world.SetProject(actor, paused with { Stage = "acquiring", Blocker = null, RequiresFreshChoice = false });
        world.ContinueProject(actor, world.Agents[actor]);
        if (world.Agents[actor].Project is { Stage: "blocked" or "cancelled" } blocked)
            world.SetOrderStatus(instruction, OrderStatus.Blocked, blocked.Blocker ?? "The ordered construction cannot continue yet.");
    }


    internal static void PauseConstructionForOrder(IConstructionOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        if (world.Agents.TryGetValue(instruction.TargetAgentId, out var person) &&
            world.IsConstructionOrderProject(instruction, person.Project) &&
            person.Project is { Stage: not ("completed" or "cancelled" or "paused") } project)
            world.SetProject(person.AgentId, project with { Stage = "paused", Blocker = "Food or warmth needs come first." });
    }


    internal static void CancelConstructionForOrder(IConstructionOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        if (world.Agents.TryGetValue(instruction.TargetAgentId, out var person) &&
            world.IsConstructionOrderProject(instruction, person.Project) &&
            person.Project is { Stage: not ("completed" or "cancelled") } project)
            world.SetProject(person.AgentId, project with { Stage = "cancelled", Blocker = "The construction order stopped." });
    }


    internal static void MaintainConstructionOrdersBeforeTick(IConstructionOrdersWorld world)
    {
        foreach (var instruction in world.Orders.Instructions.Values.Where(item =>
                     item.Task is { Action: "construct_building", ConstructionOwnerId: not null } order &&
                     world.IsActiveOrder(order.Status)).ToArray())
        {
            if (!world.Agents.TryGetValue(instruction.TargetAgentId, out var person) ||
                !world.IsAdult(instruction.TargetAgentId) ||
                world.Society.GetAgent(instruction.TargetAgentId).HouseholdId != instruction.Task!.ConstructionOwnerId)
            {
                world.CancelConstructionForOrder(instruction);
                world.SetOrderStatus(instruction, OrderStatus.Blocked, "The worker or household for this building is no longer available.");
            }
            else if (world.NeedsUrgentFood(person) || world.NeedsUrgentWarmth(person))
                world.SetOrderStatus(instruction, OrderStatus.Interrupted, "Food or warmth needs come first.");
        }
    }


    internal static OwnerQueuedInstruction? ConstructionInstructionForProject(IConstructionOrdersWorld world, string actor, AgentProject? project) => project?.OrderInstructionId is { } id
            ? world.Orders.Instructions.Values.FirstOrDefault(instruction => instruction.InstructionId == id &&
                instruction.TargetAgentId == actor && instruction.Task is { } order && world.IsActiveOrder(order.Status) &&
                world.IsConstructionOrderProject(instruction, project)) : null;

    internal static void RecordConstructionOrderCompletion(IConstructionOrdersWorld world, OwnerQueuedInstruction instruction, string materialOwner)
    {
        var order = instruction.Task!;
        var purpose = "building:" + order.ConstructionInstanceId;
        var receipt = new WorldConstructionReceipt(Identity.Parse(instruction.InstructionId), instruction.TargetAgentId,
            order.ConstructionInstanceId!, order.TargetDefinitionId!, order.ConstructionOwnerId!, materialOwner,
            order.ConstructionPosition!.Value, order.ConstructionStartedTick!.Value, world.WorldTick,
            world.Society.Inventory.Reservations.Where(item => item.Purpose == purpose)
                .Select(item => item.Id).Order().ToArray());
        world.RecordConstructionReceipt(receipt);
        world.CreditOrderEffect(instruction, Identity.Parse(world.ConstructionOrderReceipt(receipt.BuildingInstanceId)), 1);
    }

}
