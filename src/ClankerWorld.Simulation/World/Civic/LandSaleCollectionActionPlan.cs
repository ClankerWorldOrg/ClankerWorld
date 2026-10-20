namespace ClankerWorld.Simulation.World;

public sealed record LandSaleCollectionChoice(string TownId, string TermsToken)
{
    public string Encode() => $"civic|{TownId}|land_transfer_collect_payment|{TermsToken}|";
    public static LandSaleCollectionChoice? Decode(string value) =>
        value.Split('|') is ["civic", var town, "land_transfer_collect_payment", var terms, ""] &&
        IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true) && IdentityRules.Reference(terms, DerivedId.MaximumLength)
            ? new(town, terms) : null;
}

public sealed record LandSaleCollectionActionPlan(string ActorId, LandSaleCollectionChoice Choice,
    TownLandTransferRequest Request, GridPoint NoticePlace, PlannedEquipmentPickup Pickup, int Quantity,
    IReadOnlyList<GridPoint> Route, IReadOnlyList<GridPoint> ReturnRoute) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "collect those selected goods for the accepted permission sale";
}
