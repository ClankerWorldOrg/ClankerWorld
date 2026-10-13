using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void AddFoodPlanCandidate(List<CognitionCandidate> candidates, FoodActionPlan? plan,
        string description, int priority, string? destination = null)
    {
        if (plan is not null && CheckFoodPlan(plan).Allowed)
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

    private ActionCheck CheckFoodPlan(FoodActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, FoodPolicy(plan is ConsumeFoodActionPlan));
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
                    (shared.DependentId is null ? !MayCollectSharedFood(plan.Actor) : !ChildrenNeedingCare(plan.Actor).Any(child => child.AgentId == shared.DependentId)))
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
                if (!CanReachByFootOrSwimming(plan.Actor, agents[plan.Actor].Position, gathering.Source.Position) ||
                    Reach(plan.Actor, gathering.Source.Position, ReachPurpose.FoodSource, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count == 0)
                    return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected food source is out of reach now");
                return gathering.Selected.Kind != FoodActionKind.Harvest || IsWithinInteractionRange(agents[plan.Actor].Position,
                    gathering.Source.Position, ResourceInteractionRange) ? new() :
                    ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "they are no longer beside the selected food source");
            default: throw new InvalidOperationException("A food plan has no registered operation.");
        }
    }

    private void ApplyFoodPlan(FoodActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckFoodPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
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
}
