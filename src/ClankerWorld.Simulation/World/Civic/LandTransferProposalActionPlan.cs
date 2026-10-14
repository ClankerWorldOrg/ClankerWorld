namespace ClankerWorld.Simulation.World;

public sealed record LandTransferProposalChoice(string TownId, string SourceHousehold, string SourceToken, bool Sale)
{
    public string Encode() => $"civic|{TownId}|{(Sale ? "land_transfer_sell" : "land_transfer_propose")}|{SourceHousehold}|{SourceToken}";
    public static LandTransferProposalChoice? Decode(string value) => value.Split('|') is ["civic", var town, var verb, var household, var source] &&
        town.Length > 0 && household.Length > 0 && source.Length > 0 && verb is "land_transfer_propose" or "land_transfer_sell"
        ? new(town, household, source, verb == "land_transfer_sell") : null;
}

public sealed record LandTransferBeneficiary(string HouseholdId, string Name, IReadOnlyList<GridPoint> Tiles);

public sealed record LandTransferProposalActionPlan(string ActorId, LandTransferProposalChoice Choice, GridPoint NoticePlace,
    IReadOnlyList<LandTransferBeneficiary> Beneficiaries, string? TargetHousehold = null,
    IReadOnlyList<GridPoint>? Tiles = null, TownLandSalePrice? Price = null, bool Bound = false,
    bool InvalidTerms = false) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "propose that selected household permission transfer";
}
