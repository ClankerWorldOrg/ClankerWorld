namespace ClankerWorld.Simulation.World;

public sealed record ExpansionChoice(string BuildingId)
{
    public const string Prefix = "expand_building:";
    public static ExpansionChoice? Decode(string candidate) => candidate.StartsWith(Prefix, StringComparison.Ordinal)
        ? new(candidate[Prefix.Length..]) : null;
    public string Encode() => Prefix + BuildingId;
}

public sealed record ExpansionMaterialInput(string LotId, string OwnerId, string ItemKind, int Quantity);

public abstract record ExpansionMaterialStep(string ItemKind);
public sealed record ExpansionDeliveryStep(string Kind, string LotId, int Quantity, bool Store)
    : ExpansionMaterialStep(Kind);
public sealed record ExpansionPickupStep(string Kind, string LotId, string OwnerId, int Quantity,
    GridPoint Position, int InteractionRange) : ExpansionMaterialStep(Kind);
public sealed record ExpansionGatherStep(string Kind, MapResource Source, ToolGatheringPlan Harvest,
    string? ToolOwnerId, string? ToolKind, IReadOnlyList<GridPoint> ReturnRoute) : ExpansionMaterialStep(Kind);

public sealed record ExpansionActionPlan(string ActorId, ExpansionChoice Choice, PlacedBuilding Building,
    GridPoint TargetPosition, BuildingFootprintRevision TargetFootprint, string OwnerId,
    IReadOnlyList<ExpansionMaterialInput>? Inputs, IReadOnlyList<GridPoint> Route,
    ExpansionMaterialStep? MaterialStep)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "expand that selected building into its planned footprint";
}
