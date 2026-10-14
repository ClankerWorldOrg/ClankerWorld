using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static readonly ActorCapabilityPolicy FirePolicy = new(ActionLifeStages.Adults,
        AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);
    private static readonly ActorCapabilityPolicy WarmthPolicy = new(ActionLifeStages.NonInfants,
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

    private void ApplyFirePlan(FireActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedFirePlan(plan);
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

    private void ApplyWarmthPlan(WarmthActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedWarmthPlan(plan);
    }

    private void ApplyCheckedWarmthPlan(WarmthActionPlan plan)
    {
        if (KeepCurrentWarmthProtection(plan)) return;
        if (!IsWithinInteractionRange(agents[plan.Actor].Position, plan.Destination, plan.InteractionRange))
            MoveToward(plan.Actor, agents[plan.Actor], plan.Destination,
                plan.Kind == WarmthDestinationKind.NaturalCover ? "storm_cover" : "warmth", plan.InteractionRange);
    }
}
