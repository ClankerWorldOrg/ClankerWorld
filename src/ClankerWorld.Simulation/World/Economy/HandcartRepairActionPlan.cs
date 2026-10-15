namespace ClankerWorld.Simulation.World;

public sealed record HandcartRepairChoice(string CartId)
{
    public const string Prefix = "repair_handcart:";
    public string Encode() => Prefix + CartId;
    public static HandcartRepairChoice? Decode(string value) => value.StartsWith(Prefix, StringComparison.Ordinal) ? new(value[Prefix.Length..]) : null;
}

public sealed record HandcartRepairInputChoice(string ItemKind)
{
    public const string Prefix = "collect_handcart_repair:";
    public string Encode() => Prefix + ItemKind;
    public static HandcartRepairInputChoice? Decode(string value) => value.StartsWith(Prefix, StringComparison.Ordinal) ? new(value[Prefix.Length..]) : null;
}

public sealed record HandcartRepairActionPlan(string ActorId, HandcartRepairChoice Choice, GridPoint Position,
    IReadOnlyList<PlannedStockMember> Family, HandcartHitch? Hitch, IReadOnlyList<PlannedRepairMaterial> Materials,
    IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "repair the handcart";
}

public sealed record HandcartRepairInputActionPlan(string ActorId, HandcartRepairInputChoice Choice, string CartId,
    GridPoint Position, IReadOnlyList<PlannedStockMember> Family, HandcartHitch? Hitch,
    PlannedEquipmentPickup Pickup, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "collect the handcart repair input";
}
