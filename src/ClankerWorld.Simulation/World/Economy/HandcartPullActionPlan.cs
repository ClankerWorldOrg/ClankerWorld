namespace ClankerWorld.Simulation.World;

public sealed record HandcartPullChoice(string BuildingId)
{
    public const string Prefix = "pull_handcart:";
    public string Encode() => Prefix + BuildingId;
    public static HandcartPullChoice? Decode(string value) => value.StartsWith(Prefix, StringComparison.Ordinal)
        ? new(value[Prefix.Length..]) : null;
}

public sealed record HandcartPullActionPlan(string ActorId, HandcartPullChoice Choice, string CartId,
    GridPoint Origin, IReadOnlyList<PlannedStockMember> Family, HandcartHitch Hitch,
    GridPoint Destination, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "pull the handcart";
}
