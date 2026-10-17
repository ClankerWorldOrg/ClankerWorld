using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed record BuildingMaterialChoice(string ItemKind)
{
    public const string Prefix = "gather_building_material:";
    public string Encode() => IdentityRules.PrefixedReference(Prefix, ItemKind);
}

public sealed record BuildingMaterialActionPlan(string ActorId, BuildingMaterialChoice Choice, string HouseholdId,
    BuildingDefinition Building, ContentQuantity Material, string? HouseId, GridPoint Destination, int DestinationRange,
    MapResource Source, ToolGatheringPlan Harvest, string? ToolOwnerId, string? ToolKind,
    IReadOnlyList<GridPoint> Route, IReadOnlyList<GridPoint> ReturnRoute)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => $"gather {Choice.ItemKind.Replace('_', ' ')} for their household building";
    public int PhysicalQuantity => checked(Harvest.Quantity + Harvest.TreeSeedQuantity);
}
