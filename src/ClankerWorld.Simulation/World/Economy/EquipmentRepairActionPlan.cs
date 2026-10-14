namespace ClankerWorld.Simulation.World;

public readonly record struct EquipmentRepairChoice
{
    public const string Candidate = "repair_equipment";
    public static string Encode() => Candidate;
    public static bool TryDecode(string candidate, out EquipmentRepairChoice choice)
    {
        choice = new();
        return candidate == Candidate;
    }
}

public sealed record PlannedEquipmentRepairInput(int Quantity, PlannedEquipmentPickup Pickup);

public sealed record EquipmentRepairActionPlan(string ActorId, string TargetId, string ItemKind,
    string HouseholdId, string BuildingId, GridPoint Destination,
    IReadOnlyList<PlannedRepairMaterial> Materials, IReadOnlyList<PlannedEquipmentRepairInput> Inputs,
    IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, EquipmentRepairChoice.Candidate)
{
    public override string Activity => "prepare the selected equipment repair";
}
