using System.Text.Json;
using ClankerWorld.Simulation.Kernel;
using System.Globalization;

namespace ClankerWorld.Simulation.World;

public enum TownProjectActionFamily { Load, Work, Recovery, Donation }

public sealed record TownProjectActionRequest(TownProjectActionFamily Family, string CandidateId)
{
    public string Encode() => CandidateId;
    public static TownProjectActionRequest? Decode(string candidate)
    {
        foreach (var (prefix, family) in new[]
        {
            ("town_project_supply:", TownProjectActionFamily.Load), ("town_project_deliver:", TownProjectActionFamily.Load),
            ("town_project_work:", TownProjectActionFamily.Work), ("town_project_gather:", TownProjectActionFamily.Work),
            ("town_project_recover:", TownProjectActionFamily.Recovery), ("town_project_return:", TownProjectActionFamily.Recovery),
            ("town_project_donate:", TownProjectActionFamily.Donation),
        })
            if (candidate.StartsWith(prefix, StringComparison.Ordinal)) return new(family, candidate);
        return null;
    }
}

public sealed record TownProjectWorkChoice(string ProjectId, string? ItemKind)
{
    public string Encode() => ItemKind is null
        ? DerivedId.Hash(JsonSerializer.Serialize(new[] { ProjectId }), "town_project_work:")
        : DerivedId.Hash(JsonSerializer.Serialize(new[] { ProjectId, ItemKind }), "town_project_gather:");
}

public sealed record TownProjectWorkActionPlan(string ActorId, TownProjectWorkChoice Choice, string TownId,
    string ProposalId, TownProjectPayload Project, GridPoint Destination, ToolWorkPlan? Hammer,
    IReadOnlyList<TownProjectDelivery> DeliveredMaterials, MapResource? Source, ToolGatheringPlan? Harvest,
    string? ToolOwnerId, string? ToolKind, PlannedEquipmentPickup? ToolPickup, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.ItemKind is null ? "work on that selected Town project" : "gather those selected personal project materials";
}

public enum TownProjectLoadKind { Supply, Deliver }

public sealed record TownProjectLoadChoice(TownProjectLoadKind Kind, string ProjectId, string StockId, int Quantity, string? DeliveryId)
{
    public string Encode() => Kind == TownProjectLoadKind.Deliver
        ? DerivedId.Hash(JsonSerializer.Serialize(new[] { ProjectId, DeliveryId! }), "town_project_deliver:")
        : DerivedId.Hash(JsonSerializer.Serialize(new[] { ProjectId, StockId, Quantity.ToString(CultureInfo.InvariantCulture) }), "town_project_supply:");
}

public sealed record TownProjectLoadActionPlan(string ActorId, TownProjectLoadChoice Choice, string TownId,
    string ProposalId, TownProjectPayload Project, string ItemKind, GridPoint Destination, GridPoint Target,
    int InteractionRange, string? CarrierId, string? StorageBuildingId, InventoryGroundPosition? GroundPosition,
    TownProjectDelivery? Delivery, IReadOnlyList<GridPoint> Route, IReadOnlyList<GridPoint> DeliveryRoute)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "carry that selected Town project load";
}

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

public sealed record TownProjectDonationChoice(string ProjectId, string StockId, int Quantity, bool AtSite)
{
    public string Encode() => DerivedId.Hash(JsonSerializer.Serialize(new[] { ProjectId, StockId,
        Quantity.ToString(CultureInfo.InvariantCulture), AtSite ? "at-site" : "travel" }), "town_project_donate:");
}

public sealed record TownProjectDonationActionPlan(string ActorId, TownProjectDonationChoice Choice, string TownId,
    string ProposalId, TownProjectPayload Project, string ItemKind, GridPoint Destination, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "donate that selected personal contribution";
}
