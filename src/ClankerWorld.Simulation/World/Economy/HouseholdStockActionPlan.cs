namespace ClankerWorld.Simulation.World;

public enum HouseholdStockActionKind { RecoverDelivery, Deliver, StorePreparationTool, Collect, StoreFood }

public enum HouseholdStockRequestKind { RecoverDelivery, Haul, StoreFood }

public readonly record struct HouseholdStockRequest(HouseholdStockRequestKind Kind)
{
    public string Encode() => Kind switch
    {
        HouseholdStockRequestKind.RecoverDelivery => "recover_household_delivery",
        HouseholdStockRequestKind.StoreFood => "store_household_food",
        _ => "haul_household_stock",
    };

    public static bool TryDecode(string candidate, out HouseholdStockRequest choice)
    {
        choice = candidate switch
        {
            "recover_household_delivery" => new(HouseholdStockRequestKind.RecoverDelivery),
            "store_household_food" => new(HouseholdStockRequestKind.StoreFood),
            _ => new(HouseholdStockRequestKind.Haul),
        };
        return candidate == choice.Encode();
    }

    public static HouseholdStockRequest ForStep(HouseholdStockActionKind kind) => new(kind switch
    {
        HouseholdStockActionKind.RecoverDelivery => HouseholdStockRequestKind.RecoverDelivery,
        HouseholdStockActionKind.StoreFood => HouseholdStockRequestKind.StoreFood,
        _ => HouseholdStockRequestKind.Haul,
    });
}

public sealed record HouseholdStockActionPlan(string ActorId, HouseholdStockActionKind Kind,
    string HouseholdId, string StockId, string OwnerId, string ItemKind, int Quantity,
    int PhysicalQuantity, GridPoint Destination, int InteractionRange, string? HouseId,
    string? DeliveryBuildingId, IReadOnlyList<GridPoint> Route,
    IReadOnlyList<PlannedStockMember>? Family = null) : ActionPlan(ActorId, HouseholdStockRequest.ForStep(Kind).Encode())
{
    public override string Activity => Kind == HouseholdStockActionKind.RecoverDelivery
        ? "return the unusable supplies" : "take supplies to the household House";
}
