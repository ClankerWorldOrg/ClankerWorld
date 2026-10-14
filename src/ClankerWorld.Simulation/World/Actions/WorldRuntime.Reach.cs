using System.Collections.ObjectModel;

namespace ClankerWorld.Simulation.World;

public enum ReachPurpose { ChildSocial, ChildHelping, HouseholdReturn, TownWarehouse, HouseholdPickup, HouseholdRecovery, FoodCargoStorage, FarmStockPickup, FarmStockDelivery, WorkstationSupply, MaterialGathering, StoreStock }
public enum SwimmingPolicy { WhenCapable, Never }

public sealed partial class WorldRuntime
{
    // The first leg always starts at the current actor position. A return leg
    // names its prospective origin separately instead of using a camp origin.
    private ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose,
        int range, int load, SwimmingPolicy swimming) =>
        ReachLeg(actor, agents[actor].Position, target, purpose, range, load, swimming);

    private ReadOnlyCollection<GridPoint> HarvestReturnRoute(string actor, MapResource source, GridPoint destination,
        ReachPurpose purpose, int load)
    {
        if (PickupPosition(actor, source.Position, ResourceInteractionRange) is not { } origin) return Array.AsReadOnly<GridPoint>([]);
        if (map.IsReachableOnFoot(origin, destination) && ReachLeg(actor, origin, destination, purpose, 0, 0, SwimmingPolicy.Never) is { Count: > 0 } foot)
            return foot;
        var swimmingRoom = Math.Max(0, SwimmingRules.MaximumCarriedUnits -
            PersonalEquipmentRules.CarriedQuantity(society.Checkpoint.Inventory, actor, agents[actor].Equipment));
        return load <= swimmingRoom && load <= PickupCarryCapacity(actor, source.Position, destination, 0, ResourceInteractionRange)
            ? ReachLeg(actor, origin, destination, purpose, 0, load, SwimmingPolicy.WhenCapable) : Array.AsReadOnly<GridPoint>([]);
    }

    private ReadOnlyCollection<GridPoint> ReachLeg(string actor, GridPoint origin, GridPoint target,
        ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) =>
        FindUnoccupiedRoute(actor, origin, target, range,
            allowSwimming: swimming == SwimmingPolicy.WhenCapable, additionalCarriedUnits: load).AsReadOnly();
}
