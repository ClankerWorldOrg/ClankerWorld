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

}
