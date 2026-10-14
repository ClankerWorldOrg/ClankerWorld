namespace ClankerWorld.Simulation.World;

public sealed record ToolCollectionChoice(string ItemKind)
{
    public const string Prefix = "collect_tool:";
    public string Encode() => ItemKind switch
    {
        "wooden_axe" => "collect_wooden_axe",
        "wooden_pickaxe" => "collect_wooden_pickaxe",
        "wooden_hoe" => "collect_wooden_hoe",
        _ => IdentityRules.PrefixedReference(Prefix, ItemKind),
    };
    public static ToolCollectionChoice? Decode(string candidate) => candidate switch
    {
        "collect_wooden_axe" => new("wooden_axe"),
        "collect_wooden_pickaxe" => new("wooden_pickaxe"),
        "collect_wooden_hoe" => new("wooden_hoe"),
        _ => candidate.StartsWith(Prefix, StringComparison.Ordinal) ? new(candidate[Prefix.Length..]) : null,
    };
}

public sealed record ToolCollectionActionPlan(string ActorId, ToolCollectionChoice Choice,
    string? HouseholdId, PlannedEquipmentPickup Pickup, int MinimumFreeAfterPickup,
    PlannedCargoStorage? FutureCargoStorage, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => $"collect the {Choice.ItemKind.Replace('_', ' ')}";
}
