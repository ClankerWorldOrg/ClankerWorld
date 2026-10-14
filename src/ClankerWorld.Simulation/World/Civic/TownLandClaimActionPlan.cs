namespace ClankerWorld.Simulation.World;

public sealed record TownLandClaimChoice(string TownId)
{
    public string Encode() => $"civic|{TownId}|claim_land||";
    public static TownLandClaimChoice? Decode(string value) => value.Split('|') is ["civic", var town, "claim_land", "", ""] && town.Length > 0 ? new(town) : null;
}

public sealed record TownLandClaimActionPlan(string ActorId, TownLandClaimChoice Choice,
    IReadOnlyList<GridPoint> OfferedTiles, IReadOnlyList<GridPoint>? SelectedTiles = null) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "propose that selected Town land claim";
}
