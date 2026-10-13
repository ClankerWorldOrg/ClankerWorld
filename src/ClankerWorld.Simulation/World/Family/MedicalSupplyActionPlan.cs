namespace ClankerWorld.Simulation.World;

public sealed record MedicalSupplyChoice(string ItemKind = "medicine")
{
    public const string Id = "medical_collect:medicine";
    public string Encode() => "medical_collect:" + ItemKind;
    public static MedicalSupplyChoice? Decode(string value) => value == Id ? new() : null;
}

public abstract record MedicalSupplyActionPlan(string ActorId, string PatientId, GridPoint LastSeenPosition,
    MedicalSupplyTrip? OriginalTrip) : ActionPlan(ActorId, MedicalSupplyChoice.Id);

public sealed record MedicalSupplyPickupPlan(string ActorId, string PatientId, GridPoint LastSeenPosition,
    MedicalSupplyTrip? OriginalTrip, PlannedEquipmentPickup Pickup, IReadOnlyList<GridPoint> Route)
    : MedicalSupplyActionPlan(ActorId, PatientId, LastSeenPosition, OriginalTrip)
{
    public override string Activity => "fetch the medicine dose";
}

public sealed record MedicalSupplyReturnPlan(string ActorId, string PatientId, GridPoint LastSeenPosition,
    MedicalSupplyTrip OriginalTrip, string StockId, GoodsPlace Place, IReadOnlyList<GridPoint> Route)
    : MedicalSupplyActionPlan(ActorId, PatientId, LastSeenPosition, OriginalTrip)
{
    public override string Activity => "return with the medicine";
}
