using System.Globalization;
using System.Text.Json;

namespace ClankerWorld.Simulation.World;

public sealed record TownProjectRecoveryChoice(bool Recover, string ProjectId, string StockId, int Quantity, string? WarehouseId)
{
    public string Encode() => Recover
        ? DerivedId.Hash(JsonSerializer.Serialize(new[] { ProjectId, StockId, Quantity.ToString(CultureInfo.InvariantCulture), WarehouseId! }), "town_project_recover:")
        : DerivedId.Hash(JsonSerializer.Serialize(new[] { ProjectId, StockId, WarehouseId ?? "ground" }), "town_project_return:");
}

public sealed record TownProjectRecoveryActionPlan(string ActorId, TownProjectRecoveryChoice Choice, string TownId,
    TownProjectPayload Project, string ItemKind, TownProjectDelivery ReleasedDelivery, GridPoint Target,
    GridPoint Destination, IReadOnlyList<GridPoint> Route, IReadOnlyList<GridPoint> ReturnRoute)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "return that selected released Town project load";
}
