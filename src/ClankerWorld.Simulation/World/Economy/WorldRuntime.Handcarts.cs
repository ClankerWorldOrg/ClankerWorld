using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

/// <summary>An exclusive physical attachment; position, property, condition and cargo remain inventory facts.</summary>
public sealed record HandcartHitch(string CartLotId, string PullerId);

public sealed partial class WorldRuntime
{
    private static readonly string[] CartRepairKinds = ["wood", "iron_fittings", "rope"];
    private const string AttachCartPrefix = HandcartControlChoice.AttachPrefix;
    private const string LoadCartPrefix = HandcartCargoChoice.LoadPrefix;
    private const string UnloadCartPrefix = HandcartCargoChoice.UnloadPrefix;
    private const string UnloadCartGroundPrefix = HandcartCargoChoice.GroundPrefix;
    private const string CollectCartRepairPrefix = HandcartRepairInputChoice.Prefix;
    private const string RepairCartPrefix = HandcartRepairChoice.Prefix;
    private const string PullCartPrefix = HandcartPullChoice.Prefix;
    // Generic cart controls have no delivery intention. Keep them selectable by models without
    // letting the built-in chooser shuffle goods or pull between arbitrary buildings ahead of safe idle.
    private const int UnassignedCartPriority = 110;

    private static bool IsHandcartRecipe(RecipeDefinition recipe) => ProductionSystem.IsHandcartRecipe(recipe);

    private RecipeDefinition? HouseholdHandcartRecipe(string? household) =>
        household is not null && BlacksmithForHousehold(household) is { } blacksmith
            ? worldContent.Recipes.FirstOrDefault(recipe =>
                recipe.WorkstationBuildingId == blacksmith.DefinitionId && IsHandcartRecipe(recipe))
            : null;

    /// <summary>
    /// The cart recipe while the actor may build their own cart: no cart yet, none being built, and every
    /// material either carried already or in stock they may collect, so they do not gather part of a set that
    /// nothing in the world could complete.
    /// </summary>
    private RecipeDefinition? HandcartToBuild(string actor)
    {
        var household = society.Checkpoint.GetAgent(actor).HouseholdId;
        var recipe = HouseholdHandcartRecipe(household);
        return recipe is not null && household is not null && MayBuildHandcart(actor, household, recipe) ? recipe : null;
    }

    private bool MayBuildHandcart(string actor, string household, RecipeDefinition recipe)
    {
        if (!IsAdult(actor) || !IsHandcartRecipe(recipe) || HandcartControlStock(actor).Any() ||
            worldSimulation.ProductionJobs.Any(job => job.WorkerId == actor && job.RecipeId == recipe.CanonicalId &&
                job.State == WorldProductionJobState.Running)) return false;
        var shortfall = recipe.Inputs.Select(input => (Kind: input.ResourceId, Missing: input.Amount -
                CarriedMaterialQuantity(actor, input.ResourceId) - HouseholdMaterialQuantity(actor, household, input.ResourceId)))
            .Where(item => item.Missing > 0).ToArray();
        // Town Warehouse stock needs a route, so it is counted last: first without routes, then one route per Warehouse.
        return shortfall.All(item => WarehouseStockLots(actor, item.Kind).Sum(AvailableLotQuantity) >= item.Missing) &&
            shortfall.All(item => ReachableWarehouseStockCovers(actor, item.Kind, item.Missing));
    }

    private int CarriedMaterialQuantity(string actor, string kind) =>
        FindGoods(new(GoodsUse.Holdings, actor, GoodsOwners.One(actor), GoodsKinds.One(kind))).Matches
            .Where(match => ToolProgressionRules.IsTopLevelCarriedLot(match.Lot, actor)).Sum(match => match.Quantity);

    private int HouseholdMaterialQuantity(string actor, string household, string kind) =>
        FindGoods(new GoodsRequest(GoodsUse.ReachableHoldings, actor, GoodsOwners.One(household), GoodsKinds.One(kind)))
            .Matches.Where(match => match.Lot.CarrierId is null && match.Lot.ContainerLotId is null)
            .Sum(match => match.Quantity);

    private bool ReachableWarehouseStockCovers(string actor, string kind, int missing)
    {
        var found = 0;
        foreach (var match in WarehouseStockGoods(actor, kind, GoodsUse.ReachableHoldings))
        {
            found += match.Quantity;
            if (found >= missing) return true;
        }
        return false;
    }

