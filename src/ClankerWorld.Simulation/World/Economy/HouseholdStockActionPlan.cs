namespace ClankerWorld.Simulation.World;

public enum HouseholdStockActionKind { RecoverDelivery, Deliver, StorePreparationTool, Collect, StoreFood }

public sealed record HouseholdStockActionPlan(string ActorId, HouseholdStockActionKind Kind,
    string HouseholdId, string StockId, string OwnerId, string ItemKind, int Quantity,
    int PhysicalQuantity, GridPoint Destination, int InteractionRange, string? HouseId,
    string? DeliveryBuildingId, IReadOnlyList<GridPoint> Route,
    IReadOnlyList<PlannedStockMember>? Family = null) : ActionPlan(ActorId, Kind switch
    {
        HouseholdStockActionKind.RecoverDelivery => "recover_household_delivery",
        HouseholdStockActionKind.StoreFood => "store_household_food",
        _ => "haul_household_stock",
    })
{
    public override string Activity => Kind == HouseholdStockActionKind.RecoverDelivery
        ? "return the unusable supplies" : "take supplies to the household House";
}
