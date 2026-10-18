namespace ClankerWorld.Simulation.World;

public enum PrivateEquipmentKind { Garment, CarryAid }

public sealed record PrivateEquipmentActionPlan(string ActorId, PrivateEquipmentKind Kind,
    string? HouseholdId, string StockId, string OwnerId, string ItemKind, string? PreviousSlotId,
    GridPoint Position, int InteractionRange, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, Candidate(Kind))
{
    public static string Candidate(PrivateEquipmentKind kind) => kind == PrivateEquipmentKind.CarryAid ? "equip_carry_aid" : "wear_clothing";

    public static bool TryDecode(string candidate, out PrivateEquipmentKind choice)
    {
        choice = candidate == Candidate(PrivateEquipmentKind.CarryAid) ? PrivateEquipmentKind.CarryAid : PrivateEquipmentKind.Garment;
        return candidate == Candidate(choice);
    }

    public override string Activity => Kind == PrivateEquipmentKind.CarryAid
        ? "equip the selected carrying aid" : "wear the selected garment";
}
