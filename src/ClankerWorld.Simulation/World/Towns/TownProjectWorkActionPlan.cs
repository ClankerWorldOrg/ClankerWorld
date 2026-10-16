using System.Text.Json;

namespace ClankerWorld.Simulation.World;

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
