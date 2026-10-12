using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private sealed record TreeOrderPlan(GridPoint Site, string Species, InventoryLot Seed, bool Carried);

    private static bool IsTreePlantingOrder(string action) =>
        action is "plant_tree" or "plant_broadleaf" or "plant_conifer" or "plant_orchard";

    private GridPoint? TreeOrderSite(OwnerQueuedInstruction instruction, AgentState person)
    {
        if (instruction.Task!.TargetPosition is not { } site)
            return PlantingSite(instruction.TargetAgentId, person.Position);
        if (!map.Contains(site) || towns.Any(town => town.BorderTiles.Contains(site)) ||
            PlantingSiteRefusal(site, PlantingObstacles()) is not null || TreeAreaFull(site)) return null;
        return IsWithinInteractionRange(person.Position, site, ResourceInteractionRange) ||
            FindUnoccupiedRoute(instruction.TargetAgentId, person.Position, site, ResourceInteractionRange).Count > 0
            ? site : null;
    }

    private string TreeOrderSpecies(OrderTask order, GridPoint site) => order.Action switch
    {
        "plant_broadleaf" => TreeGrowthRules.Broadleaf,
        "plant_conifer" => TreeGrowthRules.Conifer,
        "plant_orchard" => TreeGrowthRules.Orchard,
        _ => PlantingSpecies(site),
    };

    private InventoryLot? CarriedTreeOrderSeed(string actor, string kind) => society.Checkpoint.Inventory.Lots
        .Where(lot => lot.OwnerId == actor && lot.ItemKind == kind && PersonalEquipmentRules.IsCarried(lot, actor) &&
            lot.ContainerLotId is null && lot.DeliveryBuildingId is null && PlantingSeedQuantity(lot) > 0)
        .OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();

    private InventoryLot? CollectableTreeOrderSeed(string actor, string kind, GridPoint site) =>
        RecoverablePersonalGoods(actor, kind).Where(lot => PlantingSeedQuantity(lot) > 0 &&
            PickupCarryCapacity(actor, lot, site, ResourceInteractionRange) >= 1)
            .OrderBy(lot => map.FootDistance(agents[actor].Position, HouseholdStockPosition(lot)))
            .ThenBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault() ?? SharedItem(kind, actor, site, ResourceInteractionRange);

    private TreeOrderPlan? TreePlantingOrderPlan(OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (!IsAdult(actor) || !ReadyForBriefInteraction(actor) || TreeOrderSite(instruction, person) is not { } site) return null;
        var species = TreeOrderSpecies(instruction.Task!, site);
        var kind = TreeGrowthRules.SeedItem(species);
        if (CarriedTreeOrderSeed(actor, kind) is { } carried) return new(site, species, carried, true);
        return FreeCarryCapacity(actor) > 0 && CollectableTreeOrderSeed(actor, kind, site) is { } stock
            ? new(site, species, stock, false) : null;
    }

    private CognitionCandidate? TreePlantingOrderCandidate(OwnerQueuedInstruction instruction, AgentState person) =>
        TreePlantingOrderPlan(instruction, person) is { } plan
            ? new(instruction.Task!.Action, plan.Carried
                ? "Walk to the requested planting site and plant one sapling using your actual seed."
                : "Collect a permitted planting seed before travelling to the planting site.", 0, plan.Seed.Id)
            : null;

    private void ExecuteTreePlantingOrder(OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (TreePlantingOrderPlan(instruction, person) is not { } plan)
        {
            SetOrderStatus(instruction, "blocked", TreePlantingOrderBlockedReason(instruction, person));
            return;
        }
        if (!plan.Carried)
        {
            if (plan.Seed.OwnerId == actor) _ = CollectPersonalGoods(actor, plan.Seed.Id, 1);
            else CollectEquipment(actor, person, TreeGrowthRules.SeedItem(plan.Species), plan.Site, ResourceInteractionRange);
            return;
        }
        if (!IsWithinInteractionRange(person.Position, plan.Site, ResourceInteractionRange))
        {
            MoveToward(actor, person, plan.Site, "owner_order_tree_planting", ResourceInteractionRange);
            return;
        }
        var result = PlantTreeCore(actor, plan.Species, plan.Seed.Id, plan.Site);
        if (result.Planted) CreditOrderEffect(instruction, TreePlantingReceipt(result.TreeId!), 1);
        else SetOrderStatus(instruction, "blocked", "The planting site or seed is no longer available; the seed was kept.");
    }

    private string TreePlantingOrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person)
    {
        var actor = instruction.TargetAgentId;
        if (!IsAdult(actor)) return "This agent is too young to plant trees.";
        if (!ReadyForBriefInteraction(actor)) return "The agent needs warmth before planting trees.";
        if (instruction.Task!.TargetPosition is { } site)
        {
            if (!map.Contains(site)) return "The requested planting tile is outside this world.";
            if (towns.Any(town => town.BorderTiles.Contains(site))) return "Plant new trees outside Town borders.";
            if (PlantingSiteRefusal(site, PlantingObstacles()) is not null)
                return "The requested planting tile needs empty grass or forest ground, clear of buildings, Roads and existing objects.";
            if (TreeAreaFull(site)) return "The requested planting area has reached its tree and resource limit.";
        }
        if (TreeOrderSite(instruction, person) is not { } destination)
            return "No open walking route reaches a free planting site outside the Town.";
        var kind = TreeGrowthRules.SeedItem(TreeOrderSpecies(instruction.Task!, destination));
        if (CarriedTreeOrderSeed(actor, kind) is not null) return "The planting site is temporarily unavailable.";
        if (FreeCarryCapacity(actor) <= 0) return "Make carrying space to collect a planting seed.";
        if (RecoverablePersonalGoods(actor, kind).Any() || SharedItem(kind, actor) is not null)
            return "No route can bring a seed to the planting site with the current load; make carrying space or use another reachable seed.";
        return $"Carry an available {kind.Replace('_', ' ')} or leave one in permitted reachable stock; reserved and foreign seeds cannot be used.";
    }
}
