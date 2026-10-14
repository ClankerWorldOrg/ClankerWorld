using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy FarmStockPolicy => new(ActionLifeStages.Adults,
        AllowUrgentFood: false, AllowUrgentWarmth: true, AllowActiveOrder: true);

    private FarmStockActionPlan? PlanFarmStockAction(string actor, FarmStockActionKind kind)
    {
        var candidateId = kind == FarmStockActionKind.Grain ? "haul_farm_grain" : "haul_farm_flour";
        if (!ActorCapabilities.Check(CapabilitiesFor(actor), candidateId, FarmStockPolicy).Allowed ||
            HouseholdFor(actor) is not { } household || CarriedHouseDelivery(actor) is not null ||
            FarmhouseForHousehold(household) is not { } farmhouse)
            return null;
        FarmStockChoice? choice;
        PlacedBuilding? destination;
        if (kind == FarmStockActionKind.Grain)
        {
            choice = FarmGrainForDelivery(household, actor);
            destination = choice is null ? null : FarmStorageForHaul(household, actor, choice);
        }
        else
        {
            destination = HouseForHousehold(household);
            choice = destination is null ? null : FarmFlourForHouse(household, farmhouse.InstanceId, actor, destination.InstanceId);
        }
        if (choice is null || destination is null || PlanFarmStockHaul(actor, destination.InstanceId, choice) is not { } haul)
            return null;
        var source = HouseholdStockPosition(choice.Carrier);
        var range = HouseholdStockInteractionRange(choice.Carrier);
        var route = Reach(actor, source, ReachPurpose.FarmStockPickup, range, 0, SwimmingPolicy.WhenCapable);
        var returnRoute = ReachLeg(actor, source, destination.Position, ReachPurpose.FarmStockDelivery, 0, 0, SwimmingPolicy.WhenCapable);
        if (!IsWithinInteractionRange(agents[actor].Position, source, range) && route.Count == 0 || returnRoute.Count == 0)
            return null;
        var inventory = society.Checkpoint.Inventory;
        return new(actor, kind, household, choice.Carrier.Id, choice.Resource.Id, choice.Resource.ItemKind,
            haul.TransferQuantity, haul.ResourceQuantity,
            haul.MoveContainerFamily ? ContainerFamilyQuantity(inventory, choice.Carrier.Id) : haul.TransferQuantity,
            haul.MoveContainerFamily, destination.InstanceId, destination.Position, source, range,
            choice.Carrier.StorageBuildingId, choice.ResourceQuantityLimit,
            haul.MoveContainerFamily ? GoodsPlanFamily.Capture(inventory, choice.Carrier) : null, route, returnRoute);
    }

    private ActionCheck CheckFarmStockAction(FarmStockActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, FarmStockPolicy);
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || FarmhouseForHousehold(plan.HouseholdId) is not { } farmhouse ||
            CarriedHouseDelivery(plan.Actor) is not null)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household or delivery work has changed");
        var destination = worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == plan.DestinationId);
        if (destination is null || destination.HouseholdId != plan.HouseholdId || destination.Position != plan.Destination ||
            (plan.Kind == FarmStockActionKind.Flour && HouseForHousehold(plan.HouseholdId)?.InstanceId != plan.DestinationId ||
             plan.Kind == FarmStockActionKind.Grain && destination.InstanceId != farmhouse.InstanceId &&
                HouseholdBuildingWithTag(plan.HouseholdId, "silo")?.InstanceId != plan.DestinationId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected farm storage has changed");
        var inventory = society.Checkpoint.Inventory;
        var request = DeliveryGoodsRequest(plan.Actor, plan.HouseholdId, plan.ItemKind, plan.DestinationId, GoodsDeliveryPurpose.FarmStock);
        if (RecheckGoods(request, plan.ResourceId) is not { } match || match.Root.Id != plan.CarrierId ||
            !plan.MoveContainerFamily && match.Quantity < plan.ResourceQuantity || match.Root.CarrierId is not null ||
            match.Root.StorageBuildingId != plan.SourceBuildingId || HouseholdStockPosition(match.Root) != plan.Source ||
            HouseholdStockInteractionRange(match.Root) != plan.SourceRange ||
            !GoodsPlanFamily.Matches(inventory, match.Root, plan.Family) ||
            InventoryContainerRules.IsContainer(match.Root.ItemKind) && HasActiveContainerReservation(inventory, match.Root.Id) ||
            plan.MoveContainerFamily && UnusableDeliveryStock(inventory, match.Root))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected farm load is no longer available");
        if (plan.Kind == FarmStockActionKind.Flour && plan.SourceBuildingId != farmhouse.InstanceId ||
            plan.ResourceQuantityLimit is not null && FarmhouseGrainDeficit(plan.HouseholdId, farmhouse) < plan.ResourceQuantity)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the Farmhouse no longer needs this load");
        if (PickupCarryCapacity(plan.Actor, match.Root, plan.Destination) < plan.PhysicalQuantity ||
            DestinationRoom(plan.DestinationId) < plan.PhysicalQuantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected load no longer fits");
        if (!IsWithinInteractionRange(agents[plan.Actor].Position, plan.Source, plan.SourceRange) &&
            Reach(plan.Actor, plan.Source, ReachPurpose.FarmStockPickup, plan.SourceRange, 0, SwimmingPolicy.WhenCapable).Count == 0 ||
            ReachLeg(plan.Actor, plan.Source, plan.Destination, ReachPurpose.FarmStockDelivery, 0, 0, SwimmingPolicy.WhenCapable).Count == 0)
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected farm trip is out of reach now");
        return new();
    }

    private int FarmhouseGrainDeficit(string household, PlacedBuilding farmhouse) => worldContent.Recipes
        .Where(recipe => recipe.WorkstationBuildingId == farmhouse.DefinitionId && !recipe.IsCrop && NeedsRecipeOutput(recipe, household))
        .SelectMany(recipe => recipe.Inputs).Where(input => input.ResourceId == FarmFieldRules.Grain)
        .Select(input => input.Amount).DefaultIfEmpty(0).Max() * SupplyBatches -
        FarmSuppliedQuantity(household, farmhouse.InstanceId) - FarmIncomingQuantity(farmhouse.InstanceId);

    private void ApplyFarmStockAction(FarmStockActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckFarmStockAction(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        var state = agents[plan.Actor];
        if (!IsWithinInteractionRange(state.Position, plan.Source, plan.SourceRange))
        {
            MoveToward(plan.Actor, state, plan.Source, plan.Kind == FarmStockActionKind.Grain ? "farm_grain" : "farm_flour", plan.SourceRange);
            return;
        }
        var inventory = society.Checkpoint.Inventory;
        var haul = new FarmStockHaulPlan(inventory.GetLot(plan.CarrierId), inventory.GetLot(plan.ResourceId),
            plan.TransferQuantity, plan.ResourceQuantity, plan.MoveContainerFamily);
        ApplyFarmStockHaul(plan.Actor, plan.HouseholdId, plan.DestinationId, haul,
            plan.Kind == FarmStockActionKind.Grain ? DerivedId.Create($"farm-grain-pickup:{WorldTick}:{plan.Actor}") : DerivedId.Create($"farm-flour-pickup:{WorldTick}:{plan.Actor}"),
            plan.Kind == FarmStockActionKind.Grain ? "farm_grain_picked_up" : "farm_flour_picked_up");
    }
}