    // A would-be cart builder keeps the materials they carry for it. Household hauls take only what is
    // beyond that, or they would return it to stock as fast as the builder collects it from there.
    private int SpareCarriedQuantity(string actor, InventoryLot lot)
    {
        var kept = lot.OwnerId == actor && ToolProgressionRules.IsTopLevelCarriedLot(lot, actor) &&
            HouseholdHandcartRecipe(society.Checkpoint.GetAgent(actor).HouseholdId) is { } cart
            ? cart.Inputs.Where(input => input.ResourceId == lot.ItemKind).Sum(input => input.Amount) : 0;
        if (kept == 0 || HandcartToBuild(actor) is null) return AvailableLotQuantity(lot);
        return Math.Min(AvailableLotQuantity(lot), Math.Max(0, CarriedMaterialQuantity(actor, lot.ItemKind) - kept));
    }

    private static bool IsHandcartCargo(InventoryCheckpoint inventory, InventoryLot lot) => lot.ContainerLotId is { } id &&
        InventoryIndex.For(inventory).Find(id)?.ItemKind == InventoryContainerRules.Handcart;

    private InventoryLot? AttachedHandcart(string actor) => handcartHitches
        .FirstOrDefault(hitch => hitch.PullerId == actor) is { } hitch
        ? InventoryIndex.For(society.Checkpoint.Inventory).Find(hitch.CartLotId) : null;

    private IEnumerable<InventoryLot> NearbyOwnedHandcarts(string actor, GridPoint position) =>
        HandcartControlStock(actor).Where(lot => lot.GroundPosition == new InventoryGroundPosition(position.X, position.Y));

    private InventoryLot? NearbyOwnedHandcart(string actor, GridPoint position) =>
        AttachedHandcart(actor) ?? NearbyOwnedHandcarts(actor, position).FirstOrDefault();

    private bool CanPullHandcart(InventoryLot cart) => cart.ConditionBasisPoints > 0 &&
        !HasActiveContainerReservation(society.Checkpoint.Inventory, cart.Id);

    private bool LegalHandcartStep(GridPoint from, GridPoint to) => !map.IsDiagonalFootStep(from, to) &&
        (IsRoadSurface(to) || (map.ElevationAt(to) ?? 0) < WorldMap.MountainElevationThreshold &&
            map.Tiles[to.Y * map.Width + to.X].Terrain is not (TerrainKind.Mountain or TerrainKind.Peak));

    private int TravelStepCost(string actor, GridPoint from, GridPoint to) => AttachedHandcart(actor) is null
        ? AgentStepCost(from, to)
        : IsRoadSurface(from) && IsRoadSurface(to)
            ? RoadStepCost(from, to) : checked(RoadStepCost(from, to) + 100);

    private void MoveAttachedHandcart(string actor, GridPoint from, GridPoint to)
    {
        if (AttachedHandcart(actor) is not { } cart) return;
        if (cart.GroundPosition != new InventoryGroundPosition(from.X, from.Y) || !LegalHandcartStep(from, to))
            throw new InvalidOperationException("The cart and puller must share a legal physical step.");
        ApplyInventoryTransition(inventory => InventoryAuthority.MoveHandcart(inventory, actor, cart.Id,
            new(to.X, to.Y), IsRoadSurface(from) && IsRoadSurface(to) ? 5 : 20));
        AppendEvent(new HandcartMovedEventPayload(actor, cart.Id));
        if (society.Checkpoint.Inventory.GetLot(cart.Id).ConditionBasisPoints == 0)
            ParkHandcart(actor, "broken");
    }

    private void ParkHandcart(string actor, string reason = "parked")
    {
        if (handcartHitches.FirstOrDefault(hitch => hitch.PullerId == actor) is not { } hitch) return;
        handcartHitches.Remove(hitch);
        AppendEvent(new HandcartParkedEventPayload(actor, hitch.CartLotId, reason));
    }

