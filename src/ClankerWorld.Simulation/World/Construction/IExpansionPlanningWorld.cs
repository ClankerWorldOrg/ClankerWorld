using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IExpansionPlanningWorld
{
    SocietyCheckpoint Society { get; }
    int ResourceInteractionRange { get; }
    DeclarativeWorldContentState Content { get; }
    ActorCapabilityState? CapabilitiesFor(string actor);
    bool IsAdult(string actor);
    string HouseholdFor(string actor);
    string? TownForResident(string actor);
    int StoredQuantity(string building);
    HouseResidentCapacityRules.Capacity? HouseResidentCapacity(string household);
    int AnimalYardCapacity(PlacedBuilding building);
    (GridPoint Position, BuildingFootprintRevision Footprint) ExpansionShapeForOrder(PlacedBuilding building, OwnerQueuedInstruction? instruction);
    ExpansionMaterialStep? PlanExpansionMaterialStep(string actor, PlacedBuilding building, IReadOnlyList<ContentQuantity> costs);
    ActionCheck CheckExpansionMaterialStep(ExpansionActionPlan plan, PlacedBuilding building);
    IEnumerable<InventoryLot> ExpansionMaterialLots(string actor, PlacedBuilding building);
    int AvailableLotQuantity(InventoryLot lot);
    bool CanFitExpansion(PlacedBuilding building, GridPoint position, BuildingFootprintRevision footprint, out string failure);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
}
