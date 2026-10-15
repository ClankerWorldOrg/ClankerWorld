using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    internal static RecipeDefinition? HouseholdHandcartRecipe(IHandcartWorld world, string? household) =>
        household is not null && world.BlacksmithForHousehold(household) is { } blacksmith
            ? world.Recipes.FirstOrDefault(recipe =>
                recipe.WorkstationBuildingId == blacksmith.DefinitionId && ProductionSystem.IsHandcartRecipe(recipe))
            : null;

    internal static RecipeDefinition? HandcartToBuild(IHandcartWorld world, string actor)
    {
        var household = world.HouseholdFor(actor);
        var recipe = HouseholdHandcartRecipe(world, household);
        return recipe is not null && household is not null && MayBuildHandcart(world, actor, household, recipe) ? recipe : null;
    }

    internal static bool MayBuildHandcart(IHandcartWorld world, string actor, string household, RecipeDefinition recipe)
    {
        if (!world.IsAdult(actor) || !ProductionSystem.IsHandcartRecipe(recipe) || world.HandcartControlStock(actor).Any() ||
            world.ProductionJobs.Any(job => job.WorkerId == actor && job.RecipeId == recipe.CanonicalId &&
                job.State == WorldProductionJobState.Running)) return false;
        var shortfall = recipe.Inputs.Select(input => (Kind: input.ResourceId, Missing: input.Amount -
                CarriedMaterialQuantity(world, actor, input.ResourceId) - HouseholdMaterialQuantity(world, actor, household, input.ResourceId)))
            .Where(item => item.Missing > 0).ToArray();
        // Town Warehouse stock needs a route, so it is counted last: first without routes, then one route per Warehouse.
        return shortfall.All(item => world.WarehouseStockLots(actor, item.Kind).Sum(world.AvailableLotQuantity) >= item.Missing) &&
            shortfall.All(item => ReachableWarehouseStockCovers(world, actor, item.Kind, item.Missing));
    }

    internal static int CarriedMaterialQuantity(IHandcartWorld world, string actor, string kind) =>
        world.FindGoods(new(GoodsUse.Holdings, actor, GoodsOwners.One(actor), GoodsKinds.One(kind))).Matches
            .Where(match => ToolProgressionRules.IsTopLevelCarriedLot(match.Lot, actor)).Sum(match => match.Quantity);

    internal static int HouseholdMaterialQuantity(IHandcartWorld world, string actor, string household, string kind) =>
        world.FindGoods(new GoodsRequest(GoodsUse.ReachableHoldings, actor, GoodsOwners.One(household), GoodsKinds.One(kind)))
            .Matches.Where(match => match.Lot.CarrierId is null && match.Lot.ContainerLotId is null)
            .Sum(match => match.Quantity);

    internal static bool ReachableWarehouseStockCovers(IHandcartWorld world, string actor, string kind, int missing)
    {
        var found = 0;
        foreach (var match in world.WarehouseStockGoods(actor, kind, GoodsUse.ReachableHoldings))
        {
            found += match.Quantity;
            if (found >= missing) return true;
        }
        return false;
    }

    internal static int SpareCarriedQuantity(IHandcartWorld world, string actor, InventoryLot lot)
    {
        var kept = lot.OwnerId == actor && ToolProgressionRules.IsTopLevelCarriedLot(lot, actor) &&
            HouseholdHandcartRecipe(world, world.HouseholdFor(actor)) is { } cart
            ? cart.Inputs.Where(input => input.ResourceId == lot.ItemKind).Sum(input => input.Amount) : 0;
        if (kept == 0 || HandcartToBuild(world, actor) is null) return world.AvailableLotQuantity(lot);
        return Math.Min(world.AvailableLotQuantity(lot), Math.Max(0, CarriedMaterialQuantity(world, actor, lot.ItemKind) - kept));
    }

    internal static InventoryLot? AttachedHandcart(IHandcartWorld world, string actor) => world.Transport.Hitches
        .FirstOrDefault(hitch => hitch.PullerId == actor) is { } hitch
        ? InventoryIndex.For(world.Inventory).Find(hitch.CartLotId) : null;

    internal static IEnumerable<InventoryLot> NearbyOwnedHandcarts(IHandcartWorld world, string actor, GridPoint position) =>
        world.HandcartControlStock(actor).Where(lot => lot.GroundPosition == new InventoryGroundPosition(position.X, position.Y));

    internal static InventoryLot? NearbyOwnedHandcart(IHandcartWorld world, string actor, GridPoint position) =>
        AttachedHandcart(world, actor) ?? NearbyOwnedHandcarts(world, actor, position).FirstOrDefault();

    internal static bool CanPullHandcart(IHandcartWorld world, InventoryLot cart) => cart.ConditionBasisPoints > 0 &&
        !world.HasActiveContainerReservation(world.Inventory, cart.Id);

    internal static bool LegalHandcartStep(IHandcartWorld world, GridPoint from, GridPoint to) => !world.Map.IsDiagonalFootStep(from, to) &&
        (IsRoadSurface(world.Transport, to) || (world.Map.ElevationAt(to) ?? 0) < WorldMap.MountainElevationThreshold &&
            world.Map.Tiles[to.Y * world.Map.Width + to.X].Terrain is not (TerrainKind.Mountain or TerrainKind.Peak));

    internal static int TravelStepCost(IHandcartWorld world, string actor, GridPoint from, GridPoint to) => AttachedHandcart(world, actor) is null
        ? world.AgentStepCost(from, to)
        : IsRoadSurface(world.Transport, from) && IsRoadSurface(world.Transport, to)
            ? RoadStepCost(world.Map, world.Transport, from, to) : checked(RoadStepCost(world.Map, world.Transport, from, to) + 100);

    internal static void MoveAttachedHandcart(IHandcartWorld world, string actor, GridPoint from, GridPoint to)
    {
        if (AttachedHandcart(world, actor) is not { } cart) return;
        if (cart.GroundPosition != new InventoryGroundPosition(from.X, from.Y) || !LegalHandcartStep(world, from, to))
            throw new InvalidOperationException("The cart and puller must share a legal physical step.");
        world.MoveCartInventory(actor, cart.Id, to, IsRoadSurface(world.Transport, from) && IsRoadSurface(world.Transport, to) ? 5 : 20);
        world.AppendEvent("handcart_moved", $"{actor}:{cart.Id}");
        if (world.Inventory.GetLot(cart.Id).ConditionBasisPoints == 0)
            ParkHandcart(world, actor, "broken");
    }

    internal static void ParkHandcart(IHandcartWorld world, string actor, string reason = "parked")
    {
        if (world.Transport.Hitches.FirstOrDefault(hitch => hitch.PullerId == actor) is not { } hitch) return;
        world.Transport.DetachHandcart(hitch);
        world.AppendEvent("handcart_parked", $"{actor}:{hitch.CartLotId}:{reason}");
    }

    internal static void ReconcileHandcartHitches(IHandcartWorld world)
    {
        foreach (var hitch in world.Transport.Hitches.ToArray())
        {
            var cart = InventoryIndex.For(world.Inventory).Find(hitch.CartLotId);
            if (!world.Agents.ContainsKey(hitch.PullerId) || cart?.OwnerId != hitch.PullerId ||
                cart.ConditionBasisPoints == 0)
                ParkHandcart(world, hitch.PullerId, "ownership_changed");
        }
    }

    internal static bool CanLoadCartLot(IHandcartWorld world, string actor, AgentState person, InventoryLot lot) =>
        lot.ContainerLotId is null && lot.DeliveryBuildingId is null &&
        !world.OnBorrowedMarketStall(lot) && (lot.OwnerId != actor || !world.OnMarketStall(lot)) &&
        world.RecheckGoods(new(GoodsUse.ConsumeAt, actor, GoodsOwners.One(lot.OwnerId), GoodsKinds.One(lot.ItemKind)), lot.Id) is not null &&
        // Worn clothing, a carry aid, a worn ornament or an item under repair stays on the agent.
        !PersonalEquipmentRules.IsSelected(person.Equipment, lot.Id) &&
        // Goods in anyone's custody, even the owner's, must be put down before loading.
        (lot.OwnerId == actor && lot.CarrierId is null && (ToolProgressionRules.IsTopLevelCarriedLot(lot, actor) ||
             lot.GroundPosition == new InventoryGroundPosition(person.Position.X, person.Position.Y)) ||
         // Household stock someone is borrowing stays the household's.
         lot.OwnerId == world.HouseholdFor(actor) && lot.CarrierId is null &&
             (!world.IsEdibleFood(lot.ItemKind) || world.MayCollectSharedFood(actor)) &&
             person.Position == world.HouseholdStockPosition(lot));

    internal static void CompleteHandcartControl(IHandcartWorld world, HandcartControlActionPlan plan, InventoryLot cart)
    {
        if (plan.Choice.Kind == HandcartControlKind.Attach)
        {
            world.Transport.AttachHandcart(cart.Id, plan.Actor);
            world.AppendEvent("handcart_attached", $"{plan.Actor}:{cart.Id}");
        }
        else if (plan.Choice.Kind == HandcartControlKind.Park) ParkHandcart(world, plan.Actor);
        else
        {
            var actor = plan.Actor;
            var recipient = plan.Choice.TargetId!;
            world.TransferCartInventory(actor, recipient, cart);
            world.AppendEvent("handcart_transferred", $"{actor}:{cart.Id}:{recipient}");
        }
    }
}
