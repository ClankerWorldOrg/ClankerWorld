using ClankerWorld.Simulation.Kernel;
using System.Globalization;
using System.Text.Json;

namespace ClankerWorld.Simulation.World;

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
