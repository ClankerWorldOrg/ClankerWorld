namespace ClankerWorld.Simulation.World;

public enum LandSaleExchangeKind { Meet, Pay }
public enum LandSaleExchangeStage { Approach, Wait, Exchange }

public sealed record LandSaleExchangeChoice(string TownId, string TermsToken, LandSaleExchangeKind Kind)
{
    public string Encode() => $"civic|{TownId}|{(Kind == LandSaleExchangeKind.Meet ? "land_transfer_meet" : "land_transfer_pay")}|{TermsToken}|";
    public static LandSaleExchangeChoice? Decode(string value) => value.Split('|') is ["civic", var town, var verb, var terms, ""] &&
        town.Length > 0 && terms.Length > 0 && verb is "land_transfer_meet" or "land_transfer_pay"
            ? new(town, terms, verb == "land_transfer_meet" ? LandSaleExchangeKind.Meet : LandSaleExchangeKind.Pay) : null;
}

public sealed record LandSalePaymentStock(string LotId, int Quantity);

public sealed record LandSaleExchangeActionPlan(string ActorId, LandSaleExchangeChoice Choice,
    TownLandTransferRequest Request, GridPoint NoticePlace, LandSaleExchangeStage Stage, string? Seller,
    IReadOnlyList<LandSalePaymentStock> Goods, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Kind == LandSaleExchangeKind.Meet
        ? "meet the buyer for those accepted permission terms" : "pay those selected goods for the accepted permission sale";
}
