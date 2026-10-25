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
            order = order.UpdateTypedDefinition<ConstructBuildingOrderDefinition>(task =>
                OrderStore.PrepareExecuteConstructionOrderStepConstructBuildingOrderDefinition(task, household, position, started, world.ConstructionInstanceId(instruction.InstructionId)));
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
    internal static bool ShelterBuildingKnown(IShelterOrdersWorld world, string actor, AgentState person, PlacedBuilding building) => building.HouseholdId is { } owner &&
            (owner == world.Society.GetAgent(actor).HouseholdId || world.HasHouseGuestInvitation(actor, building.InstanceId)) ||
        world.IsTownHallStormRefuge(actor, building) ||
        world.IsWithinInteractionRange(person.Position, building.Position, world.ResourceInteractionRange);

    internal static bool ShelterBuildingCovers(IShelterOrdersWorld world, string actor, PlacedBuilding building, GridPoint point) => !world.IsSwimming(actor, point) && (building.HouseholdId is null && !world.IsTownHall(building)
            ? world.IsWithinInteractionRange(point, building.Position, world.ResourceInteractionRange)
            : WorldContentSimulationRules.Footprint(world.Content.Buildings.Single(item => item.CanonicalId == building.DefinitionId), building).Contains(point));

    internal static PlacedBuilding? BoundShelterBuilding(IShelterOrdersWorld world, string actor, OrderTask order)
    {
        if (order.ShelterBinding is not { Kind: "building" } binding) return null;
        var available = order.Action == "tend_fire" ? world.AccessibleHeatingBuildings(actor) : world.AccessibleShelters(actor);
        return available.FirstOrDefault(building => building.InstanceId == binding.BuildingInstanceId &&
            building.DefinitionId == binding.DefinitionId && building.HouseholdId == binding.OwnerId &&
            building.Position == binding.BuildingPosition && building.PlacedTick == binding.BuildingPlacedTick &&
            (order.TargetBuildingKind != "house" || building.HouseholdId == world.Society.GetAgent(actor).HouseholdId &&
                world.Content.Buildings.Single(item => item.CanonicalId == building.DefinitionId).Tags.Contains("house", StringComparer.Ordinal)));
    }


    internal static bool ShelterBindingIsAvailable(IShelterOrdersWorld world, string actor, OrderTask order)
    {
        if (order.ShelterBinding is not { } binding) return false;
        if (binding.Kind == "natural") return order.Action == "seek_shelter" &&
            world.WeatherAt(binding.Position) == WeatherKind.Storm && world.NaturalStormCover(binding.Position);
        if (world.BoundShelterBuilding(actor, order) is not { } building) return false;
        return order.Action == "tend_fire"
            ? !world.IsFireLit(building) && world.IsWithinInteractionRange(binding.Position, building.Position,
                building.HouseholdId is null ? world.ResourceInteractionRange : 0)
            : world.ShelterBuildingCovers(actor, building, binding.Position);
    }


    internal static OwnerShelterBinding? SelectShelterBinding(IShelterOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        if (order.ShelterBinding is { } bound) return bound;
        var buildings = (order.Action == "tend_fire" ? world.AccessibleHeatingBuildings(actor) : world.AccessibleShelters(actor))
            .Where(building => world.ShelterBuildingKnown(actor, person, building))
            .Where(building => order.TargetBuildingKind != "house" ||
                building.HouseholdId == world.Society.GetAgent(actor).HouseholdId &&
                world.Content.Buildings.Single(item => item.CanonicalId == building.DefinitionId).Tags.Contains("house", StringComparer.Ordinal))
            .Where(building => order.Action != "tend_fire" || !world.IsFireLit(building))
            .OrderBy(building => world.Map.FootDistance(person.Position, building.Position))
            .ThenBy(building => building.InstanceId, StringComparer.Ordinal);
        var choices = new List<OwnerShelterBinding>();
        foreach (var building in buildings)
        {
            GridPoint? position;
            if (order.TargetPosition is { } requested)
            {
                if (order.Action == "tend_fire" ? requested != building.Position : !world.ShelterBuildingCovers(actor, building, requested)) continue;
                position = requested;
            }
            else if (order.Action == "seek_shelter" && world.ShelterBuildingCovers(actor, building, person.Position))
                position = person.Position;
            else if (world.IsTownHall(building))
                position = world.ReachableHallShelterPoint(actor, person.Position, building);
            else if (building.HouseholdId is null)
                position = world.Map.FootNeighbors(building.Position).Append(building.Position)
                    .Where(point => world.Map.IsPassable(point) && world.CanReachLeg(actor, person.Position, point, ReachPurpose.WarmthProtection, 0, 0, SwimmingPolicy.WhenCapable))
                    .OrderBy(point => world.Map.FootDistance(person.Position, point)).ThenBy(point => point.Y).ThenBy(point => point.X)
                    .Select(point => (GridPoint?)point).FirstOrDefault();
            else
                position = building.Position;
            if (position is not { } destination || !world.CanReachLeg(actor, person.Position, destination, ReachPurpose.WarmthProtection, 0, 0, SwimmingPolicy.WhenCapable)) continue;
            choices.Add(OrderStore.CreateOwnerShelterBinding("building", destination, building.InstanceId, building.DefinitionId, building.HouseholdId, building.Position, building.PlacedTick));
        }
        if (order.Action == "seek_shelter" && order.TargetBuildingKind is null)
        {
            var natural = order.TargetPosition is { } requested
                ? world.IsWithinInteractionRange(person.Position, requested, world.ResourceInteractionRange) &&
                    world.WeatherAt(requested) == WeatherKind.Storm && world.NaturalStormCover(requested)
                    ? requested : (GridPoint?)null
                : world.WeatherAt(person.Position) == WeatherKind.Storm
                    ? world.NaturalStormCover(person.Position) ? person.Position : world.NearbyNaturalStormCover(actor, person.Position)
                    : null;
            if (natural is { } cover && world.WeatherAt(cover) == WeatherKind.Storm && world.CanReachLeg(actor, person.Position, cover, ReachPurpose.WarmthProtection, 0, 0, SwimmingPolicy.WhenCapable))
                choices.Add(OrderStore.CreateOwnerShelterBinding("natural", cover));
        }
        return choices.OrderBy(choice => world.Map.FootDistance(person.Position, choice.Position))
            .ThenBy(choice => choice.Kind, StringComparer.Ordinal)
            .ThenBy(choice => choice.BuildingInstanceId, StringComparer.Ordinal).FirstOrDefault();
    }


    internal static bool CanInspectShelterTarget(IShelterOrdersWorld world, OrderTask order, string actor, AgentState person) => order.ShelterBinding is null && order.TargetPosition is { } target && world.Map.Contains(target) &&
        !world.IsWithinInteractionRange(person.Position, target, world.ResourceInteractionRange) &&
        world.Reach(actor, target, ReachPurpose.WarmthProtection, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0;

    internal static MapResource? KnownOrderFirewoodSource(IShelterOrdersWorld world, string actor, AgentState person,
        GridPoint? returnTo = null)
    {
        var known = world.Knowledge.Facts.Where(fact => fact.OwnerId == actor &&
                fact.ResourceKinds.Any(kind => kind is "wood" or "construction"))
            .Select(fact => fact.Position).ToHashSet();
        var toolCache = new Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>();
        return world.Map.Resources.Where(source => (source.Kind is "wood" or "construction") &&
                world.Resources.GetValueOrDefault(source.Id) == ResourceState.Available &&
                (known.Contains(source.Position) || world.IsWithinInteractionRange(person.Position, source.Position, world.ResourceInteractionRange)))
            .OrderBy(source => world.Map.FootDistance(person.Position, source.Position)).ThenBy(source => source.Id, StringComparer.Ordinal)
            .FirstOrDefault(source => world.CanGatherFromSource(actor, "wood", source, toolCache) &&
                world.FreeCarryCapacity(actor) > 0 &&
                (world.ProjectMaterialHarvest(actor, "wood", source) is not { } harvest ||
                    world.FreeCarryCapacity(actor) >= harvest.Quantity + harvest.TreeSeedQuantity) &&
                world.CanReachLeg(actor, person.Position, source.Position, ReachPurpose.MaterialGathering, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable) &&
                (returnTo is null || world.CanReturnWithHarvest(actor, "wood", source, returnTo.Value)));
    }


    internal static bool CanObtainFirewood(IShelterOrdersWorld world, string actor, AgentState person, GridPoint? returnTo = null) => world.HasCarriedOwnItem(actor, "wood") ||
        world.FreeCarryCapacity(actor) > 0 && world.SharedItem("wood", actor, returnTo) is not null || world.KnownOrderFirewoodSource(actor, person, returnTo) is not null;

    internal static CognitionCandidate? ShelterOrderCandidateFor(IShelterOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        if (world.SurvivalState is null) return null;
        var order = instruction.Task!;
        var binding = world.SelectShelterBinding(instruction, person);
        if (binding is null)
            return world.CanInspectShelterTarget(order, instruction.TargetAgentId, person)
                ? new CognitionCandidate("inspect_shelter_site", "Travel to observe the shelter site named by this order.", 0) : null;
        if (!world.ShelterBindingIsAvailable(instruction.TargetAgentId, order.UpdateDefinition(definition => definition switch
{
    SeekShelterOrderDefinition task => OrderStore.PrepareShelterOrderCandidateForSeekShelterOrderDefinition(task, binding),
    TendFireOrderDefinition task => OrderStore.PrepareShelterOrderCandidateForTendFireOrderDefinition(task, binding),
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
})) ||
            !world.CanReachLeg(instruction.TargetAgentId, person.Position, binding.Position, ReachPurpose.WarmthProtection, 0, 0, SwimmingPolicy.WhenCapable) ||
            order.Action == "tend_fire" && !world.CanObtainFirewood(instruction.TargetAgentId, person, binding.Position)) return null;
        return new CognitionCandidate(order.Action,
            order.Action == "seek_shelter" ? "Reach the requested permitted shelter." : "Light one permitted hearth using your own wood.",
            0, binding.BuildingInstanceId);
    }


    internal static void ExecuteShelterOrderStep(IShelterOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        var order = current.Task!;
        var binding = world.SelectShelterBinding(current, person);
        if (binding is null)
        {
            if (world.CanInspectShelterTarget(order, actor, person))
            {
                world.MoveToward(actor, person, order.TargetPosition!.Value, "owner_order_shelter_site", world.ResourceInteractionRange);
                var reached = world.Agents[actor].Position;
                if (reached != person.Position) world.RecordKnowledgeFact(actor, reached);
                if (world.IsWithinInteractionRange(reached, order.TargetPosition.Value, world.ResourceInteractionRange))
                    world.RecordKnowledgeFact(actor, order.TargetPosition.Value);
            }
            else world.SetOrderStatus(current, OrderStatus.Blocked, world.ShelterOrderBlockedReason(current, person));
            return;
        }
        if (order.ShelterBinding is null)
        {
            order = order.UpdateDefinition(definition => definition switch
{
    SeekShelterOrderDefinition task => OrderStore.PrepareShelterOrderCandidateForSeekShelterOrderDefinition(task, binding),
    TendFireOrderDefinition task => OrderStore.PrepareShelterOrderCandidateForTendFireOrderDefinition(task, binding),
    _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
});
            current = OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(current, order);
            world.Orders.RecordShelterOrderStep(current.IdempotencyKey, current);
            world.MarkCheckpointCurrent();
        }
        if (!world.ShelterBindingIsAvailable(actor, order) || !world.CanReachLeg(actor, person.Position, binding.Position, ReachPurpose.WarmthProtection, 0, 0, SwimmingPolicy.WhenCapable))
        {
            world.SetOrderStatus(current, OrderStatus.Blocked, world.ShelterOrderBlockedReason(current, person));
            return;
        }
        if (order.Action == "tend_fire" && !world.HasCarriedOwnItem(actor, "wood"))
        {
            if (world.FreeCarryCapacity(actor) > 0 && world.SharedItem("wood", actor, binding.Position) is not null)
                world.CollectEquipment(actor, person, "wood", binding.Position);
            else if (world.KnownOrderFirewoodSource(actor, person, binding.Position) is { } source)
            {
                _ = world.GatherProjectMaterial(actor, person, "wood", source, returnTo: binding.Position);
                var reached = world.Agents[actor].Position;
                if (world.IsWithinInteractionRange(reached, source.Position, world.ResourceInteractionRange))
                    world.RecordKnowledgeFact(actor, source.Position);
            }
            else world.SetOrderStatus(current, OrderStatus.Blocked, world.ShelterOrderBlockedReason(current, person));
            return;
        }
        if (person.Position != binding.Position)
            world.MoveToward(actor, person, binding.Position, order.Action == "tend_fire" ? "fuel_fire" : "warmth");
        if (world.Agents[actor].Position != binding.Position || !world.ShelterBindingIsAvailable(actor, order)) return;
        string? payment = null;
        if (order.Action == "tend_fire")
        {
            var effect = world.TendFireAt(actor, world.Agents[actor], world.BoundShelterBuilding(actor, order)!);
            if (effect is null) return;
            payment = effect.FuelReservationId;
        }
        var completion = OrderStore.CreateOwnerShelterCompletion(world.WorldTick, world.Agents[actor].Position, Identity.ParseOptional(payment));
        current = world.Orders.Instructions[current.IdempotencyKey];
        current = OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(current, current.Task!.UpdateDefinition(definition => definition switch
            {
                SeekShelterOrderDefinition task => OrderStore.PrepareExecuteShelterOrderStepSeekShelterOrderDefinition(task, completion),
                TendFireOrderDefinition task => OrderStore.PrepareExecuteShelterOrderStepTendFireOrderDefinition(task, completion),
                _ => throw new InvalidOperationException("This order kind does not own the requested fields."),
            })
);
        world.Orders.RecordShelterOrderStep(current.IdempotencyKey, current);
        world.CreditOrderEffect(current, Identity.Parse(world.ShelterOrderReceipt(current.InstructionId, current.Task!)), 1);
    }


    internal static string ShelterOrderBlockedReason(IShelterOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var order = instruction.Task!;
        if (!world.MayFollowOwnerOrder(instruction.TargetAgentId, order))
            return order.Action == "tend_fire" ? "This agent is too young to light a fire." : "This agent needs a caregiver to reach shelter.";
        if (world.SurvivalState is null) return "Shelter and fire care are not available in this world yet.";
        if (order.ShelterBinding is not null && !world.ShelterBindingIsAvailable(instruction.TargetAgentId, order))
            return order.Action == "tend_fire" ? "The chosen hearth is unavailable, changed, or already burning." : "The chosen shelter is no longer available or permitted.";
        if (order.Action == "tend_fire" && !world.CanObtainFirewood(instruction.TargetAgentId, person))
            return "No usable owned firewood can be carried to the hearth right now.";
        return order.TargetPosition is not null ? "The requested shelter site is not permitted or reachable right now." :
            "No known permitted shelter is reachable right now.";
    }
    internal static CognitionCandidate? RepairOrderCandidateFor(IRepairOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (person.Equipment?.Repair is { } repair)
            return new("repair_equipment", repair.OrderInstructionId == instruction.InstructionId
                ? "Finish repairing the requested personal item with the reserved materials."
                : "End the previous repair and release its unused materials before starting this task.", 0, repair.LotId);
        var target = world.WornEquipmentItems(actor)
            .Where(lot => lot.ItemKind == instruction.Task!.TargetEquipmentKind)
            .FirstOrDefault(lot => world.CanPrepareEquipmentRepair(actor, person, lot));
        return target is null ? null : new("repair_equipment", "Collect the materials and repair the requested personal item at your household's work site.", 0, target.Id);
    }


    internal static void ExecuteRepairOrderStep(IRepairOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (person.Equipment?.Repair is { } repair)
        {
            if (repair.OrderInstructionId == instruction.InstructionId)
            {
                if (world.ContinueEquipmentRepair(actor) is { } effect)
                {
                    var receipt = world.EquipmentRepairReceipt(actor, effect.StartedTick, effect.LotId);
                    world.CreditOrderEffect(instruction, Identity.Parse(receipt), 1);
                }
                return;
            }
            world.CancelEquipmentRepair(actor);
            person = world.Agents[actor];
        }
        if (world.RepairOrderCandidateFor(instruction, person)?.DestinationId is not { } lotId)
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.RepairOrderBlockedReason(instruction));
            return;
        }
        world.RepairEquipment(actor, person, lotId, instruction.InstructionId);
    }


    internal static string RepairOrderBlockedReason(IRepairOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        var actor = instruction.TargetAgentId;
        if (!world.MayFollowOwnerOrder(actor, instruction.Task!))
            return "This agent is too young to repair equipment.";
        var kind = instruction.Task!.TargetEquipmentKind!;
        var targets = world.WornEquipmentItems(actor).Where(lot => lot.ItemKind == kind).ToArray();
        if (targets.Length == 0)
            return "No matching worn personal item is available; carry an unreserved single item that needs repair.";
        if (targets.All(lot => world.EquipmentRepairSite(actor, lot) is null))
            return kind == "basket" ? "The agent needs their household's House to repair a basket." :
                "The agent needs their household's Tailor Shop to repair this item.";
        if (targets.All(lot => world.MissingRepairInputUnits(actor, lot) > world.FreeCarryCapacity(actor)))
            return "Make carrying space for the repair materials.";
        if (targets.All(lot => PersonalEquipmentRules.RepairMaterials(lot.ItemKind).Any(input =>
                !world.HasCarriedOwnItem(actor, input.ResourceId) && world.SharedItem(input.ResourceId, actor) is null)))
            return "The repair needs available materials in the agent's hands or household stock.";
        return "The agent and repair materials need an open route to the work site; make carrying space if the trip needs swimming.";
    }


    internal static void CancelRepairForOrder(IRepairOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        if (world.Agents.TryGetValue(instruction.TargetAgentId, out var person) &&
            person.Equipment?.Repair?.OrderInstructionId == instruction.InstructionId)
            world.CancelEquipmentRepair(instruction.TargetAgentId);
    }
    internal static CognitionCandidate? ProductionOrderCandidateFor(IProductionOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var order = instruction.Task!;
        var recipe = WorldProductionOrderCatalog.Find(world.Content, order.TargetRecipeId)?.Recipe;
        if (recipe is null || !world.ReadyForBriefInteraction(instruction.TargetAgentId)) return null;
        if (world.BoundProductionJob(instruction) is { State: WorldProductionJobState.Running or WorldProductionJobState.Paused } job)
            return world.ProductionOrderBuilding(instruction, recipe) is null ? null :
                new("produce_item", "Continue the ordered production at its reserved workstation.", 0, job.BuildingInstanceId);
        if (!world.TryFindRecipeSite(recipe, out var buildingId, out _, instruction.TargetAgentId)) return null;
        return new("produce_item", "Prepare and complete the requested recipe using real workstation inputs.", 0, buildingId);
    }


    internal static PlacedBuilding? ProductionOrderBuilding(IProductionOrdersWorld world, OwnerQueuedInstruction instruction, RecipeDefinition recipe)
    {
        var order = instruction.Task!;
        var actor = instruction.TargetAgentId;
        var building = world.Simulation.Buildings.FirstOrDefault(item => item.InstanceId == order.ProductionBuildingId);
        if (building is null || building.DefinitionId != recipe.WorkstationBuildingId ||
            order.TargetPosition is { } target && building.Position != target ||
            building.HouseholdId is { } household && world.Society.GetAgent(actor).HouseholdId != household)
            return null;
        var definition = world.Content.Buildings.FirstOrDefault(item => item.CanonicalId == building.DefinitionId);
        return definition is null || building.HouseholdId is null && definition.Tags.Any(world.IsHouseholdBuildingTag)
            ? null : building;
    }


    internal static WorldProductionJob? BoundProductionJob(IProductionOrdersWorld world, OwnerQueuedInstruction instruction) => instruction.Task is { Action: "produce_item", ProductionJobId: { } id } order
            ? world.Simulation.ProductionJobs.FirstOrDefault(job => job.JobId == id &&
                job.OrderInstructionId == instruction.InstructionId &&
                job.WorkerId == instruction.TargetAgentId && job.RecipeId == order.TargetRecipeId &&
                job.BuildingInstanceId == order.ProductionBuildingId && job.StartedTick >= order.ProductionProjectStartedTick)
            : null;

    internal static void ExecuteProductionOrderStep(IProductionOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        instruction = world.Orders.Instructions[instruction.IdempotencyKey];
        var order = instruction.Task!;
        if (world.NeedsUrgentFood(person) || world.NeedsUrgentWarmth(person))
        {
            world.SetOrderStatus(instruction, OrderStatus.Interrupted, "Food or warmth needs come first.");
            return;
        }
        var entry = WorldProductionOrderCatalog.Find(world.Content, order.TargetRecipeId);
        if (entry is null || world.ProductionOrderCandidateFor(instruction, person)?.DestinationId is not { } buildingId)
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.ProductionOrderBlockedReason(instruction));
            return;
        }

        if (world.BoundProductionJob(instruction) is { State: WorldProductionJobState.Running or WorldProductionJobState.Paused } job)
        {
            var building = world.ProductionOrderBuilding(instruction, entry.Recipe)!;
            if (person.Position != building.Position)
            {
                world.PauseProductionOrderJob(instruction, job);
                world.MoveToward(actor, person, building.Position, "project", 0);
                return;
            }
            if (job.State == WorldProductionJobState.Paused)
            {
                if (!world.CanResumeHeldInputs(actor, job.BuildingInstanceId, job.InputReservationIds) ||
                    !world.TryResumeKnife(actor, job.JobId, out var knifeId))
                {
                    world.SetOrderStatus(instruction, OrderStatus.Blocked, "The reserved ingredients or production tool are no longer available.");
                    return;
                }
                var deadline = checked(world.WorldTick + job.CompletionTick - job.PausedAtTick!.Value);
                world.ExtendOrderReservations(job.InputReservationIds, deadline);
                world.ResumeProductionJob(job.JobId, deadline, knifeId);
                world.AppendEvent(new ProductionOrderResumedEventPayload(null, instruction.InstructionId, job.JobId));
            }
            return;
        }

        if (!world.IsProductionOrderProject(instruction, person.Project) ||
            person.Project!.Stage is "completed" or "cancelled" || person.Project.JobId is not null)
        {
            var project = new AgentProject(IdentityRules.PrefixedReference("build:recipe:", entry.Recipe.CanonicalId),
                entry.Recipe.DisplayName, world.WorldTick, "acquiring", LastTransitionTick: world.WorldTick);
            order = order.UpdateTypedDefinition<ProduceItemOrderDefinition>(task =>
                OrderStore.PrepareExecuteProductionOrderStepProduceItemOrderDefinition(task, buildingId, null, world.WorldTick));
            instruction = OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(instruction, order);
            world.Orders.RecordProductionOrderStep(instruction.IdempotencyKey, instruction);
            world.PhysicalAgents.RecordProject(actor, person, project);
            world.MarkCheckpointCurrent();
            world.AppendEvent(new ProjectChosenEventPayload(actor, project.CandidateId));
        }
        if (world.Agents[actor].Project is { RequiresFreshChoice: true } pausedProject)
            world.SetProject(actor, pausedProject with { Stage = "acquiring", RequiresFreshChoice = false, Blocker = null });
        world.ContinueProject(actor, world.Agents[actor]);
        var currentProject = world.Agents[actor].Project!;
        if (currentProject.JobId is { } startedJobId && world.Simulation.ProductionJobs.Any(job =>
                job.JobId == startedJobId && job.OrderInstructionId == instruction.InstructionId &&
                job.WorkerId == actor && job.RecipeId == order.TargetRecipeId &&
                job.BuildingInstanceId == order.ProductionBuildingId && job.StartedTick == world.WorldTick))
        {
            var current = world.Orders.Instructions[instruction.IdempotencyKey];
            world.Orders.RecordProductionOrderStep(instruction.IdempotencyKey, OrderStore.PrepareExecuteBoatOrderOwnerQueuedInstruction(current, current.Task!.UpdateTypedDefinition<ProduceItemOrderDefinition>(task =>
                OrderStore.PrepareExecuteProductionOrderStepProduceItemOrderDefinitionProductionJobId(task, startedJobId))));
        }
        if (currentProject.Stage is "blocked" or "cancelled")
            world.SetOrderStatus(instruction, OrderStatus.Blocked, currentProject.Blocker ?? "The requested production cannot continue yet.");
    }


    internal static string ProductionOrderBlockedReason(IProductionOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        if (!world.MayFollowOwnerOrder(instruction.TargetAgentId, instruction.Task!))
            return "This agent is too young to do workstation production.";
        if (WorldProductionOrderCatalog.Find(world.Content, instruction.Task!.TargetRecipeId) is null)
            return "The requested recipe is not active.";
        if (world.IsProductionOrderProject(instruction, world.Agents[instruction.TargetAgentId].Project) &&
            world.Agents[instruction.TargetAgentId].Project?.Blocker is { } blocker)
            return blocker;
        return "The requested workstation is unavailable, full, outside this household's access, or cannot be reached.";
    }


    internal static void MaintainProductionOrdersBeforeTick(IProductionOrdersWorld world)
    {
        foreach (var instruction in world.Orders.Instructions.Values.Where(item =>
                     item.Task is { Action: "produce_item" } order && world.IsActiveOrder(order.Status)).ToArray())
        {
            var job = world.BoundProductionJob(instruction);
            if (job is not { State: WorldProductionJobState.Running or WorldProductionJobState.Paused }) continue;
            var recipe = world.Content.Recipes.FirstOrDefault(item => item.CanonicalId == job.RecipeId);
            if (!world.Agents.TryGetValue(instruction.TargetAgentId, out var person) || recipe is null ||
                !world.IsAdult(instruction.TargetAgentId) || world.ProductionOrderBuilding(instruction, recipe) is not { } building ||
                !world.IsProductionOrderProject(instruction, person.Project))
            {
                world.CancelProductionForOrder(instruction);
                world.SetOrderStatus(instruction, OrderStatus.Blocked, "The worker or workstation for this order is no longer available.");
                continue;
            }
            if (world.NeedsUrgentFood(person) || world.NeedsUrgentWarmth(person))
            {
                world.PauseProductionOrderJob(instruction, job);
                world.SetOrderStatus(instruction, OrderStatus.Interrupted, "Food or warmth needs come first.");
            }
            else if (person.Position != building.Position || world.IsConversationBusy(person.AgentId) ||
                     instruction.Task!.WaitForDecisionAfterFailure)
                world.PauseProductionOrderJob(instruction, job);
        }
    }


    internal static void PauseProductionOrderJob(IProductionOrdersWorld world, OwnerQueuedInstruction instruction, WorldProductionJob job)
    {
        if (job.State != WorldProductionJobState.Running) return;
        world.HoldOrderReservations(job.InputReservationIds);
        world.PauseProductionJob(job.JobId, world.WorldTick);
        world.AppendEvent(new ProductionOrderPausedEventPayload(null, instruction.InstructionId, job.JobId));
    }


    internal static bool IsActiveProductionOrderJob(IProductionOrdersWorld world, string jobId) => world.Orders.Instructions.Values.Any(instruction =>
        instruction.Task is { Action: "produce_item" } order && world.IsActiveOrder(order.Status) &&
        order.ProductionJobId == jobId);

    internal static void CancelProductionForOrder(IProductionOrdersWorld world, OwnerQueuedInstruction instruction)
    {
        if (instruction.Task?.Action != "produce_item") return;
        if (world.BoundProductionJob(instruction) is { State: WorldProductionJobState.Running or WorldProductionJobState.Paused } job)
        {
            world.ReleaseProductionOrderInputs(job);
            world.CancelProductionJobs(new HashSet<string>(StringComparer.Ordinal) { job.JobId });
            world.AppendEvent(new RecipeCancelledEventPayload(job.WorkerId, job.JobId, job.RecipeId, null));
        }
        if (world.Agents.TryGetValue(instruction.TargetAgentId, out var person) &&
            world.IsProductionOrderProject(instruction, person.Project))
            world.SetProject(person.AgentId, person.Project! with { Stage = "cancelled", Blocker = "The production order stopped." });
    }


    internal static void CreditProductionOrderJob(IProductionOrdersWorld world, WorldProductionJob job, RecipeDefinition recipe)
    {
        var instruction = world.PendingInstructionFor(job.WorkerId);
        if (instruction?.Task is not { Action: "produce_item" } order || world.BoundProductionJob(instruction)?.JobId != job.JobId ||
            !world.Agents.TryGetValue(job.WorkerId, out var person) || !world.IsProductionOrderProject(instruction, person.Project)) return;
        var units = order.ProgressUnit == "production_batches" ? 1 :
            recipe.Outputs.Where(output => output.ResourceId == order.TargetOutputKind).Sum(output => output.Amount);
        var receipt = world.ProductionOrderReceipt(job.JobId);
        world.SetProject(job.WorkerId, person.Project! with { Stage = "completed", Blocker = null });
        world.CreditOrderEffect(instruction, Identity.Parse(receipt), units);
    }
    internal static CognitionCandidate? MaterialOrderCandidateFor(IMaterialOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        if (world.FreeCarryCapacity(actor) == 0) return null;
        var known = world.KnownMaterialOrderSources(instruction, person).ToArray();
        var toolCache = new Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>();
        var hasReachableSource = false;
        foreach (var source in known)
        {
            if (!world.CanReachLeg(actor, person.Position, source.Position, ReachPurpose.MaterialGathering, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable)) continue;
            hasReachableSource = true;
            if (!world.CanGatherFromSource(actor, order.TargetMaterialKind!, source, toolCache) ||
                world.ProjectMaterialHarvest(actor, order.TargetMaterialKind!, source) is { } plan &&
                world.FreeCarryCapacity(actor) < plan.Quantity + plan.TreeSeedQuantity)
                continue;
            return new CognitionCandidate("gather_material", "Gather the requested material using normal tools and carrying space.", 0, source.Id);
        }

        if (hasReachableSource || !world.CanExploreForOrder(person))
            return null;
        if (world.MaterialOrderTarget(instruction) is { } target)
        {
            if (!world.Map.Contains(target) || world.IsWithinInteractionRange(person.Position, target, world.ResourceInteractionRange) ||
                !world.CanReachLeg(actor, person.Position, target, ReachPurpose.MaterialGathering, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable))
                return null;
            return new CognitionCandidate("inspect_material_site", "Travel to observe the material site named by this order.", 0);
        }
        if (order.TargetResourceId is not null) return null;
        return new CognitionCandidate("explore", "Explore nearby terrain to discover the requested material.", 0);
    }


    internal static IEnumerable<MapResource> KnownMaterialOrderSources(IMaterialOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var order = instruction.Task!;
        var knownPositions = world.Knowledge.Facts.Where(fact => fact.OwnerId == instruction.TargetAgentId &&
                fact.ResourceKinds.Any(kind => kind == order.TargetMaterialKind ||
                    order.TargetMaterialKind == "wood" && kind == "construction"))
            .Select(fact => fact.Position).ToHashSet();
        return world.Map.Resources.Where(source =>
                OrderTaskParser.MatchesMaterial(source, order.TargetMaterialKind!) &&
                world.Resources.GetValueOrDefault(source.Id) == ResourceState.Available &&
                (order.TargetResourceId is null || source.Id == order.TargetResourceId) &&
                (order.TargetPosition is null || source.Position == order.TargetPosition) &&
                (knownPositions.Contains(source.Position) ||
                 world.IsWithinInteractionRange(person.Position, source.Position, world.ResourceInteractionRange)))
            .OrderBy(source => world.Map.FootDistance(person.Position, source.Position))
            .ThenBy(source => source.Id, StringComparer.Ordinal);
    }


    internal static GridPoint? MaterialOrderTarget(IMaterialOrdersWorld world, OwnerQueuedInstruction instruction) => instruction.Task!.TargetPosition ?? (instruction.Task.TargetResourceId is { } id
            ? world.Map.Resources.SingleOrDefault(source => source.Id == id)?.Position : null);

    internal static void ExecuteMaterialOrderStep(IMaterialOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person, string candidate)
    {
        var actor = instruction.TargetAgentId;
        if (candidate == "inspect_material_site")
        {
            if (world.MaterialOrderTarget(instruction) is { } target && world.Map.Contains(target))
            {
                world.MoveToward(actor, person, target, "owner_order_material_site", world.ResourceInteractionRange);
                // Retain the observed site rather than every walking step. The bounded
                // ledger accepts passable tiles, so false impassable sites add no fact.
                var reached = world.Agents[actor].Position;
                if (world.Map.IsPassable(target) && world.IsWithinInteractionRange(reached, target, world.ResourceInteractionRange))
                    world.RecordKnowledgeFact(actor, target);
            }
            return;
        }

        // Recheck the selected site's tools, room and route at execution time.
        var candidateNow = world.MaterialOrderCandidateFor(instruction, person);
        var source = candidateNow?.Id == "gather_material"
            ? world.Map.Resources.Single(item => item.Id == candidateNow.DestinationId) : null;
        if (source is null)
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.MaterialOrderBlockedReason(instruction, person));
            return;
        }
        if (world.Map.IsPassable(source.Position) && world.IsWithinInteractionRange(person.Position, source.Position, world.ResourceInteractionRange))
            world.RecordKnowledgeFact(actor, source.Position);
        var effect = world.GatherProjectMaterial(actor, person, instruction.Task!.TargetMaterialKind!, source);
        if (effect is not null)
        {
            var receipt = world.MaterialGatherReceipt(effect.LotId);
            world.CreditOrderEffect(instruction, Identity.Parse(receipt),
                instruction.Task.ProgressUnit == "harvests" ? 1 : effect.Quantity);
        }
    }


    internal static string MaterialOrderBlockedReason(IMaterialOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (!world.MayFollowOwnerOrder(actor, instruction.Task!))
            return "This agent is too young to gather these materials.";
        if (instruction.Task!.TargetPosition is { } target && !world.Map.Contains(target))
            return "The requested tile is outside this world.";
        if (world.FreeCarryCapacity(actor) == 0)
            return "Carrying space is full; make room before gathering materials.";
        var sources = world.KnownMaterialOrderSources(instruction, person).ToArray();
        if (sources.Length != 0)
        {
            var toolCache = new Dictionary<(ToolFamily Family, int Tier), ToolDefinition?>();
            var usable = sources.Where(source => world.CanGatherFromSource(actor, instruction.Task.TargetMaterialKind!, source, toolCache)).ToArray();
            if (usable.Length == 0)
                return "A usable gathering tool is needed; carry one or make a shared one accessible.";
            if (usable.All(source => world.ProjectMaterialHarvest(actor, instruction.Task.TargetMaterialKind!, source) is { } plan &&
                    world.FreeCarryCapacity(actor) < plan.Quantity + plan.TreeSeedQuantity))
                return "Carrying space is too small for a whole gathering load; make room first.";
            return "No open walking route reaches a suitable material source right now.";
        }
        if (world.MaterialOrderTarget(instruction) is { } site && world.IsWithinInteractionRange(person.Position, site, world.ResourceInteractionRange))
            return "The requested site has no available matching material right now.";
        return !world.CanExploreForOrder(person)
            ? "The agent needs to be fed and warm before searching for materials."
            : "The requested material site is not reachable right now.";
    }
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