    private void ReconcileHandcartHitches()
    {
        foreach (var hitch in handcartHitches.ToArray())
        {
            var cart = InventoryIndex.For(society.Checkpoint.Inventory).Find(hitch.CartLotId);
            if (!agents.ContainsKey(hitch.PullerId) || cart?.OwnerId != hitch.PullerId ||
                cart.ConditionBasisPoints == 0)
                ParkHandcart(hitch.PullerId, "ownership_changed");
        }
    }

    private bool CanLoadCartLot(string actor, AgentState person, InventoryLot lot) =>
        lot.ContainerLotId is null && lot.DeliveryBuildingId is null &&
        !OnBorrowedMarketStall(lot) && (lot.OwnerId != actor || !OnMarketStall(lot)) &&
        RecheckGoods(new(GoodsUse.ConsumeAt, actor, GoodsOwners.One(lot.OwnerId), GoodsKinds.One(lot.ItemKind)), lot.Id) is not null &&
        // Worn clothing, a carry aid, a worn ornament or an item under repair stays on the agent.
        !PersonalEquipmentRules.IsSelected(person.Equipment, lot.Id) &&
        // Goods in anyone's custody, even the owner's, must be put down before loading.
        (lot.OwnerId == actor && lot.CarrierId is null && (ToolProgressionRules.IsTopLevelCarriedLot(lot, actor) ||
             lot.GroundPosition == new InventoryGroundPosition(person.Position.X, person.Position.Y)) ||
         // Household stock someone is borrowing stays the household's.
         lot.OwnerId == society.Checkpoint.GetAgent(actor).HouseholdId && lot.CarrierId is null &&
             (!IsEdibleFood(lot.ItemKind) || MayCollectSharedFood(actor)) &&
             person.Position == HouseholdStockPosition(lot));

    private void AddHandcartCandidates(List<CognitionCandidate> candidates, string actor,
        AgentState person)
    {
        var inventory = society.Checkpoint.Inventory;
        if (HandcartToBuild(actor) is { } craftRecipe)
            foreach (var input in craftRecipe.Inputs)
                AddHandcartBuildMaterialCandidate(candidates, PlanHandcartBuildMaterial(actor, new(input.ResourceId), craftRecipe));
        if (AttachedHandcart(actor) is null && !animalWorld.Animals.Any(animal => animal.RiderId == actor || animal.LeaderId == actor))
        {
            foreach (var cart in HandcartControlStock(actor))
                AddHandcartControlCandidate(candidates, PlanHandcartControl(actor, new(HandcartControlKind.Attach, cart.Id), cart),
                    "Reach and attach your parked handcart to carry a larger load.");
        }
        else if (AttachedHandcart(actor) is not null)
        {
            AddHandcartControlCandidate(candidates, PlanHandcartControl(actor, new(HandcartControlKind.Park)),
                "Park the handcart here with its cargo kept inside.");
            foreach (var building in worldSimulation.Buildings.OrderBy(item => item.InstanceId, StringComparer.Ordinal))
                AddHandcartPullCandidate(candidates, PlanHandcartPull(actor, new(building.InstanceId)));
        }
        if (WornHandcarts(actor).Any())
        {
            foreach (var kind in CartRepairKinds)
                AddHandcartRepairCandidate(candidates, PlanHandcartRepairInput(actor, new(kind)));
            foreach (var cart in WornHandcarts(actor))
                AddHandcartRepairCandidate(candidates, PlanHandcartRepair(actor, new(cart.Id)));
        }
        if (NearbyOwnedHandcart(actor, person.Position) is not { } nearby) return;
        foreach (var lot in HandcartLoadStock(actor))
            AddHandcartCargoCandidate(candidates, PlanHandcartCargo(actor, new(HandcartCargoKind.Load, lot.Id), nearby.Id));
        foreach (var cart in NearbyOwnedHandcarts(actor, person.Position))
            foreach (var lot in CartCargoLots(actor, cart.Id))
            {
                AddHandcartCargoCandidate(candidates, PlanHandcartCargo(actor, new(HandcartCargoKind.Unload, lot.Id), cart.Id));
                AddHandcartCargoCandidate(candidates, PlanHandcartCargo(actor, new(HandcartCargoKind.UnloadGround, lot.Id), cart.Id));
            }
        if (AttachedHandcart(actor) is null && !HasActiveContainerReservation(inventory, nearby.Id))
            foreach (var recipient in agents.Values.Where(item => item.AgentId != actor &&
                         IsAdult(item.AgentId) && IsWithinInteractionRange(person.Position, item.Position, 1))
                         .OrderBy(item => item.AgentId, StringComparer.Ordinal))
                AddHandcartControlCandidate(candidates, PlanHandcartControl(actor, new(HandcartControlKind.Give, recipient.AgentId), nearby),
                    $"Give your parked handcart and its cargo to {society.Checkpoint.GetAgent(recipient.AgentId).Name} here.");
    }

