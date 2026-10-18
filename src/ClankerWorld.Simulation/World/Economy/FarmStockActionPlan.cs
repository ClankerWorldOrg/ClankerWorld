namespace ClankerWorld.Simulation.World;

public enum FarmStockActionKind { Grain, Flour }

public sealed record FarmStockActionPlan(string ActorId, FarmStockActionKind Kind, string HouseholdId,
    string CarrierId, string ResourceId, string ItemKind, int TransferQuantity, int ResourceQuantity,
    int PhysicalQuantity, bool MoveContainerFamily, string DestinationId, GridPoint Destination,
    GridPoint Source, int SourceRange, string? SourceBuildingId, int? ResourceQuantityLimit,
    IReadOnlyList<PlannedStockMember>? Family, IReadOnlyList<GridPoint> Route,
    IReadOnlyList<GridPoint> ReturnRoute) : ActionPlan(ActorId, Candidate(Kind))
{
    public static string Candidate(FarmStockActionKind kind) => kind == FarmStockActionKind.Grain ? "haul_farm_grain" : "haul_farm_flour";

    public static bool TryDecode(string candidate, out FarmStockActionKind choice)
    {
        choice = candidate == Candidate(FarmStockActionKind.Grain) ? FarmStockActionKind.Grain : FarmStockActionKind.Flour;
        return candidate == Candidate(choice);
    }

    public override string Activity => Kind == FarmStockActionKind.Grain ? "carry crops to farm storage" : "carry flour to the House";
}
