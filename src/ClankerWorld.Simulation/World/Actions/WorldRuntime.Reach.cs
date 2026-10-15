using System.Collections.ObjectModel;

namespace ClankerWorld.Simulation.World;

public enum ReachPurpose { ChildSocial, ChildHelping, HouseholdReturn, TownWarehouse, HouseholdPickup, HouseholdRecovery, FoodCargoStorage, FarmStockPickup, FarmStockDelivery, WorkstationSupply, MaterialGathering, StoreStock, SmithDelivery, ToolRepair, FieldWork, FieldSeedPickup, TreePlanting, HouseholdVessel, PrivateEquipment, EquipmentRepair, FireFuel, WarmthProtection, OrnamentPickup, FoodSource, HouseholdFood, AnimalProduct, AnimalSupply, AnimalWork, AnimalTrip, AnimalTravel, HandcartControl, HandcartPull, HandcartRepair, HandcartBuildMaterial, BoatDeparture, MedicalSupply, PracticalCare, GuardianTravel, Learning, HouseholdResume, ToolRequest, CivicVisit, LandSalePayment, NonLandService, TownProjectProposal, TownProjectDonation, TownProjectLoad, TownProjectWork, TownProjectRecovery, MarketStall, MarketStock, MarketExchange, DirectBarter, ShopBuy, ShopResponse, KnowledgeWriting, KnowledgeStorage, RecipeProduction, BuildingPreparation, ProjectAssistance, Construction, BuildingExpansion, ProjectInput, PersonalGoods, Exploration, Movement, PickupApproach, PickupReturn, GoodsCollection, GoodsDelivery, GoodsRecovery }
public enum SwimmingPolicy { WhenCapable, Never }
public enum ReachObstacles { OccupiedTiles, Terrain }

public sealed partial class WorldRuntime
{
    // A request proves the communal asset's geography before a passenger boards.
    // This prospective water leg starts from that exact boat, without reserving it.
    private IReadOnlyList<GridPoint> ReachBoatGeography(BoatState boat, PlacedBuilding destination) =>
        TransportSystem.ReachBoatGeography(BoatWorld, boat, destination);

    // The first leg always starts at the current actor position. A return leg
    // names its prospective origin separately instead of using a camp origin.
    private ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose,
        int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = ReachObstacles.OccupiedTiles) =>
        ReachLeg(actor, agents[actor].Position, target, purpose, range, load, swimming, obstacles);

    private bool CanReach(string actor, GridPoint target, ReachPurpose purpose,
        int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = ReachObstacles.OccupiedTiles) =>
        IsWithinInteractionRange(agents[actor].Position, target, range) ||
        Reach(actor, target, purpose, range, load, swimming, obstacles).Count > 0;

    private bool CanReachLeg(string actor, GridPoint origin, GridPoint target, ReachPurpose purpose,
        int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = ReachObstacles.OccupiedTiles) =>
        IsWithinInteractionRange(origin, target, range) ||
        ReachLeg(actor, origin, target, purpose, range, load, swimming, obstacles).Count > 0;

    // Geography eligibility ignores temporary occupants and needs no physical route.
    private bool CanReachTerrain(string actor, GridPoint target, ReachPurpose purpose, int load, SwimmingPolicy swimming)
    {
        var origin = agents[actor].Position;
        return map.IsReachableOnFoot(origin, target) ||
            swimming == SwimmingPolicy.WhenCapable && SwimmingRules.IsReachable(map, origin, target) &&
            CanSwim(actor, additionalCarriedUnits: load);
    }

    private ReadOnlyCollection<GridPoint> HarvestReturnRoute(string actor, MapResource source, GridPoint destination,
        ReachPurpose purpose, int load, int destinationRange = 0)
    {
        if (PickupPosition(actor, source.Position, ResourceInteractionRange) is not { } origin) return Array.AsReadOnly<GridPoint>([]);
        if ((destinationRange > 0 || map.IsReachableOnFoot(origin, destination)) && ReachLeg(actor, origin, destination, purpose, destinationRange, 0, SwimmingPolicy.Never) is { Count: > 0 } foot)
            return foot;
        var swimmingRoom = Math.Max(0, SwimmingRules.MaximumCarriedUnits -
            PersonalEquipmentRules.CarriedQuantity(society.Checkpoint.Inventory, actor, agents[actor].Equipment));
        return load <= swimmingRoom && load <= PickupCarryCapacity(actor, source.Position, destination, destinationRange, ResourceInteractionRange)
            ? ReachLeg(actor, origin, destination, purpose, destinationRange, load, SwimmingPolicy.WhenCapable) : Array.AsReadOnly<GridPoint>([]);
    }

    private ReadOnlyCollection<GridPoint> ReachLeg(string actor, GridPoint origin, GridPoint target,
        ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = ReachObstacles.OccupiedTiles)
    {
        if (obstacles == ReachObstacles.Terrain)
        {
            // Field intentions keep terrain eligibility while another person
            // briefly blocks the actual route. Movement still checks occupancy.
            if (map.IsReachableOnFoot(origin, target))
                return SharedUnoccupiedRoute(origin, [], target, range).AsReadOnly();
            return swimming == SwimmingPolicy.WhenCapable && CanSwim(actor, origin, load)
                ? SharedUnoccupiedRoute(origin, [], target, range, swimming: true).AsReadOnly()
                : Array.AsReadOnly<GridPoint>([]);
        }
        return FindUnoccupiedRoute(actor, origin, target, range,
            allowSwimming: swimming == SwimmingPolicy.WhenCapable, additionalCarriedUnits: load).AsReadOnly();
    }
}
