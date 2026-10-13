namespace ClankerWorld.Simulation.World;

public sealed record PlannedEquipmentRepairInput(int Quantity, PlannedEquipmentPickup Pickup);

public sealed record EquipmentRepairActionPlan(string ActorId, string TargetId, string ItemKind,
    string HouseholdId, string BuildingId, GridPoint Destination,
    IReadOnlyList<PlannedRepairMaterial> Materials, IReadOnlyList<PlannedEquipmentRepairInput> Inputs,
    IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, "repair_equipment")
{
    public override string Activity => "prepare the selected equipment repair";
}
