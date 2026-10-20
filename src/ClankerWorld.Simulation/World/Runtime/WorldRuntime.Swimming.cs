using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private bool IsSwimming(string actor, GridPoint position) =>
        PassengerBoat(actor) is null && SwimmingRules.IsSwimmingWater(map, position);

    private bool CanSwim(string actor, GridPoint? origin = null, int additionalCarriedUnits = 0) =>
        PassengerBoat(actor) is null && AttachedHandcart(actor) is null &&
        !animalWorld.Animals.Any(animal => animal.RiderId == actor || animal.LeaderId == actor) &&
        // A swimmer can always make their way out if their condition changes mid-crossing.
        (SwimmingRules.IsSwimmingWater(map, origin ?? agents[actor].Position) ||
         SwimmingStartingCondition(actor, additionalCarriedUnits) &&
         MovingCareGroup(actor).All(id => id == actor || SwimmingStartingCondition(id)) &&
         !agents.Values.Any(child => child.GuardianPlacement?.CaregiverId == actor));

    private bool SwimmingStartingCondition(string actor, int additionalCarriedUnits = 0)
    {
        var person = agents[actor];
        return person.GuardianPlacement is null && !LifeStageRules.IsInfant(society.Checkpoint.GetAgent(actor).AgeBand) &&
            (person.Survival?.WarmthBasisPoints ?? 10_000) >= SwimmingRules.MinimumStartingWarmth &&
            (person.Survival?.IllnessBasisPoints ?? 0) <= SwimmingRules.MaximumStartingIllness &&
            PersonalEquipmentRules.CarriedQuantity(society.Checkpoint.Inventory, actor, person.Equipment) <=
                SwimmingRules.MaximumCarriedUnits - additionalCarriedUnits &&
            AttachedHandcart(actor) is null &&
            !animalWorld.Animals.Any(animal => animal.RiderId == actor || animal.LeaderId == actor);
    }

    // Preserve occupancy-independent task selection while permitting an eligible freshwater route.
    private bool CanReachByFootOrSwimming(string actor, GridPoint from, GridPoint destination) =>
        map.IsReachableOnFoot(from, destination) ||
        SwimmingRules.IsReachable(map, from, destination) && CanSwim(actor);

    private int AgentStepCost(GridPoint from, GridPoint to) =>
        SwimmingRules.IsSwimmingWater(map, from) || SwimmingRules.IsSwimmingWater(map, to)
            ? SwimmingRules.StepCost : RoadStepCost(from, to);

    private int LegalAgentStepCost(GridPoint from, GridPoint to) =>
        SwimmingRules.IsSwimmingWater(map, from) || SwimmingRules.IsSwimmingWater(map, to)
            ? SwimmingRules.StepCost : LegalRoadStepCost(from, to);

    // Plan from the tile where the real pickup will happen, including an
    // interaction beside a resource or storage building. No inventory is changed.
    private GridPoint? PickupPosition(string actor, GridPoint source, int sourceRange)
    {
        var position = agents[actor].Position;
        if (IsWithinInteractionRange(position, source, sourceRange)) return position;
        var approach = Reach(actor, source, ReachPurpose.PickupApproach, sourceRange, 0, SwimmingPolicy.WhenCapable);
        return approach.Count == 0 ? null : approach[^1];
    }

    private int PickupCarryCapacity(string actor, GridPoint source, GridPoint destination,
        int destinationRange = 0, int sourceRange = 0)
    {
        if (PickupPosition(actor, source, sourceRange) is not { } origin) return 0;
        var capacity = FreeCarryCapacity(actor);
        if ((destinationRange > 0 || map.IsReachableOnFoot(origin, destination)) &&
            HasUnoccupiedRoute(actor, origin, destination, destinationRange, allowSwimming: false))
            return capacity;
        var swimmingRoom = Math.Max(0, SwimmingRules.MaximumCarriedUnits -
            PersonalEquipmentRules.CarriedQuantity(society.Checkpoint.Inventory, actor, agents[actor].Equipment));
        return swimmingRoom > 0 && HasUnoccupiedRoute(actor, origin, destination, destinationRange,
            additionalCarriedUnits: 1) ? Math.Min(capacity, swimmingRoom) : 0;
    }

    private int PickupCarryCapacity(string actor, ClankerWorld.Simulation.Kernel.InventoryLot stock,
        GridPoint destination, int destinationRange = 0) =>
        PickupCarryCapacity(actor, HouseholdStockPosition(stock), destination,
            destinationRange, HouseholdStockInteractionRange(stock));

    private bool CanPrepareSharedPickup(string actor, ClankerWorld.Simulation.Kernel.InventoryLot stock,
        GridPoint destination, int destinationRange)
    {
        if (PickupPosition(actor, HouseholdStockPosition(stock), HouseholdStockInteractionRange(stock)) is not { } origin)
            return false;
        // Foot preparation may store spare cargo before taking an ingredient.
        // Its existing caller still makes and checks the actual carrying room.
        return (destinationRange > 0 || map.IsReachableOnFoot(origin, destination)) &&
            HasUnoccupiedRoute(actor, origin, destination, destinationRange, allowSwimming: false) ||
            PickupCarryCapacity(actor, stock, destination, destinationRange) >= 1;
    }

    internal bool CanReturnWithHarvest(string actor, string itemKind, ClankerWorld.Simulation.World.MapResource source,
        GridPoint destination, int destinationRange = 0, bool useHarvestBonus = true)
    {
        if (PickupPosition(actor, source.Position, ResourceInteractionRange) is not { } origin) return false;
        // Existing foot gathering may first make room. Keep that behavior.
        if ((destinationRange > 0 || map.IsReachableOnFoot(origin, destination)) &&
            HasUnoccupiedRoute(actor, origin, destination, destinationRange, allowSwimming: false))
            return true;
        var required = ProjectMaterialCarryUnits(actor, itemKind, source, useHarvestBonus);
        var swimmingRoom = Math.Max(0, SwimmingRules.MaximumCarriedUnits -
            PersonalEquipmentRules.CarriedQuantity(society.Checkpoint.Inventory, actor, agents[actor].Equipment));
        // After the foot return failed, an oversized harvest cannot gain a route by searching again.
        return required <= swimmingRoom && required <=
            PickupCarryCapacity(actor, source.Position, destination, destinationRange, ResourceInteractionRange);
    }
}
