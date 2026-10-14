namespace ClankerWorld.Simulation.World;

public sealed record ExpansionChoice(string BuildingId)
{
    public const string Prefix = "expand_building:";
    public string Encode() => Prefix + BuildingId;
}

public sealed record ExpansionMaterialInput(string LotId, string OwnerId, string ItemKind, int Quantity);

public sealed record ExpansionActionPlan(string ActorId, ExpansionChoice Choice, PlacedBuilding Building,
    GridPoint TargetPosition, BuildingFootprintRevision TargetFootprint, string OwnerId,
    IReadOnlyList<ExpansionMaterialInput>? Inputs, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "expand that selected building into its planned footprint";
}
