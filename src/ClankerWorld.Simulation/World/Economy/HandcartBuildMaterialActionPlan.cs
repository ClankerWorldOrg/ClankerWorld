namespace ClankerWorld.Simulation.World;

public sealed record HandcartBuildMaterialChoice(string ItemKind)
{
    public const string Prefix = "collect_handcart_material:";
    public string Encode() => Prefix + ItemKind;
    public static HandcartBuildMaterialChoice? Decode(string value) => value.StartsWith(Prefix, StringComparison.Ordinal) ? new(value[Prefix.Length..]) : null;
}

public sealed record HandcartBuildMaterialActionPlan(string ActorId, HandcartBuildMaterialChoice Choice,
    string HouseholdId, string RecipeId, int InputAmount, string SmithId, GridPoint Destination,
    PlannedEquipmentPickup Pickup, IReadOnlyList<PlannedStockMember> Family, int Quantity,
    IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "collect the handcart building material";
}
