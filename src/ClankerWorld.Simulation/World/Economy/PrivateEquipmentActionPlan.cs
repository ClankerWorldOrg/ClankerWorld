namespace ClankerWorld.Simulation.World;

public enum PrivateEquipmentKind { Garment, CarryAid }

public sealed record PrivateEquipmentActionPlan(string ActorId, PrivateEquipmentKind Kind,
    string? HouseholdId, string StockId, string OwnerId, string ItemKind, string? PreviousSlotId,
    GridPoint Position, int InteractionRange, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, Kind == PrivateEquipmentKind.CarryAid ? "equip_carry_aid" : "wear_clothing")
{
    public override string Activity => Kind == PrivateEquipmentKind.CarryAid
        ? "equip the selected carrying aid" : "wear the selected garment";
}
