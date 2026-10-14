namespace ClankerWorld.Simulation.World;

public sealed record HouseholdLandFilingChoice(string TownId, string? BuildingId, string PlotToken)
{
    public string Encode() => $"civic|{TownId}|{(BuildingId is null ? "request_land_use" : "request_expansion_land")}|{BuildingId ?? ""}|{PlotToken}";
    public static HouseholdLandFilingChoice? Decode(string value) => value.Split('|') is ["civic", var town, var verb, var building, var plot] && town.Length > 0 &&
        (verb == "request_land_use" && building.Length == 0 && plot.Length == 0 || verb == "request_expansion_land" && building.Length > 0)
        ? new(town, verb == "request_land_use" ? null : building, plot) : null;
}

public sealed record HouseholdLandFilingActionPlan(string ActorId, HouseholdLandFilingChoice Choice, string Household,
    IReadOnlyList<GridPoint> OfferedTiles, IReadOnlyList<GridPoint>? SelectedTiles, PlacedBuilding? Building) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "file that selected household land plot";
}
