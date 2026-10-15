using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static bool ShelterBuildingKnown(IShelterOrdersWorld world, string actor, AgentState person, PlacedBuilding building) =>
        building.HouseholdId is { } owner &&
            (owner == world.Society.GetAgent(actor).HouseholdId || world.HasHouseGuestInvitation(actor, building.InstanceId)) ||
        world.IsTownHallStormRefuge(actor, building) ||
        world.IsWithinInteractionRange(person.Position, building.Position, world.ResourceInteractionRange);

    internal static bool ShelterBuildingCovers(IShelterOrdersWorld world, string actor, PlacedBuilding building, GridPoint point) =>
        !world.IsSwimming(actor, point) && (building.HouseholdId is null && !world.IsTownHall(building)
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
        if (BoundShelterBuilding(world, actor, order) is not { } building) return false;
        return order.Action == "tend_fire"
            ? !world.IsFireLit(building) && world.IsWithinInteractionRange(binding.Position, building.Position,
                building.HouseholdId is null ? world.ResourceInteractionRange : 0)
            : ShelterBuildingCovers(world, actor, building, binding.Position);
    }

    internal static OwnerShelterBinding? SelectShelterBinding(IShelterOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var order = instruction.Task!;
        if (order.ShelterBinding is { } bound) return bound;
        var buildings = (order.Action == "tend_fire" ? world.AccessibleHeatingBuildings(actor) : world.AccessibleShelters(actor))
            .Where(building => ShelterBuildingKnown(world, actor, person, building))
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
                if (order.Action == "tend_fire" ? requested != building.Position : !ShelterBuildingCovers(world, actor, building, requested)) continue;
                position = requested;
            }
            else if (order.Action == "seek_shelter" && ShelterBuildingCovers(world, actor, building, person.Position))
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

    internal static bool CanInspectShelterTarget(IShelterOrdersWorld world, OrderTask order, string actor, AgentState person) =>
        order.ShelterBinding is null && order.TargetPosition is { } target && world.Map.Contains(target) &&
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
        world.FreeCarryCapacity(actor) > 0 && world.SharedItem("wood", actor, returnTo) is not null || KnownOrderFirewoodSource(world, actor, person, returnTo) is not null;

    internal static CognitionCandidate? ShelterOrderCandidateFor(IShelterOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        if (world.Survival is null) return null;
        var order = instruction.Task!;
        var binding = SelectShelterBinding(world, instruction, person);
        if (binding is null)
            return CanInspectShelterTarget(world, order, instruction.TargetAgentId, person)
                ? new CognitionCandidate("inspect_shelter_site", "Travel to observe the shelter site named by this order.", 0) : null;
        if (!ShelterBindingIsAvailable(world, instruction.TargetAgentId, OrderStore.PrepareShelterBinding(order, binding)) ||
            !world.CanReachLeg(instruction.TargetAgentId, person.Position, binding.Position, ReachPurpose.WarmthProtection, 0, 0, SwimmingPolicy.WhenCapable) ||
            order.Action == "tend_fire" && !CanObtainFirewood(world, instruction.TargetAgentId, person, binding.Position)) return null;
        return new CognitionCandidate(order.Action,
            order.Action == "seek_shelter" ? "Reach the requested permitted shelter." : "Light one permitted hearth using your own wood.",
            0, binding.BuildingInstanceId);
    }

    internal static void ExecuteShelterOrderStep(IShelterOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        var current = world.Orders.Instructions[instruction.IdempotencyKey];
        var order = current.Task!;
        var binding = SelectShelterBinding(world, current, person);
        if (binding is null)
        {
            if (CanInspectShelterTarget(world, order, actor, person))
            {
                world.MoveToward(actor, person, order.TargetPosition!.Value, "owner_order_shelter_site", world.ResourceInteractionRange);
                var reached = world.Agents[actor].Position;
                if (reached != person.Position) world.RecordKnowledgeFact(actor, reached);
                if (world.IsWithinInteractionRange(reached, order.TargetPosition.Value, world.ResourceInteractionRange))
                    world.RecordKnowledgeFact(actor, order.TargetPosition.Value);
            }
            else world.SetOrderStatus(current, "blocked", ShelterOrderBlockedReason(world, current, person));
            return;
        }
        if (order.ShelterBinding is null)
        {
            order = OrderStore.PrepareShelterBinding(order, binding);
            current = OrderStore.PrepareTaskBinding(current, order);
            world.Orders.RecordShelterOrderStep(current.IdempotencyKey, current);
            world.MarkCheckpointCurrent();
        }
        if (!ShelterBindingIsAvailable(world, actor, order) || !world.CanReachLeg(actor, person.Position, binding.Position, ReachPurpose.WarmthProtection, 0, 0, SwimmingPolicy.WhenCapable))
        {
            world.SetOrderStatus(current, "blocked", ShelterOrderBlockedReason(world, current, person));
            return;
        }
        if (order.Action == "tend_fire" && !world.HasCarriedOwnItem(actor, "wood"))
        {
            if (world.FreeCarryCapacity(actor) > 0 && world.SharedItem("wood", actor, binding.Position) is not null)
                world.CollectEquipment(actor, person, "wood", binding.Position);
            else if (KnownOrderFirewoodSource(world, actor, person, binding.Position) is { } source)
            {
                _ = world.GatherProjectMaterial(actor, person, "wood", source, returnTo: binding.Position);
                var reached = world.Agents[actor].Position;
                if (world.IsWithinInteractionRange(reached, source.Position, world.ResourceInteractionRange))
                    world.RecordKnowledgeFact(actor, source.Position);
            }
            else world.SetOrderStatus(current, "blocked", ShelterOrderBlockedReason(world, current, person));
            return;
        }
        if (person.Position != binding.Position)
            world.MoveToward(actor, person, binding.Position, order.Action == "tend_fire" ? "fuel_fire" : "warmth");
        if (world.Agents[actor].Position != binding.Position || !ShelterBindingIsAvailable(world, actor, order)) return;
        string? payment = null;
        if (order.Action == "tend_fire")
        {
            var effect = world.TendFireAt(actor, world.Agents[actor], BoundShelterBuilding(world, actor, order)!);
            if (effect is null) return;
            payment = effect.FuelReservationId;
        }
        var completion = OrderStore.CreateOwnerShelterCompletion(world.WorldTick, world.Agents[actor].Position, payment);
        current = world.Orders.Instructions[current.IdempotencyKey];
        current = OrderStore.PrepareTaskBinding(current, OrderStore.PrepareShelterCompletion(current.Task!, completion));
        world.Orders.RecordShelterOrderStep(current.IdempotencyKey, current);
        world.CreditOrderEffect(current, world.ShelterOrderReceipt(current.InstructionId, current.Task!), 1);
    }

    internal static string ShelterOrderBlockedReason(IShelterOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var order = instruction.Task!;
        if (!world.MayFollowOwnerOrder(instruction.TargetAgentId, order))
            return order.Action == "tend_fire" ? "This agent is too young to light a fire." : "This agent needs a caregiver to reach shelter.";
        if (world.Survival is null) return "Shelter and fire care are not available in this world yet.";
        if (order.ShelterBinding is not null && !ShelterBindingIsAvailable(world, instruction.TargetAgentId, order))
            return order.Action == "tend_fire" ? "The chosen hearth is unavailable, changed, or already burning." : "The chosen shelter is no longer available or permitted.";
        if (order.Action == "tend_fire" && !CanObtainFirewood(world, instruction.TargetAgentId, person))
            return "No usable owned firewood can be carried to the hearth right now.";
        return order.TargetPosition is not null ? "The requested shelter site is not permitted or reachable right now." :
            "No known permitted shelter is reachable right now.";
    }
}
