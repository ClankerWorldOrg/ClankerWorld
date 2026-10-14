using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class FarmingSystem
{
    private static ActorCapabilityPolicy TreePolicy => new(ActionLifeStages.Adults,
        AllowUrgentFood: false, AllowUrgentWarmth: true, AllowActiveOrder: true);

    internal static TreeActionPlan? PlanTreeAction(IFarmingWorld world, string actor, TreeChoice choice,
        GridPoint? selectedSite = null, MapResource? selectedStump = null)
    {
        if (!ActorCapabilities.Check(world.CapabilitiesFor(actor), TreeActionPlan.Candidate(choice), TreePolicy).Allowed) return null;
        var origin = world.Agents[actor].Position;
        var stump = choice == TreeChoice.Replant ? selectedStump ?? ReplantableTree(world, actor, origin) : null;
        var position = choice == TreeChoice.Replant ? stump?.Position : selectedSite ?? PlantingSite(world, actor, origin);
        if (position is not { } site) return null;
        var kind = choice == TreeChoice.Orchard ? TreeGrowthRules.OrchardSeedItem : TreeGrowthRules.TreeSeedItem;
        var seed = CarriedPlantingSeed(world, actor, kind) ?? world.SharedItem(kind, actor, site, 1);
        if (seed is null) return null;
        var claim = seed.OwnerId == actor ? OrchardClaim(world, seed) : null;
        return new(actor, choice, site, stump?.TreeKind ?? (choice == TreeChoice.Orchard ? TreeGrowthRules.Orchard : PlantingSpecies(world, site)),
            stump?.Id, new(seed.Id, seed.OwnerId, kind, seed.OwnerId == actor ? origin : world.HouseholdStockPosition(seed),
                seed.OwnerId == actor ? 0 : WorldRuntime.FarmingSharedRules.HouseholdStockInteractionRange(seed)), claim,
            world.Reach(actor, site, ReachPurpose.TreePlanting, 1, 0, SwimmingPolicy.WhenCapable));
    }

    private static PlannedOrchardClaim? OrchardClaim(IFarmingWorld world, InventoryLot seed)
    {
        var claim = WorldRuntime.OrchardPlantingClaim(InventoryIndex.For(world.Society.Inventory), seed, world.WorldTick);
        return claim is null ? null : new(claim.Id, claim.Quantity);
    }

    internal static ActionCheck CheckTreePlan(IFarmingWorld world, TreeActionPlan plan)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId, TreePolicy);
        if (!capability.Allowed) return capability;
        var seed = plan.Seed;
        var use = seed.OwnerId == plan.Actor ? GoodsUse.TreePlanting : GoodsUse.Collect;
        if (world.RecheckGoods(new(use, plan.Actor, GoodsOwners.One(seed.OwnerId), GoodsKinds.One(seed.ItemKind)), seed.StockId) is not { Lot: var lot } ||
            seed.OwnerId == plan.Actor && OrchardClaim(world, lot) != plan.Claim ||
            seed.OwnerId != plan.Actor && (lot.ContainerLotId is not null || lot.CarrierId is not null ||
                world.HouseholdStockPosition(lot) != seed.Position || WorldRuntime.FarmingSharedRules.HouseholdStockInteractionRange(lot) != seed.InteractionRange ||
                world.PickupCarryCapacity(plan.Actor, lot, plan.Position, 1) < 1))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected planting seed is no longer available");
        if (plan.Choice == TreeChoice.Replant)
        {
            var stump = world.Map.Resources.SingleOrDefault(item => item.Id == plan.StumpId);
            var growth = world.Environment.Ecology.Resources.SingleOrDefault(item => item.Id == plan.StumpId);
            if (stump is null || stump.Position != plan.Position || stump.TreeKind != plan.Species || !stump.IsRenewable ||
                growth is null || growth.Quantity != 0 || growth.IsPlanted)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected stump no longer needs replanting");
        }
        else if (world.Towns.Any(town => (town.BorderTiles ?? []).Contains(plan.Position)) ||
            PlantingSiteRefusal(world, plan.Position, PlantingObstacles(world)) is not null || TreeAreaFull(world, plan.Position))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected planting ground is no longer available");
        return world.Reach(plan.Actor, plan.Position, ReachPurpose.TreePlanting, 1, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected planting site is out of reach now");
    }

    internal static void ApplyTreePlan(IFarmingWorld world, TreeActionPlan plan)
    {
        if (plan.Seed.OwnerId != plan.Actor)
        {
            world.PickUpEquipment(plan.Actor, plan.Seed);
            return;
        }
        var state = world.Agents[plan.Actor];
        if (!world.IsWithinInteractionRange(state.Position, plan.Position, 1))
        {
            world.MoveToward(plan.Actor, state, plan.Position, plan.Choice == TreeChoice.Replant ? "replant_tree" : "plant_tree", 1);
            return;
        }
        if (plan.Choice == TreeChoice.Replant)
            CompleteTreeReplanting(world, plan.Actor, world.Map.GetResource(plan.StumpId!), world.Society.Inventory.GetLot(plan.Seed.StockId));
        else PlantTreeCore(world, plan.Actor, plan.Species, plan.Seed.StockId, plan.Position);
    }
}
