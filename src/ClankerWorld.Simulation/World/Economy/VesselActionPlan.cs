using System.Globalization;

namespace ClankerWorld.Simulation.World;

public enum VesselActionKind { CollectJug, FillJug, ReturnJug, StoreFood, TakeFood, ReturnEmpty }

public sealed record VesselChoice(VesselActionKind Kind, GridPoint? Shore = null, string? StockId = null)
{
    public const string FillPrefix = "fill_water_jug:";
    public const string EmptyPrefix = "return_empty_vessel:";
    public string Encode() => Kind switch
    {
        VesselActionKind.CollectJug => "collect_water_jug",
        VesselActionKind.ReturnJug => "return_water_jug",
        VesselActionKind.StoreFood => "store_food_in_pot",
        VesselActionKind.TakeFood => "take_food_from_pot",
        VesselActionKind.ReturnEmpty => EmptyPrefix + StockId,
        _ => $"{FillPrefix}{Shore!.Value.X.ToString(CultureInfo.InvariantCulture)},{Shore.Value.Y.ToString(CultureInfo.InvariantCulture)}",
    };

    public static VesselChoice? Decode(string candidate)
    {
        if (candidate.StartsWith(EmptyPrefix, StringComparison.Ordinal)) return new(VesselActionKind.ReturnEmpty, StockId: candidate[EmptyPrefix.Length..]);
        if (candidate.StartsWith(FillPrefix, StringComparison.Ordinal))
        {
            var coordinates = candidate[FillPrefix.Length..].Split(',', StringSplitOptions.TrimEntries);
            return coordinates.Length == 2 && int.TryParse(coordinates[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) &&
                int.TryParse(coordinates[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ? new(VesselActionKind.FillJug, new(x, y)) : null;
        }
        return candidate switch
        {
            "collect_water_jug" => new(VesselActionKind.CollectJug),
            "return_water_jug" => new(VesselActionKind.ReturnJug),
            "store_food_in_pot" => new(VesselActionKind.StoreFood),
            "take_food_from_pot" => new(VesselActionKind.TakeFood),
            _ => null,
        };
    }
}

public sealed record PlannedPotFood(string StockId, string OwnerId, string ItemKind);

public sealed record VesselActionPlan(string ActorId, VesselChoice Choice, string HouseholdId,
    string HouseId, GridPoint HousePosition, string VesselId, string VesselOwner, string VesselKind,
    GridPoint Target, string? SourceBuildingId, int Quantity, IReadOnlyList<PlannedStockMember>? Family,
    PlannedPotFood? Food, GridPoint? Shore, GridPoint? ReturnTo,
    IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind switch
    {
        VesselActionKind.CollectJug => "collect the selected water jug",
        VesselActionKind.FillJug => "fill the selected water jug",
        VesselActionKind.ReturnJug => "return the selected water jug",
        VesselActionKind.StoreFood => "store the selected food in the pot",
        VesselActionKind.TakeFood => "take the selected serving from the pot",
        _ => "return the selected empty vessel",
    };
}
