using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using System.Collections.ObjectModel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class OrderSystem
{
    internal static GridPoint? TreeOrderSite(ITreePlantingOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        if (instruction.Task!.TargetPosition is not { } site)
            return world.PlantingSite(instruction.TargetAgentId, person.Position);
        if (!world.Map.Contains(site) || world.Towns.Any(town => town.BorderTiles.Contains(site)) ||
            world.PlantingSiteRefusal(site, world.PlantingObstacles()) is not null || world.TreeAreaFull(site)) return null;
        return world.IsWithinInteractionRange(person.Position, site, world.ResourceInteractionRange) ||
            world.Reach(instruction.TargetAgentId, site, ReachPurpose.TreePlanting, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? site : null;
    }


    internal static string TreeOrderSpecies(ITreePlantingOrdersWorld world, OrderTask order, GridPoint site) => order.Action switch
    {
        "plant_broadleaf" => TreeGrowthRules.Broadleaf,
        "plant_conifer" => TreeGrowthRules.Conifer,
        "plant_orchard" => TreeGrowthRules.Orchard,
        _ => world.PlantingSpecies(site),
    };

    internal static InventoryLot? CarriedTreeOrderSeed(ITreePlantingOrdersWorld world, string actor, string kind) => world.Society.Inventory.Lots
        .Where(lot => lot.OwnerId == actor && lot.ItemKind == kind && PersonalEquipmentRules.IsCarried(lot, actor) &&
            lot.ContainerLotId is null && lot.DeliveryBuildingId is null && world.PlantingSeedQuantity(lot) > 0)
        .OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();

    internal static InventoryLot? CollectableTreeOrderSeed(ITreePlantingOrdersWorld world, string actor, string kind, GridPoint site) => world.RecoverablePersonalGoods(actor, kind).Where(lot => world.PlantingSeedQuantity(lot) > 0 &&
            world.PickupCarryCapacity(actor, lot, site, world.ResourceInteractionRange) >= 1)
            .OrderBy(lot => world.Map.FootDistance(world.Agents[actor].Position, world.HouseholdStockPosition(lot)))
            .ThenBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault() ?? world.SharedItem(kind, actor, site, world.ResourceInteractionRange);

    internal static TreeOrderPlan? TreePlantingOrderPlan(ITreePlantingOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (!world.IsAdult(actor) || !world.ReadyForBriefInteraction(actor) || world.TreeOrderSite(instruction, person) is not { } site) return null;
        var species = world.TreeOrderSpecies(instruction.Task!, site);
        var kind = TreeGrowthRules.SeedItem(species);
        if (world.CarriedTreeOrderSeed(actor, kind) is { } carried) return new(site, species, carried, true);
        return world.FreeCarryCapacity(actor) > 0 && world.CollectableTreeOrderSeed(actor, kind, site) is { } stock
            ? new(site, species, stock, false) : null;
    }


    internal static CognitionCandidate? TreePlantingOrderCandidate(ITreePlantingOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person) => world.TreePlantingOrderPlan(instruction, person) is { } plan
            ? new(instruction.Task!.Action, plan.Carried
                ? "Walk to the requested planting site and plant one sapling using your actual seed."
                : "Collect a permitted planting seed before travelling to the planting site.", 0, plan.Seed.Id)
            : null;

    internal static void ExecuteTreePlantingOrder(ITreePlantingOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (world.TreePlantingOrderPlan(instruction, person) is not { } plan)
        {
            world.SetOrderStatus(instruction, OrderStatus.Blocked, world.TreePlantingOrderBlockedReason(instruction, person));
            return;
        }
        if (!plan.Carried)
        {
            if (plan.Seed.OwnerId == actor) _ = world.CollectPersonalGoods(actor, plan.Seed.Id, 1);
            else world.CollectEquipment(actor, person, TreeGrowthRules.SeedItem(plan.Species), plan.Site, world.ResourceInteractionRange);
            return;
        }
        if (!world.IsWithinInteractionRange(person.Position, plan.Site, world.ResourceInteractionRange))
        {
            world.MoveToward(actor, person, plan.Site, "owner_order_tree_planting", world.ResourceInteractionRange);
            return;
        }
        var result = world.PlantTreeCore(actor, plan.Species, plan.Seed.Id, plan.Site);
        if (result.Planted) world.CreditOrderEffect(instruction, Identity.Parse(world.TreePlantingReceipt(result.TreeId!)), 1);
        else world.SetOrderStatus(instruction, OrderStatus.Blocked, "The planting site or seed is no longer available; the seed was kept.");
    }


    internal static string TreePlantingOrderBlockedReason(ITreePlantingOrdersWorld world, OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (!world.IsAdult(actor)) return "This agent is too young to plant trees.";
        if (!world.ReadyForBriefInteraction(actor)) return "The agent needs warmth before planting trees.";
        if (instruction.Task!.TargetPosition is { } site)
        {
            if (!world.Map.Contains(site)) return "The requested planting tile is outside this world.";
            if (world.Towns.Any(town => town.BorderTiles.Contains(site))) return "Plant new trees outside Town borders.";
            if (world.PlantingSiteRefusal(site, world.PlantingObstacles()) is not null)
                return "The requested planting tile needs empty grass or forest ground, clear of buildings, Roads and existing objects.";
            if (world.TreeAreaFull(site)) return "The requested planting area has reached its tree and resource limit.";
        }
        if (world.TreeOrderSite(instruction, person) is not { } destination)
            return "No open walking route reaches a free planting site outside the Town.";
        var kind = TreeGrowthRules.SeedItem(world.TreeOrderSpecies(instruction.Task!, destination));
        if (world.CarriedTreeOrderSeed(actor, kind) is not null) return "The planting site is temporarily unavailable.";
        if (world.FreeCarryCapacity(actor) <= 0) return "Make carrying space to collect a planting seed.";
        if (world.RecoverablePersonalGoods(actor, kind).Any() || world.SharedItem(kind, actor) is not null)
            return "No route can bring a seed to the planting site with the current load; make carrying space or use another reachable seed.";
        return $"Carry an available {kind.Replace('_', ' ')} or leave one in permitted reachable stock; reserved and foreign seeds cannot be used.";
    }

}
