namespace ClankerWorld.Simulation.World;

public enum TownStockActionKind { StoreSurplus, Salvage }

public readonly record struct TownStockRequest(TownStockActionKind Kind, string CandidateId)
{
    public const string WarehouseCandidate = "store_town_resources";
    public string Encode() => CandidateId;

    public static bool TryDecodeWarehouse(string candidate, out TownStockRequest choice)
    {
        choice = new(TownStockActionKind.StoreSurplus, candidate);
        return candidate == WarehouseCandidate;
    }

    public static bool TryDecodeSalvage(string candidate, out TownStockRequest choice)
    {
        choice = new(TownStockActionKind.Salvage, candidate);
        return candidate.StartsWith(WorldRuntime.CivicSharedRules.TownSalvagePrefix, StringComparison.Ordinal);
    }
}

public readonly record struct TownStockChoice(TownStockActionKind Kind, string TownId, string StockId)
{
    public string Id => Kind switch
    {
        TownStockActionKind.StoreSurplus => "store_town_resources",
        TownStockActionKind.Salvage => TownSystem.TownSalvageId(TownId, StockId),
        _ => throw new InvalidOperationException("Unknown Town stock choice."),
    };
}

public sealed record TownStockActionPlan(string ActorId, TownStockChoice Choice, string ItemKind,
    int Quantity, GridPoint Destination, int InteractionRange, string? WarehouseId = null,
    IReadOnlyList<GridPoint>? Route = null, IReadOnlyList<PlannedStockMember>? Family = null) : ActionPlan(ActorId, Choice.Id)
{
    public override string Activity => Choice.Kind == TownStockActionKind.StoreSurplus
        ? "take spare goods to the Town Warehouse" : "salvage the communal goods";
}