    private bool ApplyHandcartCandidate(string actor, AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = int.MaxValue, ActionPlan? originalPlan = null)
    {
        if (originalPlan is HandcartControlActionPlan controlPlan)
        { ApplyHandcartControlPlan(controlPlan); return true; }
        if (HandcartControlChoice.Decode(candidateId) is { } control)
        {
            if (PlanHandcartControl(actor, control) is { } plan) ApplyHandcartControlPlan(plan);
            return true;
        }
        if (originalPlan is HandcartBuildMaterialActionPlan buildMaterial)
        { ApplyHandcartBuildMaterialPlan(buildMaterial); return true; }
        if (HandcartBuildMaterialChoice.Decode(candidateId) is { } buildMaterialChoice)
        {
            if (PlanHandcartBuildMaterial(actor, buildMaterialChoice) is { } plan) ApplyHandcartBuildMaterialPlan(plan);
            return true;
        }
        if (originalPlan is HandcartRepairInputActionPlan repairInput)
        { ApplyHandcartRepairInputPlan(repairInput); return true; }
        if (HandcartRepairInputChoice.Decode(candidateId) is { } repairInputChoice)
        {
            if (PlanHandcartRepairInput(actor, repairInputChoice) is { } plan) ApplyHandcartRepairInputPlan(plan);
            return true;
        }
        if (originalPlan is HandcartPullActionPlan pullPlan)
        { ApplyHandcartPullPlan(pullPlan); return true; }
        if (HandcartPullChoice.Decode(candidateId) is { } pull)
        {
            if (PlanHandcartPull(actor, pull) is { } plan) ApplyHandcartPullPlan(plan);
            return true;
        }
        if (originalPlan is HandcartCargoActionPlan cargoPlan)
        { ApplyHandcartCargoPlan(cargoPlan); return true; }
        if (HandcartCargoChoice.Decode(candidateId) is { } cargoChoice)
        {
            if (PlanHandcartCargo(actor, cargoChoice, targetCartId, maximumQuantity) is { } plan) ApplyHandcartCargoPlan(plan);
            return true;
        }
        if (originalPlan is HandcartRepairActionPlan repairPlan)
        { ApplyHandcartRepairPlan(repairPlan); return true; }
        if (HandcartRepairChoice.Decode(candidateId) is { } repairChoice)
        {
            if (targetCartId is not null && targetCartId != repairChoice.CartId) return true;
            if (PlanHandcartRepair(actor, repairChoice) is { } plan) ApplyHandcartRepairPlan(plan);
            return true;
        }
        return false;
    }

