using System.Globalization;
using System.Text.Json;

namespace ClankerWorld.Simulation.World;

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
