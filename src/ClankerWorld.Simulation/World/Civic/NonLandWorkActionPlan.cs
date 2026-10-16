namespace ClankerWorld.Simulation.World;

public sealed record NonLandWorkChoice(string TownId, string AgreementId, string TermId)
{
    public string Encode() => DerivedId.Hash(System.Text.Json.JsonSerializer.Serialize(new { Town = TownId, Agreement = AgreementId, Term = TermId }), "nonviolent_remedy:");
}

public enum NonLandWorkPhase { ReturnGoods, ServiceTravel, ServiceDelivery, StartRepair, ContinueRepair }

public sealed record NonLandWorkActionPlan(string ActorId, NonLandWorkChoice Choice, TownRestorativeAgreement Agreement,
    TownRemedyTerm Term, int Remaining, NonLandWorkPhase Phase, string? StockId, string? ItemKind, int Quantity,
    string? WarehouseId, GridPoint? Destination, IReadOnlyList<GridPoint> Route, ActionPlan? Repair,
    EquipmentRepairWork? RepairProgress) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "complete that selected voluntary contribution";
}

public sealed record NonLandReportRelayChoice(string TownId, string ConductId, string Recipient)
{
    public string Encode() => DerivedId.Hash(System.Text.Json.JsonSerializer.Serialize(new { Town = TownId, Act = ConductId, Listener = Recipient }), "nonviolent_relay:");
}

public sealed record NonLandReportRelayActionPlan(string ActorId, NonLandReportRelayChoice Choice, TownConductRecord Conduct,
    TownConductAcquisition Source, string Statement) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "relay that selected known act";
}