    private void CompleteHandcartBuildMaterial(HandcartBuildMaterialActionPlan plan) =>
        ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory, CartBuildMaterialTransferId(WorldTick, plan.Actor),
            plan.Pickup.OwnerId, plan.Actor, plan.Pickup.StockId, plan.Quantity, "handcart_material_collected",
            holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));

    private void CompleteHandcartRepair(HandcartRepairActionPlan plan, InventoryLot cart)
    {
        var actor = plan.Actor;
        var reservations = new List<string>();
        ApplyInventoryTransition(inventory =>
        {
            var updated = inventory;
            foreach (var material in plan.Materials)
            {
                var id = CartRepairReservationId(WorldTick, actor, material.ItemKind);
                updated = InventoryAuthority.Reserve(updated, id, actor, material.StockId, material.Quantity, "equipment_repair",
                    WorldTick + 1, holdAccess: InventoryHoldRules.BorrowedBy(actor));
                reservations.Add(id);
            }
            return InventoryAuthority.RepairSingleUnit(updated, cart.Id, 10_000, reservations.Select(Identity.Parse).ToArray(), holdAccess: InventoryHoldRules.BorrowedBy(actor));
        });
        RecordNonLandRepairCompletion(actor, cart, cart.Id, NativeCartRepairId(WorldTick, actor, cart.Id),
            agents[actor].Position, reservations.Select(Identity.Parse).ToArray());
        AppendEvent(new HandcartRepairedEventPayload(actor, cart.Id));
    }

    private void CompleteHandcartCargo(HandcartCargoActionPlan plan, InventoryLot cart, InventoryLot lot)
    {
        var actor = plan.Actor;
        var quantity = plan.Quantity;
        if (plan.Choice.Kind == HandcartCargoKind.Load)
        {
            ApplyInventoryTransition(inventory =>
            {
                var updated = inventory;
                var cargoId = lot.Id;
                if (lot.OwnerId != actor)
                {
                    var operation = $"cart-pickup:{WorldTick}:{actor}";
                    updated = InventoryAuthority.Transfer(updated, operation, lot.OwnerId, actor, lot.Id, quantity,
                        "handcart_pickup", destinationGroundPosition: cart.GroundPosition, holdAccess: InventoryHoldRules.BorrowedBy(actor));
                    cargoId = quantity == lot.Quantity ? lot.Id : DerivedId.Create($"{lot.Id}#transfer:{operation}");
                }
                return InventoryAuthority.LoadHandcart(updated, $"load:{WorldTick}:{actor}", actor, cart.Id, cargoId, quantity);
            });
            AppendEvent(new HandcartLoadedEventPayload(actor, cart.Id, lot.ItemKind, quantity));
        }
        else
        {
            ApplyInventoryTransition(inventory => InventoryAuthority.UnloadHandcart(inventory, $"unload:{WorldTick}:{actor}",
                actor, cart.Id, lot.Id, quantity, plan.Choice.Kind == HandcartCargoKind.UnloadGround));
            AppendEvent(new HandcartUnloadedEventPayload(actor, cart.Id, lot.ItemKind, quantity));
        }
    }

    private void CompleteHandcartControl(HandcartControlActionPlan plan, InventoryLot cart)
    {
        if (plan.Choice.Kind == HandcartControlKind.Attach)
        {
            handcartHitches.Add(new(cart.Id, plan.Actor));
            AppendEvent(new HandcartAttachedEventPayload(plan.Actor, cart.Id));
        }
        else if (plan.Choice.Kind == HandcartControlKind.Park) ParkHandcart(plan.Actor);
        else
        {
            var actor = plan.Actor;
            var recipient = plan.Choice.TargetId!;
            ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory, $"cart-gift:{WorldTick}:{actor}",
                actor, recipient, cart.Id, 1, "handcart_given", destinationGroundPosition: cart.GroundPosition, holdAccess: InventoryHoldRules.BorrowedBy(actor)));
            AppendEvent(new HandcartTransferredEventPayload(actor, cart.Id, recipient));
        }
    }

    private static void ValidateHandcarts(IReadOnlyList<HandcartHitch> hitches, InventoryCheckpoint inventory,
        IReadOnlyList<AgentState> people, WorldMap travelMap)
    {
        if (hitches.Any(item => item is null) || hitches.Select(item => item.CartLotId).Distinct(StringComparer.Ordinal).Count() != hitches.Count ||
            hitches.Select(item => item.PullerId).Distinct(StringComparer.Ordinal).Count() != hitches.Count)
            throw new InvalidDataException("Cart attachments must be distinct for both carts and pullers.");
        foreach (var cart in inventory.Lots.Where(lot => lot.ItemKind == InventoryContainerRules.Handcart))
            if (cart.GroundPosition is not { } ground || !travelMap.IsPassable(new(ground.X, ground.Y)))
                throw new InvalidDataException("A cart must remain on a legal ground tile.");
        foreach (var hitch in hitches)
        {
            var cart = inventory.Lots.FirstOrDefault(lot => lot.Id == hitch.CartLotId);
            var person = people.FirstOrDefault(item => item.AgentId == hitch.PullerId);
            if (cart is null || cart.ItemKind != InventoryContainerRules.Handcart || cart.OwnerId != hitch.PullerId ||
                cart.ConditionBasisPoints <= 0 || person is null ||
                cart.GroundPosition != new InventoryGroundPosition(person.Position.X, person.Position.Y))
                throw new InvalidDataException("A cart attachment needs its living owner at the cart's physical position.");
        }
    }
}
