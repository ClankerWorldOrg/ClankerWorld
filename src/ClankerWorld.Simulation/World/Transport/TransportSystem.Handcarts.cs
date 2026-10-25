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

    /// <summary>
    /// The cart recipe while the actor may build their own cart: no cart yet, none being built, and every
    /// material either carried already or in stock they may collect, so they do not gather part of a set that
    /// nothing in the world could complete.
    /// </summary>
    internal static RecipeDefinition? HandcartToBuild(IHandcartWorld world, string actor)
    {
        var household = world.RecordedHouseholdFor(actor);
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

    // A would-be cart builder keeps the materials they carry for it. Household hauls take only what is
    // beyond that, or they would return it to stock as fast as the builder collects it from there.
    internal static int SpareCarriedQuantity(IHandcartWorld world, string actor, InventoryLot lot)
    {
        var kept = lot.OwnerId == actor && ToolProgressionRules.IsTopLevelCarriedLot(lot, actor) &&
            HouseholdHandcartRecipe(world, world.RecordedHouseholdFor(actor)) is { } cart
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
        world.AppendEvent(new HandcartMovedEventPayload(actor, cart.Id));
        if (world.Inventory.GetLot(cart.Id).ConditionBasisPoints == 0)
            ParkHandcart(world, actor, "broken");
    }

    internal static void ParkHandcart(IHandcartWorld world, string actor, string reason = "parked")
    {
        if (world.Transport.Hitches.FirstOrDefault(hitch => hitch.PullerId == actor) is not { } hitch) return;
        world.Transport.DetachHandcart(hitch);
        world.AppendEvent(new HandcartParkedEventPayload(actor, hitch.CartLotId, reason));
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
         lot.OwnerId == world.RecordedHouseholdFor(actor) && lot.CarrierId is null &&
             (!world.IsEdibleFood(lot.ItemKind) || world.MayCollectSharedFood(actor)) &&
             person.Position == world.HouseholdStockPosition(lot));

    internal static void CompleteHandcartControl(IHandcartWorld world, HandcartControlActionPlan plan, InventoryLot cart)
    {
        if (plan.Choice.Kind == HandcartControlKind.Attach)
        {
            world.Transport.AttachHandcart(cart.Id, plan.Actor);
            world.AppendEvent(new HandcartAttachedEventPayload(plan.Actor, cart.Id));
        }
        else if (plan.Choice.Kind == HandcartControlKind.Park) ParkHandcart(world, plan.Actor);
        else
        {
            var actor = plan.Actor;
            var recipient = plan.Choice.TargetId!;
            world.TransferCartInventory(actor, recipient, cart);
            world.AppendEvent(new HandcartTransferredEventPayload(actor, cart.Id, recipient));
        }
    }

    internal static HandcartCargoActionPlan? PlanHandcartCargo(IHandcartWorld world, string actor, HandcartCargoChoice choice,
        string? targetCartId = null, int maximumQuantity = int.MaxValue)
    {
        var inventory = world.Inventory;
        var stock = InventoryIndex.For(inventory).Find(choice.StockId);
        var cartId = targetCartId ?? (choice.Kind == HandcartCargoKind.Load
            ? NearbyOwnedHandcart(world, actor, world.Agents[actor].Position)?.Id : stock?.ContainerLotId);
        if (cartId is null || world.RecheckGoods(new(GoodsUse.HandcartControl, actor, GoodsOwners.One(actor),
                GoodsKinds.One(InventoryContainerRules.Handcart)), cartId) is not { Lot: var cart } || cart.GroundPosition is not { } point || stock is null)
            return null;
        var match = choice.Kind == HandcartCargoKind.Load
            ? world.RecheckGoods(new(GoodsUse.ConsumeAt, actor, GoodsOwners.One(stock.OwnerId), GoodsKinds.One(stock.ItemKind)), stock.Id)
            : world.RecheckGoods(new(GoodsUse.Cargo, actor, GoodsOwners.One(actor), GoodsKinds.One(stock.ItemKind), ContainerId: cartId), stock.Id);
        if (match is null) return null;
        var displayQuantity = choice.Kind == HandcartCargoKind.Load
            ? Math.Min(InventoryContainerRules.HandcartCapacity - InventoryIndex.For(inventory).ContentsQuantity(cartId), world.AvailableLotQuantity(stock))
            : stock.Quantity;
        var available = choice.Kind == HandcartCargoKind.Load && stock.OwnerId != actor && world.IsEdibleFood(stock.ItemKind)
            ? Math.Min(displayQuantity, world.SharedFoodCollectionAllowance(actor)) : displayQuantity;
        var quantity = Math.Min(maximumQuantity, choice.Kind == HandcartCargoKind.Unload ? Math.Min(available, world.FreeCarryCapacity(actor)) : available);
        return new(actor, choice, cartId, new(point.X, point.Y), GoodsPlanFamily.Capture(inventory, cart),
            world.Transport.Hitches.FirstOrDefault(item => item.PullerId == actor), stock.OwnerId, stock.ItemKind,
            GoodsPlanFamily.Capture(inventory, stock), match.Place, quantity, displayQuantity);
    }

    internal static ActionCheck CheckHandcartCargoPlan(IHandcartWorld world, HandcartCargoActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? world.TransportCapabilities);
        if (!capability.Allowed) return capability;
        var selection = CheckHandcartSelection(world, plan.Actor, plan.CartId, plan.Position, plan.CartFamily, plan.Hitch);
        if (!selection.Allowed) return selection;
        if (world.Agents[plan.Actor].Position != plan.Position)
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "they are no longer beside the selected handcart");
        var inventory = world.Inventory;
        var cart = inventory.GetLot(plan.CartId);
        if (!InventoryRules.CanMoveFamily(InventoryIndex.For(inventory), cart))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected cart cargo is promised now");
        var load = plan.Choice.Kind == HandcartCargoKind.Load;
        var match = world.RecheckGoods(new(load ? GoodsUse.ConsumeAt : GoodsUse.Cargo, plan.Actor, GoodsOwners.One(plan.StockOwner),
            GoodsKinds.One(plan.ItemKind), ContainerId: load ? null : plan.CartId), plan.Choice.StockId);
        if (match is null || match.Quantity < plan.Quantity || match.Place != plan.SourcePlace ||
            !GoodsPlanFamily.Matches(inventory, match.Lot, plan.StockFamily) ||
            load && (!CanLoadCartLot(world, plan.Actor, world.Agents[plan.Actor], match.Lot) || !InventoryHoldRules.Allows(InventoryIndex.For(inventory), match.Lot)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected load is no longer available here");
        if (load && (!CanPullHandcart(world, cart) || plan.Quantity > InventoryContainerRules.HandcartCapacity - InventoryIndex.For(inventory).ContentsQuantity(cart.Id)) ||
            plan.Quantity <= 0 || !load && plan.Choice.Kind == HandcartCargoKind.Unload && world.FreeCarryCapacity(plan.Actor) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no longer room for the planned load");
        if (load && plan.StockOwner != plan.Actor && world.IsEdibleFood(plan.ItemKind) && world.SharedFoodCollectionAllowance(plan.Actor) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the household needs the selected food now");
        return new();
    }

    internal static void ApplyCheckedHandcartCargo(IHandcartWorld world, HandcartCargoActionPlan plan)
    {
        CompleteHandcartCargo(world, plan, world.Inventory.GetLot(plan.CartId), world.Inventory.GetLot(plan.Choice.StockId));
    }

    internal static HandcartPullActionPlan? PlanHandcartPull(IHandcartWorld world, string actor, HandcartPullChoice choice)
    {
        var hitch = world.Transport.Hitches.FirstOrDefault(item => item.PullerId == actor);
        var target = world.Buildings.FirstOrDefault(building => building.InstanceId == choice.BuildingId);
        if (hitch is null || target is null || world.RecheckGoods(new(GoodsUse.HandcartControl, actor, GoodsOwners.One(actor),
                GoodsKinds.One(InventoryContainerRules.Handcart)), hitch.CartLotId) is not { Lot: var cart } ||
            cart.GroundPosition is not { } ground) return null;
        return new(actor, choice, cart.Id, new(ground.X, ground.Y), GoodsPlanFamily.Capture(world.Inventory, cart),
            hitch, target.Position, world.Reach(actor, target.Position, ReachPurpose.HandcartPull, 0, 0, SwimmingPolicy.Never));
    }

    internal static ActionCheck CheckHandcartPullPlan(IHandcartWorld world, HandcartPullActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? world.TransportCapabilities);
        if (!capability.Allowed) return capability;
        var selection = CheckHandcartSelection(world, plan.Actor, plan.CartId, plan.Origin, plan.Family, plan.Hitch);
        if (!selection.Allowed) return selection;
        if (world.Agents[plan.Actor].Position != plan.Origin || world.Buildings.FirstOrDefault(building =>
                building.InstanceId == plan.Choice.BuildingId)?.Position != plan.Destination)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected cart or destination has moved");
        // An absent route is an existing wait step, with native movement-blocked events.
        var cart = world.Inventory.GetLot(plan.CartId);
        return CanPullHandcart(world, cart) && InventoryRules.CanMoveFamily(InventoryIndex.For(world.Inventory), cart)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected cart is broken or its cargo is promised now");
    }

    internal static void ApplyCheckedHandcartPull(IHandcartWorld world, HandcartPullActionPlan plan)
    {
        world.MoveToward(plan.Actor, world.Agents[plan.Actor], plan.Destination, "pull_handcart");
    }

    internal static HandcartControlActionPlan? PlanHandcartControl(IHandcartWorld world, string actor, HandcartControlChoice choice, InventoryLot? selected = null)
    {
        var hitch = world.Transport.Hitches.FirstOrDefault(item => item.PullerId == actor);
        var cart = selected ?? (choice.Kind == HandcartControlKind.Attach
            ? world.RecheckGoods(new(GoodsUse.HandcartControl, actor, GoodsOwners.One(actor), GoodsKinds.One(InventoryContainerRules.Handcart)), choice.TargetId!)?.Lot :
            choice.Kind == HandcartControlKind.Park ? hitch is not null ? InventoryIndex.For(world.Inventory).Find(hitch.CartLotId) : null :
            world.HandcartControlStock(actor).FirstOrDefault(item => item.GroundPosition == new InventoryGroundPosition(world.Agents[actor].Position.X, world.Agents[actor].Position.Y)));
        if (cart?.GroundPosition is not { } ground) return null;
        var position = new GridPoint(ground.X, ground.Y);
        return new(actor, choice, cart.Id, position, GoodsPlanFamily.Capture(world.Inventory, cart), hitch,
            choice.Kind == HandcartControlKind.Give && world.Agents.TryGetValue(choice.TargetId!, out var recipient) ? recipient.Position : null,
            choice.Kind == HandcartControlKind.Attach ? world.Reach(actor, position, ReachPurpose.HandcartControl, 0, 0, SwimmingPolicy.WhenCapable) : Array.AsReadOnly<GridPoint>([]));
    }

    internal static ActionCheck CheckHandcartControlPlan(IHandcartWorld world, HandcartControlActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? world.HandcartControlCapabilities(plan.Choice.Kind));
        if (!capability.Allowed) return capability;
        var selection = CheckHandcartSelection(world, plan.Actor, plan.CartId, plan.Position, plan.Family, plan.Hitch);
        if (!selection.Allowed) return selection;
        var cart = world.Inventory.GetLot(plan.CartId);
        var hitch = world.Transport.Hitches.FirstOrDefault(item => item.PullerId == plan.Actor);
        if (plan.Choice.Kind == HandcartControlKind.Park)
            return hitch?.CartLotId == plan.CartId && world.Agents[plan.Actor].Position == plan.Position ? new() :
                ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected handcart is no longer attached here");
        if (hitch is not null || world.HasActiveContainerReservation(world.Inventory, cart.Id))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected cart is attached or reserved now");
        if (plan.Choice.Kind == HandcartControlKind.Attach)
        {
            if (!CanPullHandcart(world, cart) || world.Animals.Any(animal => animal.RiderId == plan.Actor || animal.LeaderId == plan.Actor) ||
                world.Transport.Hitches.Any(item => item.CartLotId == plan.CartId))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected handcart is no longer available to attach");
            return world.Reach(plan.Actor, plan.Position, ReachPurpose.HandcartControl, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
                ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected handcart is out of reach now");
        }
        if (world.Agents[plan.Actor].Position != plan.Position || !world.Agents.TryGetValue(plan.Choice.TargetId!, out var recipient) || !world.IsAdult(recipient.AgentId) ||
            recipient.Position != plan.RecipientPosition || !world.IsWithinInteractionRange(plan.Position, recipient.Position, 1))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected recipient is no longer here to receive the cart");
        return InventoryRules.CanMoveFamily(InventoryIndex.For(world.Inventory), cart, InventoryHoldRules.BorrowedBy(plan.Actor))
            ? new() : ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected cart cargo is promised to other work");
    }

    internal static void ApplyCheckedHandcartControl(IHandcartWorld world, HandcartControlActionPlan plan)
    {
        if (plan.Choice.Kind == HandcartControlKind.Attach && world.Agents[plan.Actor].Position != plan.Position)
        { world.MoveToward(plan.Actor, world.Agents[plan.Actor], plan.Position, "attach_handcart"); return; }
        CompleteHandcartControl(world, plan, world.Inventory.GetLot(plan.CartId));
    }

    internal static ActionCheck CheckHandcartSelection(IHandcartWorld world, string actor, string cartId, GridPoint position,
        IReadOnlyList<PlannedStockMember> family, HandcartHitch? hitch)
    {
        if (world.RecheckGoods(new(GoodsUse.HandcartControl, actor, GoodsOwners.One(actor), GoodsKinds.One(InventoryContainerRules.Handcart)), cartId) is not { Lot: var cart } ||
            cart.GroundPosition != new InventoryGroundPosition(position.X, position.Y) || !GoodsPlanFamily.Matches(world.Inventory, cart, family))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected cart or cargo has changed");
        return world.Transport.Hitches.FirstOrDefault(item => item.PullerId == actor) == hitch ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected cart attachment has changed");
    }

    internal static HandcartBuildMaterialActionPlan? PlanHandcartBuildMaterial(IHandcartWorld world, string actor, HandcartBuildMaterialChoice choice,
        RecipeDefinition? selected = null)
    {
        var recipe = selected ?? HandcartToBuild(world, actor);
        if (recipe is null || world.HouseholdFor(actor) is not { } home || world.HouseholdBuildingWithTag(home, "blacksmith") is not { } smith)
            return null;
        var input = recipe.Inputs.FirstOrDefault(item => item.ResourceId == choice.ItemKind);
        if (input.Amount <= 0 || world.SharedItem(choice.ItemKind, actor, smith.Position) is not { } stock ||
            world.RecheckGoods(world.SharedCollectionRequest(actor, stock.OwnerId, choice.ItemKind), stock.Id) is not { } match) return null;
        var pickup = new PlannedEquipmentPickup(stock.Id, stock.OwnerId, stock.ItemKind,
            world.HouseholdStockPosition(stock), world.HouseholdStockInteractionRange(stock));
        var quantity = Math.Min(input.Amount - CarriedMaterialQuantity(world, actor, input.ResourceId),
            Math.Min(match.Quantity, world.PickupCarryCapacity(actor, stock, smith.Position)));
        return new(actor, choice, home, recipe.CanonicalId, input.Amount, smith.InstanceId, smith.Position, pickup,
            GoodsPlanFamily.Capture(world.Inventory, stock), quantity,
            world.Reach(actor, pickup.Position, ReachPurpose.HandcartBuildMaterial, pickup.InteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    internal static ActionCheck CheckHandcartBuildMaterialPlan(IHandcartWorld world, HandcartBuildMaterialActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? world.TransportCapabilities);
        if (!capability.Allowed) return capability;
        var recipe = world.Recipes.FirstOrDefault(item => item.CanonicalId == plan.RecipeId);
        var smith = world.Buildings.FirstOrDefault(item => item.InstanceId == plan.SmithId);
        if (world.HouseholdFor(plan.Actor) != plan.HouseholdId || recipe is null || smith is null || smith.HouseholdId != plan.HouseholdId ||
            smith.Position != plan.Destination || smith.DefinitionId != recipe.WorkstationBuildingId ||
            recipe.Inputs.FirstOrDefault(input => input.ResourceId == plan.Choice.ItemKind).Amount != plan.InputAmount ||
            !MayBuildHandcart(world, plan.Actor, plan.HouseholdId, recipe))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected handcart recipe or building plan is no longer available");
        if (plan.Quantity <= 0 || plan.InputAmount - CarriedMaterialQuantity(world, plan.Actor, plan.Choice.ItemKind) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they no longer need the planned material quantity");
        var pickup = plan.Pickup;
        if (world.RecheckGoods(world.SharedCollectionRequest(plan.Actor, pickup.OwnerId, pickup.ItemKind), pickup.StockId) is not { } match ||
            match.Quantity < plan.Quantity || !GoodsPlanFamily.Matches(world.Inventory, match.Lot, plan.Family) ||
            world.HouseholdStockPosition(match.Lot) != pickup.Position || world.HouseholdStockInteractionRange(match.Lot) != pickup.InteractionRange)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected building material is no longer available");
        if (world.PickupCarryCapacity(plan.Actor, match.Lot, plan.Destination) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "they cannot carry the planned materials back to the Blacksmith");
        return world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, pickup.Position, pickup.InteractionRange) ||
            world.Reach(plan.Actor, pickup.Position, ReachPurpose.HandcartBuildMaterial, pickup.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected building material is out of reach now");
    }

    internal static void ApplyCheckedHandcartBuildMaterial(IHandcartWorld world, HandcartBuildMaterialActionPlan plan)
    {
        var pickup = plan.Pickup;
        if (!world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, pickup.Position, pickup.InteractionRange))
        { world.MoveToward(plan.Actor, world.Agents[plan.Actor], pickup.Position, "handcart_material", pickup.InteractionRange); return; }
        CompleteHandcartBuildMaterial(world, plan);
    }

    internal static void CompleteHandcartBuildMaterial(IHandcartWorld world, HandcartBuildMaterialActionPlan plan) =>
        world.CollectCartMaterial(plan);

    internal static void CompleteHandcartRepair(IHandcartWorld world, HandcartRepairActionPlan plan, InventoryLot cart)
    {
        var actor = plan.Actor;
        var reservations = world.RepairCartInventory(plan, cart);
        world.RecordNonLandRepairCompletion(actor, cart, cart.Id, world.NativeCartRepairId(world.WorldTick, actor, cart.Id),
            world.Agents[actor].Position, reservations);
        world.AppendEvent(new HandcartRepairedEventPayload(actor, cart.Id));
    }

    internal static void CompleteHandcartCargo(IHandcartWorld world, HandcartCargoActionPlan plan, InventoryLot cart, InventoryLot lot)
    {
        var actor = plan.Actor;
        var quantity = plan.Quantity;
        if (plan.Choice.Kind == HandcartCargoKind.Load)
        {
            world.LoadCartInventory(plan, cart, lot);
            world.AppendEvent(new HandcartLoadedEventPayload(actor, cart.Id, lot.ItemKind, quantity));
        }
        else
        {
            world.UnloadCartInventory(plan, cart, lot);
            world.AppendEvent(new HandcartUnloadedEventPayload(actor, cart.Id, lot.ItemKind, quantity));
        }
    }
}
