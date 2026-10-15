namespace ClankerWorld.Simulation.World;

public sealed record CivicVisitChoice(string TownId)
{
    public string Encode() => $"civic|{TownId}|visit||";
    public static CivicVisitChoice? Decode(string value) => value.Split('|') is ["civic", var town, "visit", "", ""] && IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true)
        ? new(town) : null;
}

public sealed record CivicVisitActionPlan(string ActorId, CivicVisitChoice Choice, GridPoint NoticePlace,
    IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "visit that Town's public notice place";
}
