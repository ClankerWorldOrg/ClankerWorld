using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void AddFoodPlanCandidate(List<CognitionCandidate> candidates, FoodActionPlan? plan,
        string description, int priority, string? destination = null)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed)
            candidates.Add(new(plan.CandidateId, description, priority, destination) { ActionPlan = plan });
    }

    private static ActorCapabilityPolicy FoodPolicy(bool consumption) => new(ActionLifeStages.NonInfants,
        AllowBoat: consumption, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private ConsumeFoodActionPlan? PlanFoodConsumption(string actor, InventoryLot? selected = null)
    {
        if ((selected ?? PreferredFood(actor, actor).FirstOrDefault()) is not { } food) return null;
        return new(actor, food.Id, food.ItemKind, InventoryIndex.For(society.Checkpoint.Inventory).Root(food).Id);
    }

    private SharedFoodActionPlan? PlanSharedFood(string actor, InventoryLot? selected = null)
    {
        if ((selected ?? AvailableSharedFood(actor)) is not { } food) return null;
        var position = HouseholdStockPosition(food);
        var range = HouseholdStockInteractionRange(food);
        return new(actor, food.OwnerId, food.Id, food.ItemKind, InventoryIndex.For(society.Checkpoint.Inventory).Root(food).Id,
            position, range, Reach(actor, position, ReachPurpose.HouseholdFood, range, 0, SwimmingPolicy.WhenCapable));
    }

    private FoodResourceActionPlan? PlanFoodResource(string actor, bool harvest, MapResource? selected = null)
    {
        if ((selected ?? AvailableFoodSource(actor, agents[actor].Position)) is not { } source) return null;
        return new(actor, new(harvest ? FoodActionKind.Harvest : FoodActionKind.Seek), source, FoodHarvestKind(source),
            FoodHarvestQuantity(source), source.TreeKind == TreeGrowthRules.Orchard ? TreeGrowthRules.OrchardSeedsPerPick : 0,
            Reach(actor, source.Position, ReachPurpose.FoodSource, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    private static string FoodHarvestKind(MapResource source) => source.Kind == "fruit" ? "fruit" :
        source.NaturalObjectKind == "wild_greens" ? "wild_greens" : "berries";

    private ActionCheck CheckFoodPlan(FoodActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? FoodPolicy(plan is ConsumeFoodActionPlan));
        if (!capability.Allowed) return capability;
        var index = InventoryIndex.For(society.Checkpoint.Inventory);
        switch (plan)
        {
            case ConsumeFoodActionPlan consumption:
                if (RecheckGoods(new(GoodsUse.Eat, plan.Actor, GoodsOwners.One(plan.Actor), GoodsKinds.One(consumption.ItemKind)),
                        consumption.StockId) is not { } carried || carried.Root.Id != consumption.RootId ||
                    !InventoryRules.CanReserve(index, carried.Lot, plan.Actor, 1) ||
                    carried.Lot.ContainerLotId is not null && (carried.Root.ItemKind != InventoryContainerRules.StoragePot || index.HasReservedFamily(carried.Root.Id)))
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected carried serving is no longer available");
                return new();
            case SharedFoodActionPlan shared:
                if (HouseholdFor(plan.Actor) != shared.HouseholdId ||
                    (shared.DependentId is null ? !this.townSystem.MayCollectSharedFood(this.civicWorld, plan.Actor) : !FamilySystem.ChildrenNeedingCare(this.FamilyParenthoodWorld, plan.Actor).Any(child => child.AgentId == shared.DependentId)))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "household food is no longer available to them");
                if (RecheckGoods(new(GoodsUse.ConsumeAt, plan.Actor, GoodsOwners.One(shared.HouseholdId), GoodsKinds.One(shared.ItemKind)),
                        shared.StockId) is not { } serving || serving.Root.Id != shared.RootId || serving.Lot.CarrierId is not null ||
                    !InventoryHoldRules.Allows(index, serving.Lot, InventoryHoldRules.BorrowedBy(plan.Actor)) ||
                    !InventoryHoldRules.Allows(index, serving.Root, InventoryHoldRules.BorrowedBy(plan.Actor)) ||
                    OnBorrowedMarketStall(serving.Lot) || HouseholdStockPosition(serving.Lot) != shared.Position ||
                    HouseholdStockInteractionRange(serving.Lot) != shared.InteractionRange)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected household serving is no longer available");
                if (serving.Lot.ContainerLotId is { } potId &&
                    (HouseForHousehold(shared.HouseholdId) is not { } house || serving.Root.ItemKind != InventoryContainerRules.StoragePot ||
                     serving.Root.StorageBuildingId != house.InstanceId || serving.Root.ConditionBasisPoints <= 0 || HasActiveContainerReservation(society.Checkpoint.Inventory, potId)))
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected food pot is no longer available");
                if (serving.Lot.ContainerLotId is not null && HouseForHousehold(shared.HouseholdId) is { } potHouse &&
                    agents[plan.Actor].Position != potHouse.Position && Reach(plan.Actor, potHouse.Position,
                        ReachPurpose.HouseholdFood, 0, 0, SwimmingPolicy.WhenCapable).Count == 0)
                    return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected food pot is out of reach now");
                if (shared.ReturnPosition is { } destination && PickupCarryCapacity(plan.Actor, serving.Lot, destination, shared.ReturnRange) < 1)
                    return ActionCheck.Refuse(ActionRefusalReason.Capacity, "they cannot carry the selected serving back to the child");
                if (FreeCarryCapacity(plan.Actor) < 1)
                    return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected serving");
                return Reach(plan.Actor, shared.Position, ReachPurpose.HouseholdFood, shared.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
                    ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected household serving is out of reach now");
            case FoodResourceActionPlan gathering:
                if (!map.Resources.Contains(gathering.Source) || resources.GetValueOrDefault(gathering.Source.Id) != ResourceState.Available ||
                    !EcologyRules.Harvest(worldSystems.Ecology.GetResource(gathering.Source.Id), 1).IsValid)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected food source is no longer available");
                if (FreeCarryCapacity(plan.Actor) < gathering.Quantity + gathering.SeedQuantity)
                    return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected food harvest");
                if (!CanReachTerrain(plan.Actor, gathering.Source.Position, ReachPurpose.FoodSource, 0, SwimmingPolicy.WhenCapable) ||
                    Reach(plan.Actor, gathering.Source.Position, ReachPurpose.FoodSource, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count == 0)
                    return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected food source is out of reach now");
                return gathering.Selected.Kind != FoodActionKind.Harvest || IsWithinInteractionRange(agents[plan.Actor].Position,
                    gathering.Source.Position, ResourceInteractionRange) ? new() :
                    ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "they are no longer beside the selected food source");
            default: throw new InvalidOperationException("A food plan has no registered operation.");
        }
    }

    private void ApplyCheckedFoodPlan(FoodActionPlan plan)
    {
        var person = agents[plan.Actor];
        switch (plan)
        {
            case ConsumeFoodActionPlan consumption:
                CompleteFoodConsumption(plan.Actor, person, society.Checkpoint.Inventory.GetLot(consumption.StockId));
                break;
            case SharedFoodActionPlan shared:
                CompleteSharedFoodCollection(plan.Actor, person, society.Checkpoint.Inventory.GetLot(shared.StockId));
                break;
            case FoodResourceActionPlan gathering when gathering.Selected.Kind == FoodActionKind.Seek:
                MoveToward(plan.Actor, person, gathering.Source.Position, "food", ResourceInteractionRange);
                break;
            case FoodResourceActionPlan gathering:
                CompleteFoodHarvest(plan.Actor, gathering.Source, gathering.ItemKind, gathering.Quantity, gathering.SeedQuantity);
                break;
        }
    }

    private static ActorCapabilityPolicy FirePolicy => new(ActionLifeStages.Adults,
        AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);
    private static ActorCapabilityPolicy WarmthPolicy => new(ActionLifeStages.NonInfants,
        AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private GoodsMatch? CarriedFirewood(string actor) => SpendingGoods(actor, GoodsUse.Spend, [actor], "wood")
        .FirstOrDefault(match => PersonalEquipmentRules.IsCarried(match.Lot, actor));

    private FireActionPlan? PlanFire(string actor, ActorCapabilityPolicy? policy = null)
    {
        if (!ActorCapabilities.Check(CapabilitiesFor(actor), new WarmthChoice(WarmthChoiceKind.Fire).Encode(), policy ?? FirePolicy).Allowed ||
            ReachableUnlitHearth(actor, agents[actor]) is not { } hearth) return null;
        var range = hearth.HouseholdId is null ? ResourceInteractionRange : 0;
        PlannedFireFuel fuel;
        if (CarriedFirewood(actor) is { } carried) fuel = new CarriedFireFuel(carried.Lot.Id);
        else if (FreeCarryCapacity(actor) > 0 && SharedItem("wood", actor, hearth.Position, range) is { } stock)
            fuel = new SharedFireFuel(new(stock.Id, stock.OwnerId, stock.ItemKind,
                HouseholdStockPosition(stock), HouseholdStockInteractionRange(stock)));
        else if (MaterialSource("wood", actor, hearth.Position, range) is { } source &&
            ProjectMaterialHarvest(actor, "wood", source) is { } harvest &&
            FreeCarryCapacity(actor) >= harvest.Quantity + harvest.TreeSeedQuantity)
        {
            var tool = harvest.ToolLotId is { } id ? InventoryIndex.For(society.Checkpoint.Inventory).Find(id) : null;
            fuel = new GatheredFireFuel(source, harvest, tool?.OwnerId, tool?.ItemKind);
        }
        else return null;
        return new(actor, hearth.InstanceId, hearth.Position, range, fuel,
            Reach(actor, hearth.Position, ReachPurpose.FireFuel, range, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckFirePlan(FireActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? FirePolicy);
        if (!capability.Allowed) return capability;
        if (survivalState is null || AccessibleHeatingBuildings(plan.Actor).SingleOrDefault(item => item.InstanceId == plan.BuildingId) is not { } hearth ||
            IsFireLit(hearth) || hearth.Position != plan.Destination ||
            (hearth.HouseholdId is null ? ResourceInteractionRange : 0) != plan.InteractionRange)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected unlit hearth is no longer available");
        if (plan.Fuel is not CarriedFireFuel && CarriedFirewood(plan.Actor) is not null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they already carry firewood for this hearth");
        switch (plan.Fuel)
        {
            case CarriedFireFuel carried:
                if (RecheckGoods(new(GoodsUse.Spend, plan.Actor, GoodsOwners.One(plan.Actor), GoodsKinds.One("wood")),
                        carried.StockId) is not { Lot: var fuel } || !PersonalEquipmentRules.IsCarried(fuel, plan.Actor))
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected firewood is no longer available");
                break;
            case SharedFireFuel shared:
                var pickup = shared.Pickup;
                if (RecheckGoods(SharedCollectionRequest(plan.Actor, pickup.OwnerId, "wood"), pickup.StockId) is not { Lot: var stock } ||
                    stock.ContainerLotId is not null || stock.CarrierId is not null || HouseholdStockPosition(stock) != pickup.Position ||
                    HouseholdStockInteractionRange(stock) != pickup.InteractionRange)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected shared firewood is no longer available");
                if (!CanPrepareSharedPickup(plan.Actor, stock, plan.Destination, plan.InteractionRange))
                    return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected firewood cannot return to the hearth");
                break;
            case GatheredFireFuel gathering:
                if (!map.Resources.Contains(gathering.Source) || resources.GetValueOrDefault(gathering.Source.Id) != ResourceState.Available ||
                    !EcologyRules.Harvest(worldSystems.Ecology.GetResource(gathering.Source.Id), 1).IsValid)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected firewood source is no longer available");
                if (TreeGrowthRules.IsWoodTree(gathering.Source.TreeKind) &&
                    (worldSystems.Ecology.GetResource(gathering.Source.Id).Quantity == 1) != (gathering.Harvest.TreeSeedQuantity > 0))
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected tree harvest has changed");
                if (gathering.Harvest.ToolLotId is { } toolId &&
                    (RecheckGoods(new(GoodsUse.CarriedInputs, plan.Actor, GoodsOwners.One(gathering.ToolOwnerId!), GoodsKinds.One(gathering.ToolKind!)),
                        toolId) is not { Lot: var tool } || !ToolProgressionRules.IsTopLevelCarriedLot(tool, plan.Actor)))
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected firewood gathering tool is no longer available");
                if (FreeCarryCapacity(plan.Actor) < gathering.Harvest.Quantity + gathering.Harvest.TreeSeedQuantity)
                    return ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected firewood harvest");
                if (HarvestReturnRoute(plan.Actor, gathering.Source, plan.Destination, ReachPurpose.FireFuel,
                    gathering.Harvest.Quantity + gathering.Harvest.TreeSeedQuantity, plan.InteractionRange).Count == 0)
                    return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected firewood harvest cannot return to the hearth");
                break;
        }
        return Reach(plan.Actor, plan.Destination, ReachPurpose.FireFuel, plan.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected hearth is out of reach now");
    }

    private void ApplyCheckedFirePlan(FireActionPlan plan)
    {
        switch (plan.Fuel)
        {
            case SharedFireFuel shared: ApplyPlannedEquipmentPickup(plan.Actor, shared.Pickup); return;
            case GatheredFireFuel gathering:
                GatherPlannedProjectMaterial(plan.Actor, agents[plan.Actor], "wood", gathering.Source, gathering.Harvest);
                return;
            case CarriedFireFuel carried:
                var person = agents[plan.Actor];
                if (!IsWithinInteractionRange(person.Position, plan.Destination, plan.InteractionRange))
                    MoveToward(plan.Actor, person, plan.Destination, "fuel_fire", plan.InteractionRange);
                else CompleteFireFuel(plan.Actor, worldSimulation.Buildings.Single(item => item.InstanceId == plan.BuildingId), carried.StockId);
                return;
        }
    }

    private WarmthActionPlan? PlanWarmth(string actor, ActorCapabilityPolicy? policy = null)
    {
        if (!ActorCapabilities.Check(CapabilitiesFor(actor), new WarmthChoice(WarmthChoiceKind.Protection).Encode(), policy ?? WarmthPolicy).Allowed) return null;
        var person = agents[actor];
        WarmthActionPlan Remain() => new(actor, WarmthDestinationKind.Remain, person.Position, 0, null, false, []);
        if (WarmthChange(person) >= 0) return Remain();
        var destination = ReachableWarmthDestinations(actor, person).FirstOrDefault();
        if (NearShelter(actor, person.Position) && (destination is null || !CanUseLitHearth(actor, destination))) return Remain();
        if (WeatherAt(person.Position) == WeatherKind.Storm && NaturalStormCover(person.Position) && destination is null) return Remain();
        if (destination is not null && WarmthDestinationPoint(actor, person, destination) is { } target)
        {
            var range = WarmthDestinationRange(actor, person.Position, destination);
            return new(actor, WarmthDestinationKind.Building, target, range, destination.InstanceId, CanUseLitHearth(actor, destination),
                Reach(actor, target, ReachPurpose.WarmthProtection, range, 0, SwimmingPolicy.WhenCapable));
        }
        if (WeatherAt(person.Position) == WeatherKind.Storm && !NaturalStormCover(person.Position) &&
            NearbyNaturalStormCover(actor, person.Position) is { } cover)
            return new(actor, WarmthDestinationKind.NaturalCover, cover, 0, null, false,
                Reach(actor, cover, ReachPurpose.WarmthProtection, 0, 0, SwimmingPolicy.WhenCapable));
        return Remain();
    }

    private bool KeepCurrentWarmthProtection(WarmthActionPlan plan)
    {
        var person = agents[plan.Actor];
        if (WarmthChange(person) >= 0) return true;
        return NearShelter(plan.Actor, person.Position) && !plan.Heating ||
            plan.Kind != WarmthDestinationKind.Building && WeatherAt(person.Position) == WeatherKind.Storm && NaturalStormCover(person.Position);
    }

    private ActionCheck CheckWarmthPlan(WarmthActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? WarmthPolicy);
        if (!capability.Allowed) return capability;
        if (KeepCurrentWarmthProtection(plan)) return new();
        if (plan.Kind == WarmthDestinationKind.Remain)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected cover no longer protects them");
        if (plan.Kind == WarmthDestinationKind.NaturalCover &&
            (WeatherAt(agents[plan.Actor].Position) != WeatherKind.Storm || !NaturalStormCover(plan.Destination)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected tree cover is no longer available");
        if (plan.Kind == WarmthDestinationKind.Building)
        {
            var building = AccessibleHeatingBuildings(plan.Actor).Where(IsFireLit).Concat(AccessibleShelters(plan.Actor))
                .FirstOrDefault(item => item.InstanceId == plan.BuildingId);
            if (building is null || plan.Heating && !CanUseLitHearth(plan.Actor, building) ||
                (!IsTownHall(building) ? building.Position != plan.Destination :
                    !WorldContentSimulationRules.Footprint(worldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building).Contains(plan.Destination)))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected warmth destination is no longer available");
        }
        return Reach(plan.Actor, plan.Destination, ReachPurpose.WarmthProtection, plan.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected warmth destination is out of reach now");
    }

    private void ApplyCheckedWarmthPlan(WarmthActionPlan plan)
    {
        if (KeepCurrentWarmthProtection(plan)) return;
        if (!IsWithinInteractionRange(agents[plan.Actor].Position, plan.Destination, plan.InteractionRange))
            MoveToward(plan.Actor, agents[plan.Actor], plan.Destination,
                plan.Kind == WarmthDestinationKind.NaturalCover ? "storm_cover" : "warmth", plan.InteractionRange);
    }

    private static ActionDefinition<FoodChoice, ConsumeFoodActionPlan> FoodConsumptionDefinition => SurvivalActions.Consumption;
    private static ActionDefinition<FoodChoice, SharedFoodActionPlan> SharedFoodDefinition => SurvivalActions.SharedFood;
    private static ActionDefinition<FoodChoice, FoodResourceActionPlan> FoodResourceDefinition => SurvivalActions.Resource;
    private static ActionDefinition<WarmthChoice, FireActionPlan> FireDefinition => SurvivalActions.Fire;
    private static ActionDefinition<WarmthChoice, WarmthActionPlan> WarmthDefinition => SurvivalActions.Warmth;

    private static class SurvivalActions
    {
        internal static readonly ActionDefinition<FoodChoice, ConsumeFoodActionPlan> Consumption = new(
            FoodChoice.TryDecode, static choice => choice.Encode(), FoodPolicy(consumption: true),
            static (world, actor, _, _) => world.PlanFoodConsumption(actor),
            static (world, plan, policy) => world.CheckFoodPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedFoodPlan(plan); return true; });

        internal static readonly ActionDefinition<FoodChoice, SharedFoodActionPlan> SharedFood = new(
            FoodChoice.TryDecode, static choice => choice.Encode(), FoodPolicy(consumption: false),
            static (world, actor, _, _) => world.PlanSharedFood(actor),
            static (world, plan, policy) => world.CheckFoodPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedFoodPlan(plan); return true; });

        internal static readonly ActionDefinition<FoodChoice, FoodResourceActionPlan> Resource = new(
            FoodChoice.TryDecode, static choice => choice.Encode(), FoodPolicy(consumption: false),
            static (world, actor, choice, _) => world.PlanFoodResource(actor, choice.Kind == FoodActionKind.Harvest),
            static (world, plan, policy) => world.CheckFoodPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedFoodPlan(plan); return true; });

        internal static readonly ActionDefinition<WarmthChoice, FireActionPlan> Fire = new(
            WarmthChoice.TryDecode, static choice => choice.Encode(), FirePolicy,
            static (world, actor, _, policy) => world.PlanFire(actor, policy),
            static (world, plan, policy) => world.CheckFirePlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedFirePlan(plan); return true; });

        internal static readonly ActionDefinition<WarmthChoice, WarmthActionPlan> Warmth = new(
            WarmthChoice.TryDecode, static choice => choice.Encode(), WarmthPolicy,
            static (world, actor, _, policy) => world.PlanWarmth(actor, policy),
            static (world, plan, policy) => world.CheckWarmthPlan(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedWarmthPlan(plan); return true; });
    }
}
